using System.Xml;
using System.Xml.Schema;

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

internal static class SchemaValidation
{
    public static ValidationIssue Issue(XmlSchemaException e, string? file, XmlSeverityType severity) =>
        new(e.Message, file, e.LineNumber, e.LinePosition,
            severity == XmlSeverityType.Warning ? IssueSeverity.Warning : IssueSeverity.Error);

    public static ValidationIssue Issue(XmlException e, string? file) => new(e.Message, file, e.LineNumber, e.LinePosition);

    /// <summary>Valida um XML contra um conjunto de schemas, incluindo xs:ID, xs:key e xs:unique.</summary>
    public static List<ValidationIssue> Validate(string xml, XmlSchemaSet schemas)
    {
        var issues = new List<ValidationIssue>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas,
            // As flags substituem o padrão: sem ProcessIdentityConstraints, xs:ID/xs:key/xs:unique não são checados.
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
                | XmlSchemaValidationFlags.ProcessIdentityConstraints
                | XmlSchemaValidationFlags.AllowXmlAttributes,
        };
        settings.ValidationEventHandler += (_, e) => issues.Add(Issue(e.Exception, null, e.Severity));
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
        }
        catch (XmlException e)
        {
            issues.Add(Issue(e, null));
        }
        return issues;
    }
}
