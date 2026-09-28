namespace XsdVisualizer.Core;

public enum IssueSeverity { Error, Warning }

/// <summary>Divergência entre um XML (ou um XSD) e o seu Schema Set, localizada por arquivo, linha e coluna.</summary>
public sealed record ValidationIssue(string Message, string? File, int Line, int Column, IssueSeverity Severity = IssueSeverity.Error)
{
    public override string ToString() =>
        $"{(File is null ? "" : Path.GetFileName(File) + ":")}{Line}:{Column}: {Message}";
}
