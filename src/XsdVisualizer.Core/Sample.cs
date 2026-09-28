namespace XsdVisualizer.Core;

public enum SampleKind { Maximal, Minimal, Coverage }

/// <summary>XML gerado pelo app: a partir de um Global Element, ou um Envelope de uma Operation.</summary>
public sealed class Sample
{
    private readonly Func<string, IReadOnlyList<ValidationIssue>> _validate;

    internal Sample(GlobalElement? element, SampleKind kind, int number, string xml, IReadOnlyList<string> covers,
        IReadOnlyList<ValidationIssue> issues, Func<string, IReadOnlyList<ValidationIssue>> validate,
        Operation? operation = null, MessageDirection? direction = null, string? payload = null)
    {
        Element = element;
        Kind = kind;
        Number = number;
        Xml = xml;
        Covers = covers;
        Issues = issues;
        _validate = validate;
        Operation = operation;
        Direction = direction;
        Payload = payload;
    }

    /// <summary>O Global Element gerado; num Envelope, o do Payload (null sem Payload Binding).</summary>
    public GlobalElement? Element { get; }
    public SampleKind Kind { get; }
    /// <summary>Posição (1..n) dentro do Coverage Set; 0 para Maximal e Minimal.</summary>
    public int Number { get; }
    public string Xml { get; }
    /// <summary>O que este Sample cobre pela primeira vez no Coverage Set (ramos e valores de enumeração).</summary>
    public IReadOnlyList<string> Covers { get; }
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool IsValid => Issues.All(i => i.Severity != IssueSeverity.Error);

    /// <summary>Operation de que este Sample é o Envelope (null para Samples de Global Element).</summary>
    public Operation? Operation { get; }
    public MessageDirection? Direction { get; }
    public bool IsEnvelope => Operation is not null;
    /// <summary>Payload legível, quando no Envelope ele vai compactado.</summary>
    public string? Payload { get; }

    /// <summary>Valida outro texto (ex.: o Sample editado) do mesmo jeito que este Sample foi validado.</summary>
    public IReadOnlyList<ValidationIssue> Validate(string xml) => _validate(xml);

    public string FileName
    {
        get
        {
            var name = Operation is { } op ? $"{op.Name}.{(Direction == MessageDirection.Response ? "response" : "request")}" : Element!.Name;
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
