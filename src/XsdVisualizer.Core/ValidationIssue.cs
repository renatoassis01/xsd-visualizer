namespace XsdVisualizer.Core;

public enum IssueSeverity { Error, Warning }

/// <summary>
/// Divergência entre um XML (ou um XSD) e o seu Schema Set, localizada por arquivo, linha e coluna.
/// <paramref name="InPayload"/>: a posição se refere ao Payload descompactado de um Envelope, não ao Envelope.
/// </summary>
public sealed record ValidationIssue(string Message, string? File, int Line, int Column,
    IssueSeverity Severity = IssueSeverity.Error, bool InPayload = false)
{
    public override string ToString() =>
        $"{(File is null ? "" : Path.GetFileName(File) + ":")}{Line}:{Column}: {Message}";
}
