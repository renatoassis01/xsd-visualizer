namespace XsdVisualizer.Core;

public enum SampleKind { Maximal, Minimal, Coverage }

/// <summary>XML gerado a partir de um Global Element.</summary>
public sealed class Sample
{
    internal Sample(GlobalElement element, SampleKind kind, int number, string xml,
        IReadOnlyList<string> covers, IReadOnlyList<ValidationIssue> issues)
    {
        Element = element;
        Kind = kind;
        Number = number;
        Xml = xml;
        Covers = covers;
        Issues = issues;
    }

    public GlobalElement Element { get; }
    public SampleKind Kind { get; }
    /// <summary>Posição (1..n) dentro do Coverage Set; 0 para Maximal e Minimal.</summary>
    public int Number { get; }
    public string Xml { get; }
    /// <summary>O que este Sample cobre pela primeira vez no Coverage Set (ramos e valores de enumeração).</summary>
    public IReadOnlyList<string> Covers { get; }
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool IsValid => Issues.All(i => i.Severity != IssueSeverity.Error);

    public string FileName => Kind switch
    {
        SampleKind.Maximal => $"{Element.Name}.max.xml",
        SampleKind.Minimal => $"{Element.Name}.min.xml",
        _ => $"{Element.Name}.cov-{Number:00}.xml",
    };
}
