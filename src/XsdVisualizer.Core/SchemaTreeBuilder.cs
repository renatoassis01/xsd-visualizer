using System.Xml;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

/// <summary>Constrói (sob demanda) a árvore de um Global Element a partir do schema compilado.</summary>
internal sealed class SchemaTreeBuilder(XmlSchemaSet schemas)
{
    public SchemaNode Build(XmlSchemaElement element) => ElementNode(element, null, element.QualifiedName.Name, 1, 1);

    /// <summary>
    /// Um elemento, ou um Choice com as alternativas dele quando é cabeça de substitution group
    /// ou tem tipo com derivações (xsi:type).
    /// </summary>
    private SchemaNode ElementOrAlternatives(XmlSchemaElement element, SchemaNode parent, int min, int? max)
    {
        var declaration = Resolve(element);
        var name = declaration.QualifiedName.Name;
        var path = parent.Path + "/" + name;

        var members = SubstitutionMembers(declaration);
        if (members.Count > 0)
        {
            var alternatives = (declaration.IsAbstract ? [] : new[] { declaration }).Concat(members).ToList();
            return AlternativesNode(name, path, parent, min, max, ChoiceOrigin.SubstitutionGroup,
                self => alternatives.Select(a => ElementNode(a, self, self.Path + "/" + a.QualifiedName.Name, 1, 1)).ToList());
        }

        var types = DerivedTypes(declaration.ElementSchemaType);
        if (types.Count > 0)
        {
            var baseType = declaration.ElementSchemaType!;
            var alternatives = (baseType is XmlSchemaComplexType { IsAbstract: true } ? [] : new[] { baseType }).Concat(types).ToList();
            return AlternativesNode(name, path, parent, min, max, ChoiceOrigin.DerivedTypes,
                self => alternatives.Select(t => ElementNode(declaration, self, $"{self.Path}/{name}[{t.QualifiedName.Name}]", 1, 1,
                    overrideType: t == baseType ? null : t)).ToList());
        }

        return ElementNode(declaration, parent, path, min, max);
    }

    private static SchemaNode AlternativesNode(string name, string path, SchemaNode parent, int min, int? max,
        ChoiceOrigin origin, Func<SchemaNode, IReadOnlyList<SchemaNode>> alternatives) =>
        new(NodeKind.Choice, name, path, parent, alternatives)
        {
            Name = name,
            MinOccurs = min,
            MaxOccurs = max,
            Origin = origin,
        };

    private List<XmlSchemaElement> SubstitutionMembers(XmlSchemaElement head)
    {
        var members = new List<XmlSchemaElement>();
        var heads = new HashSet<XmlQualifiedName> { head.QualifiedName };
        bool added;
        do
        {
            added = false;
            foreach (var candidate in schemas.GlobalElements.Values.Cast<XmlSchemaElement>())
            {
                if (candidate.SubstitutionGroup.IsEmpty || !heads.Contains(candidate.SubstitutionGroup)) continue;
                if (!heads.Add(candidate.QualifiedName)) continue;
                added = true;
                if (!candidate.IsAbstract) members.Add(candidate);
            }
        } while (added);
        return members.OrderBy(DeclarationOrder).ToList();
    }

    private List<XmlSchemaType> DerivedTypes(XmlSchemaType? baseType)
    {
        if (baseType is not XmlSchemaComplexType || baseType.QualifiedName.IsEmpty) return [];
        return schemas.GlobalTypes.Values.OfType<XmlSchemaComplexType>()
            .Where(t => t != baseType && !t.IsAbstract && DerivesFrom(t, baseType))
            .OrderBy(DeclarationOrder)
            .Cast<XmlSchemaType>()
            .ToList();
    }

    private static bool DerivesFrom(XmlSchemaType type, XmlSchemaType baseType)
    {
        for (var t = type.BaseXmlSchemaType; t is not null; t = t.BaseXmlSchemaType)
            if (t == baseType) return true;
        return false;
    }

