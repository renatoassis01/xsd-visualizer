using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

internal enum GenerationMode { Maximal, Minimal }

/// <summary>Decide ramos de choice e valores de enumeração durante a geração.</summary>
internal interface IGenerationChoices
{
    int PickBranch(SchemaNode choice);
    string? PickEnumeration(SchemaNode node, IReadOnlyList<string> values);
}

/// <summary>Escreve um Sample percorrendo a árvore de um Global Element.</summary>
internal sealed class SampleGenerator
{
    private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    private const int MaxDepth = 200;

    private readonly GenerationMode _mode;
    private readonly IGenerationChoices _choices;
    private readonly Dictionary<string, int> _occurrences = new();
    private readonly ValueGenerator _values = new();
    private XmlWriter _writer = null!;

    public SampleGenerator(GenerationMode mode, IGenerationChoices choices)
    {
        _mode = mode;
        _choices = choices;
    }

    public string Generate(SchemaNode root)
    {
        var buffer = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(false),
            NewLineChars = "\n",
        };
        using (_writer = XmlWriter.Create(buffer, settings))
        {
            _writer.WriteStartDocument();
            WriteElement(root, _mode, 0);
            _writer.WriteEndDocument();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private void WriteElement(SchemaNode element, GenerationMode mode, int depth)
    {
        // Recursão: o elemento recursivo aparece, mas daí para baixo só o mínimo.
        if (element.IsRecursive) mode = GenerationMode.Minimal;

        _writer.WriteStartElement(element.Name, element.Namespace);
        if (element.XsiTypeName is { } xsiType)
        {
            var prefix = _writer.LookupPrefix(xsiType.Namespace);
            if (prefix is null)
            {
                prefix = "t";
                _writer.WriteAttributeString("xmlns", prefix, null, xsiType.Namespace);
            }
            _writer.WriteAttributeString("xsi", "type", XsiNamespace, prefix.Length == 0 ? xsiType.Name : $"{prefix}:{xsiType.Name}");
        }

        foreach (var child in element.Children)
        {
            if (child.Kind != NodeKind.Attribute) continue;
            if (child.MinOccurs == 0 && mode == GenerationMode.Minimal) continue;
            _writer.WriteAttributeString(child.Name, child.Namespace, Value(child));
        }

        if (element.SimpleType is not null)
            _writer.WriteString(Value(element));
        else if (depth < MaxDepth)
            foreach (var child in element.Children)
                if (child.Kind is not (NodeKind.Attribute or NodeKind.AnyAttribute))
                    WriteParticle(child, mode, depth + 1);

        _writer.WriteEndElement();
    }

    private void WriteParticle(SchemaNode node, GenerationMode mode, int depth)
    {
        var count = mode == GenerationMode.Minimal
            ? node.MinOccurs
            : Math.Max(node.MinOccurs, Math.Min(node.MaxOccurs ?? 2, 2));

        for (var i = 0; i < count; i++)
        {
            switch (node.Kind)
            {
                case NodeKind.Element:
                    WriteElement(node, mode, depth);
                    break;
                case NodeKind.Sequence or NodeKind.All:
                    foreach (var child in node.Children) WriteParticle(child, mode, depth);
                    break;
                case NodeKind.Choice when node.Children.Count > 0:
                    WriteParticle(node.Children[_choices.PickBranch(node)], mode, depth);
                    break;
                case NodeKind.AnyElement when i < node.MinOccurs && node.WildcardProcessing != XmlSchemaContentProcessing.Strict:
                    // Wildcards opcionais são omitidos; um obrigatório lax/skip aceita qualquer elemento não declarado.
                    _writer.WriteElementString("exemplo", PlaceholderNamespace(node), "");
                    break;
            }
        }
    }

    private static string PlaceholderNamespace(SchemaNode any)
    {
        var target = any.TargetNamespace ?? "";
        var first = (any.WildcardNamespace ?? "##any").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "##any";
        return first switch
        {
            "##any" or "##local" => "",
            "##targetNamespace" => target,
            "##other" => target == "urn:exemplo" ? "urn:exemplo:outro" : "urn:exemplo",
            _ => first,
        };
    }

    private string Value(SchemaNode node)
    {
        if (node.FixedValue is not null) return node.FixedValue;
        var variant = _occurrences[node.Path] = _occurrences.GetValueOrDefault(node.Path, -1) + 1;
        return _values.Generate(node.SimpleType!, node.Name, variant, values => _choices.PickEnumeration(node, values));
    }
}

/// <summary>Maximal/Minimal: ramos fixados pelo usuário ou o primeiro; primeira enumeração.</summary>
internal sealed class PinnedChoices(IReadOnlyDictionary<string, int>? pins) : IGenerationChoices
{
    public int PickBranch(SchemaNode choice) =>
        pins is not null && pins.TryGetValue(choice.Path, out var index) && index >= 0 && index < choice.Children.Count ? index : 0;

    public string? PickEnumeration(SchemaNode node, IReadOnlyList<string> values) => null;
}
