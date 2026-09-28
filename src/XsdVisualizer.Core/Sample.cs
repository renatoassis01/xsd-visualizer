namespace XsdVisualizer.Core;

public enum SampleKind { Maximal, Minimal, Coverage }

/// <summary>
/// A parte de um Sample que é Envelope: de qual Request/Response, com qual Payload Binding e,
/// quando o Payload vai compactado, o Payload legível.
/// </summary>
public sealed record SampleEnvelope(OperationMessage Message, PayloadBinding? PayloadBinding, string? DecompressedPayload);

/// <summary>XML gerado pelo app a partir de um Global Element; quando é um Envelope, traz também a parte <see cref="Envelope"/>.</summary>
public sealed class Sample
{
    private readonly Func<string, IReadOnlyList<ValidationIssue>> _validate;

    internal Sample(GlobalElement? element, SampleKind kind, int number, string xml, IReadOnlyList<string> covers,
        IReadOnlyList<ValidationIssue> issues, Func<string, IReadOnlyList<ValidationIssue>> validate, SampleEnvelope? envelope = null)
    {
        Element = element;
        Kind = kind;
        Number = number;
        Xml = xml;
        Covers = covers;
        Issues = issues;
        _validate = validate;
        Envelope = envelope;
    }

    /// <summary>O Global Element que gerou o conteúdo; num Envelope, o do Payload (null sem Payload Binding).</summary>
    public GlobalElement? Element { get; }
    public SampleKind Kind { get; }
    /// <summary>Posição (1..n) dentro do Coverage Set; 0 para Maximal e Minimal.</summary>
    public int Number { get; }
    public string Xml { get; }
    /// <summary>O que este Sample cobre pela primeira vez no Coverage Set (ramos e valores de enumeração).</summary>
    public IReadOnlyList<string> Covers { get; }
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool IsValid => Issues.All(i => i.Severity != IssueSeverity.Error);

    /// <summary>Presente quando este Sample é um Envelope.</summary>
    public SampleEnvelope? Envelope { get; }

    /// <summary>Valida outro texto (ex.: o Sample editado) do mesmo jeito que este Sample foi validado.</summary>
    public IReadOnlyList<ValidationIssue> Validate(string xml) => _validate(xml);

    public string FileName
    {
        get
        {
            var name = Envelope is { Message: var m } ? $"{m.Operation.Name}.{m.Direction.FileSuffix()}" : Element!.Name;
            return Kind switch
            {
                SampleKind.Maximal => $"{name}.max.xml",
                SampleKind.Minimal => $"{name}.min.xml",
                _ => $"{name}.cov-{Number:00}.xml",
            };
        }
    }
}

internal static class SampleComments
{
    public static string Coverage(string title, IReadOnlyList<string> covers)
    {
        var lines = new List<string> { " " + title };
        if (covers.Count > 0)
        {
            lines.Add(" cobre:");
            lines.AddRange(covers.Select(c => "   " + c));
        }
        return string.Join("\n", lines).Replace("--", "- -") + "\n";
    }

    public static string Insert(string xml, string comment)
    {
        var endOfDeclaration = xml.IndexOf("?>", StringComparison.Ordinal) + 2;
        return $"{xml[..endOfDeclaration]}\n<!--{comment}-->{xml[endOfDeclaration..]}";
    }
}