    private SchemaNode ElementNode(XmlSchemaElement element, SchemaNode? parent, string path, int min, int? max,
        XmlSchemaType? overrideType = null)
    {
        var declaration = Resolve(element);
        var type = overrideType ?? declaration.ElementSchemaType;
        return new SchemaNode(NodeKind.Element, declaration.QualifiedName.Name, path, parent, ElementChildren)
        {
            Name = declaration.QualifiedName.Name,
            Namespace = declaration.QualifiedName.Namespace,
            MinOccurs = min,
            MaxOccurs = max,
            TypeName = TypeName(type),
            Documentation = Documentation(declaration.Annotation) ?? Documentation(type?.Annotation),
            Facets = Facets(SimpleTypeOf(type)),
            FixedValue = declaration.FixedValue,
            DefaultValue = declaration.DefaultValue,
            IsRecursive = type is XmlSchemaComplexType && Ancestors(parent).Any(a => a.Kind == NodeKind.Element && a.SchemaType == type),
            SchemaType = type,
            SimpleType = SimpleTypeOf(type),
            XsiTypeName = overrideType?.QualifiedName,
        };
    }

    private static IEnumerable<SchemaNode> Ancestors(SchemaNode? node)
    {
        for (var n = node; n is not null; n = n.Parent) yield return n;
    }

    private IReadOnlyList<SchemaNode> ElementChildren(SchemaNode node)
    {
        if (node.SchemaType is not XmlSchemaComplexType complex) return [];
        var children = new List<SchemaNode>();
        foreach (var attribute in complex.AttributeUses.Values.Cast<XmlSchemaAttribute>().OrderBy(DeclarationOrder))
        {
            if (attribute.Use == XmlSchemaUse.Prohibited) continue;
            var type = attribute.AttributeSchemaType;
            children.Add(new SchemaNode(NodeKind.Attribute, "@" + attribute.QualifiedName.Name,
                node.Path + "/@" + attribute.QualifiedName.Name, node, _ => [])
            {
                Name = attribute.QualifiedName.Name,
                Namespace = attribute.QualifiedName.Namespace,
                MinOccurs = attribute.Use == XmlSchemaUse.Required ? 1 : 0,
                MaxOccurs = 1,
                TypeName = TypeName(type),
                Documentation = Documentation(attribute.Annotation) ?? Documentation(type?.Annotation),
                Facets = Facets(type),
                FixedValue = attribute.FixedValue,
                DefaultValue = attribute.DefaultValue,
                SchemaType = type,
                SimpleType = type,
            });
        }
        if (complex.AttributeWildcard is { } anyAttribute)
            children.Add(new SchemaNode(NodeKind.AnyAttribute, "@any", node.Path + "/@any", node, _ => [])
            {
                MinOccurs = 0,
                WildcardNamespace = anyAttribute.Namespace ?? "##any",
                Documentation = Documentation(anyAttribute.Annotation),
            });
        if (Particle(complex.ContentTypeParticle, node, children.Count) is { } content)
            children.Add(content);
        return children;
    }

    private SchemaNode? Particle(XmlSchemaParticle? particle, SchemaNode parent, int index)
    {
        if (particle is null || particle.GetType().Name == "EmptyParticle") return null;
        var min = (int)Math.Min(particle.MinOccurs, int.MaxValue);
        int? max = particle.MaxOccurs >= int.MaxValue ? null : (int)particle.MaxOccurs;

        switch (particle)
        {
            case XmlSchemaElement element:
                return ElementOrAlternatives(element, parent, min, max);
            case XmlSchemaAny any:
                return new SchemaNode(NodeKind.AnyElement, "any", $"{parent.Path}/any[{index}]", parent, _ => [])
                {
                    MinOccurs = min,
                    MaxOccurs = max,
                    WildcardNamespace = any.Namespace ?? "##any",
                    Documentation = Documentation(any.Annotation),
                };
            case XmlSchemaGroupRef group when group.Particle is { } inner:
                var node = Particle(inner, parent, index);
                return node;
            case XmlSchemaGroupBase group:
                var (kind, label) = group switch
                {
                    XmlSchemaChoice => (NodeKind.Choice, "choice"),
                    XmlSchemaAll => (NodeKind.All, "all"),
                    _ => (NodeKind.Sequence, "sequence"),
                };
                return new SchemaNode(kind, label, $"{parent.Path}/{label}[{index}]", parent,
                    self => group.Items.Cast<XmlSchemaParticle>()
                        .Select((p, i) => Particle(p, self, i))
                        .OfType<SchemaNode>()
                        .ToList())
                {
                    Name = label,
                    MinOccurs = min,
                    MaxOccurs = max,
                    Origin = kind == NodeKind.Choice ? ChoiceOrigin.Compositor : null,
                };
            default:
                return null;
        }
    }

