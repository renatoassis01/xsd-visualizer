using System.Xml;

namespace XsdVisualizer.Core;

/// <summary>
/// Binding de um Document: os Global Elements (dos Schema Sets abertos) com o mesmo nome e namespace
/// da raiz do XML. Com um só candidato, o vínculo é automático; com vários, o usuário escolhe.
/// </summary>
public sealed class DocumentBinding
{
    private DocumentBinding(string? rootName, string? rootNamespace, IReadOnlyList<GlobalElement> candidates)
    {
        RootName = rootName;
        RootNamespace = rootNamespace;
        Candidates = candidates;
    }

    public string? RootName { get; }
    public string? RootNamespace { get; }
    public IReadOnlyList<GlobalElement> Candidates { get; }
    public GlobalElement? Bound => Candidates.Count == 1 ? Candidates[0] : null;

    public static DocumentBinding Find(string xml, IEnumerable<SchemaSet> openSchemaSets)
    {
        if (ReadRoot(xml) is not var (name, ns))
            return new DocumentBinding(null, null, []);
        var candidates = openSchemaSets
            .SelectMany(s => s.GlobalElements)
            .Where(e => e.Name == name && e.Namespace == ns)
            .ToList();
        return new DocumentBinding(name, ns, candidates);
    }

    private static (string Name, string Namespace)? ReadRoot(string xml)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            return reader.MoveToContent() == XmlNodeType.Element ? (reader.LocalName, reader.NamespaceURI) : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
