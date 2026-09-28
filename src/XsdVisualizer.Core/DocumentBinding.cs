using System.Xml;
using System.Xml.Linq;

namespace XsdVisualizer.Core;

/// <summary>Uma Operation e a direção (Request/Response) cujo Body casa com o de um Envelope.</summary>
public sealed record OperationCandidate(Operation Operation, MessageDirection Direction);

/// <summary>
/// Binding de um Document: os Global Elements (dos Schema Sets abertos) com o mesmo nome e namespace
/// da raiz do XML. Com um só candidato, o vínculo é automático; com vários, o usuário escolhe.
/// Num Envelope SOAP, também as Operations cujo Body casa com o do Envelope; os Global Elements
/// candidatos passam a ser os da raiz do Payload.
/// </summary>
public sealed class DocumentBinding
{
    private DocumentBinding(string? rootName, string? rootNamespace, IReadOnlyList<GlobalElement> candidates,
        bool isEnvelope, IReadOnlyList<OperationCandidate> operationCandidates)
    {
        RootName = rootName;
        RootNamespace = rootNamespace;
        Candidates = candidates;
        IsEnvelope = isEnvelope;
        OperationCandidates = operationCandidates;
    }

    public string? RootName { get; }
    public string? RootNamespace { get; }
    public IReadOnlyList<GlobalElement> Candidates { get; }
    public GlobalElement? Bound => Candidates.Count == 1 ? Candidates[0] : null;

    public bool IsEnvelope { get; }
    public IReadOnlyList<OperationCandidate> OperationCandidates { get; }
    public OperationCandidate? BoundOperation => OperationCandidates.Count == 1 ? OperationCandidates[0] : null;

    public static DocumentBinding Find(string xml, IEnumerable<SchemaSet> openSchemaSets)
    {
        var sets = openSchemaSets.ToList();
        XElement root;
        try
        {
            root = XDocument.Parse(xml).Root!;
        }
        catch (XmlException)
        {
            return new DocumentBinding(null, null, [], false, []);
        }

        var name = root.Name.LocalName;
        var ns = root.Name.NamespaceName;
        if (name != "Envelope" || !Soap.IsEnvelopeNamespace(ns))
            return new DocumentBinding(name, ns, ElementsNamed(sets, root.Name), false, []);

        var wrapper = root.Element(XName.Get("Body", ns))?.Elements().FirstOrDefault();
        var operations = wrapper is null ? [] : sets
            .SelectMany(s => s.Services).SelectMany(s => s.Operations)
            .SelectMany(o => new[] { o.Request, o.Response }
                .Where(m => m.BodyElementName == wrapper.Name.LocalName && m.BodyElementNamespace == wrapper.Name.NamespaceName)
                .Select(m => new OperationCandidate(o, m.Direction)))
            .ToList();
        var payloadRoot = wrapper?.Elements().FirstOrDefault();
        var payloadCandidates = payloadRoot is null ? [] : ElementsNamed(sets, payloadRoot.Name);
        return new DocumentBinding(name, ns, payloadCandidates, true, operations);
    }

    private static List<GlobalElement> ElementsNamed(IEnumerable<SchemaSet> sets, XName name) =>
        sets.SelectMany(s => s.GlobalElements)
            .Where(e => e.Name == name.LocalName && e.Namespace == name.NamespaceName)
            .ToList();
}