    private XmlSchemaElement Resolve(XmlSchemaElement element) =>
        !element.RefName.IsEmpty && schemas.GlobalElements[element.RefName] is XmlSchemaElement global ? global : element;

    private static (string, int, int) DeclarationOrder(XmlSchemaObject o) => (o.SourceUri ?? "", o.LineNumber, o.LinePosition);

    internal static XmlSchemaSimpleType? SimpleTypeOf(XmlSchemaType? type)
    {
        for (var t = type; t is not null; t = t.BaseXmlSchemaType)
        {
            if (t is XmlSchemaSimpleType simple) return simple;
            if (t is XmlSchemaComplexType { ContentType: not XmlSchemaContentType.TextOnly }) return null;
        }
        return null;
    }

    private static string? TypeName(XmlSchemaType? type)
    {
        for (var t = type; t is not null; t = t.BaseXmlSchemaType)
        {
            if (t.QualifiedName.IsEmpty)
            {
                if (t is XmlSchemaComplexType) return null;
                continue;
            }
            return t.QualifiedName.Namespace == XmlSchema.Namespace ? "xs:" + t.QualifiedName.Name : t.QualifiedName.Name;
        }
        return null;
    }

    private static IReadOnlyList<Facet> Facets(XmlSchemaSimpleType? type)
    {
        var facets = new List<Facet>();
        for (var t = type; t is XmlSchemaSimpleType simple; t = t.BaseXmlSchemaType as XmlSchemaSimpleType)
        {
            if (simple.Content is not XmlSchemaSimpleTypeRestriction restriction) continue;
            foreach (var facet in restriction.Facets.OfType<XmlSchemaFacet>())
                facets.Add(new Facet(FacetKind(facet), facet.Value ?? ""));
        }
        return facets;
    }

    private static string FacetKind(XmlSchemaFacet facet) => facet switch
    {
        XmlSchemaEnumerationFacet => "enumeration",
        XmlSchemaPatternFacet => "pattern",
        XmlSchemaLengthFacet => "length",
        XmlSchemaMinLengthFacet => "minLength",
        XmlSchemaMaxLengthFacet => "maxLength",
        XmlSchemaTotalDigitsFacet => "totalDigits",
        XmlSchemaFractionDigitsFacet => "fractionDigits",
        XmlSchemaMinInclusiveFacet => "minInclusive",
        XmlSchemaMaxInclusiveFacet => "maxInclusive",
        XmlSchemaMinExclusiveFacet => "minExclusive",
        XmlSchemaMaxExclusiveFacet => "maxExclusive",
        XmlSchemaWhiteSpaceFacet => "whiteSpace",
        _ => facet.GetType().Name,
    };

    private static string? Documentation(XmlSchemaAnnotation? annotation)
    {
        if (annotation is null) return null;
        var texts = annotation.Items.OfType<XmlSchemaDocumentation>()
            .SelectMany(d => d.Markup ?? [])
            .Select(n => n?.InnerText.Trim())
            .Where(t => !string.IsNullOrEmpty(t));
        var joined = string.Join("\n", texts);
        return joined.Length == 0 ? null : joined;
    }
}
