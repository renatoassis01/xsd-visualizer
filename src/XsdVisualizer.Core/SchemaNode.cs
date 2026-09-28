using System.Xml;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

public enum NodeKind { Element, Attribute, Sequence, Choice, All, AnyElement, AnyAttribute }

/// <summary>De onde vêm as alternativas de um nó Choice.</summary>
public enum ChoiceOrigin { Compositor, SubstitutionGroup, DerivedTypes }

public sealed record Facet(string Kind, string Value);

/// <summary>Nó da árvore de um Global Element: elemento, atributo, compositor ou wildcard.</summary>
public sealed class SchemaNode
{
    private readonly Func<SchemaNode, IReadOnlyList<SchemaNode>> _childrenFactory;
    private IReadOnlyList<SchemaNode>? _children;

    internal SchemaNode(NodeKind kind, string label, string path, SchemaNode? parent,
        Func<SchemaNode, IReadOnlyList<SchemaNode>> childrenFactory)
    {
        Kind = kind;
        Label = label;
        Path = path;
        Parent = parent;
        _childrenFactory = childrenFactory;
    }

    public NodeKind Kind { get; }
    /// <summary>Nome exibido: o nome do elemento, "@atributo", "sequence", "choice", "all", "any" ou "@any".</summary>
    public string Label { get; }
    /// <summary>Identificador estável do nó dentro do Global Element.</summary>
    public string Path { get; }
    public SchemaNode? Parent { get; }
    public string Name { get; internal init; } = "";
    public string Namespace { get; internal init; } = "";
    public int MinOccurs { get; internal init; } = 1;
    /// <summary>null quando ilimitado.</summary>
    public int? MaxOccurs { get; internal init; } = 1;
    public string? TypeName { get; internal init; }
    public string? Documentation { get; internal init; }
    public IReadOnlyList<Facet> Facets { get; internal init; } = [];
    public string? FixedValue { get; internal init; }
    public string? DefaultValue { get; internal init; }
    /// <summary>Para nós Choice: se as alternativas vêm de um xs:choice, de um substitution group ou de tipos derivados.</summary>
    public ChoiceOrigin? Origin { get; internal init; }
    /// <summary>Tipo derivado que a alternativa usa via xsi:type.</summary>
    public string? XsiType => XsiTypeName?.Name;
    /// <summary>Namespaces aceitos por um wildcard (ex.: "##other").</summary>
    public string? WildcardNamespace { get; internal init; }
    /// <summary>O tipo complexo deste elemento já aparece num ancestral.</summary>
    public bool IsRecursive { get; internal init; }

    public IReadOnlyList<SchemaNode> Children => _children ??= _childrenFactory(this);

    public string Cardinality => $"{MinOccurs}..{MaxOccurs?.ToString() ?? "n"}";

    public override string ToString() => Path;

    // Dados compilados usados pelo gerador.
    internal XmlSchemaType? SchemaType { get; init; }
    internal XmlSchemaSimpleType? SimpleType { get; init; }
    internal XmlQualifiedName? XsiTypeName { get; init; }
}
