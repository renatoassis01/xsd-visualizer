using System.Xml;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

/// <summary>Abre pastas de XSD como Schema Sets.</summary>
public sealed class SchemaSetLoader
{
    private static readonly HashSet<string> Xsd11Constructs =
        ["assert", "assertion", "alternative", "openContent", "defaultOpenContent", "override"];

    /// <summary>Abre a pasta indicada, ou a pasta do arquivo .xsd indicado.</summary>
    public SchemaSet Open(string path)
    {
        var folder = Path.GetFullPath(File.Exists(path) ? Path.GetDirectoryName(path)! : path);
        var files = Directory.GetFiles(folder, "*.xsd").Order(StringComparer.Ordinal).ToList();
        var issues = new List<ValidationIssue>();
        var unreadable = new HashSet<string>(StringComparer.Ordinal);
        var referenced = files.SelectMany(f => ReferencedFiles(f, issues, unreadable)).ToHashSet(StringComparer.Ordinal);
        var units = files
            .Where(f => !referenced.Contains(f) && !unreadable.Contains(f))
            .Select(f => Compile(f, issues))
            .OfType<CompilationUnit>()
            .ToList();

        // O mesmo elemento chega a várias unidades via include; fica uma entrada por arquivo declarante,
        // preferindo a unidade cuja raiz é o próprio arquivo declarante.
        var elements = units
            .SelectMany(u => u.Schemas.GlobalElements.Values.Cast<XmlSchemaElement>().Select(e => new GlobalElement(e, u)))
            .GroupBy(e => (e.Name, e.Namespace, e.SourceFile))
            .Select(g => g.FirstOrDefault(e => e.Unit.RootFile == e.SourceFile) ?? g.First())
            .OrderBy(e => e.Name, StringComparer.Ordinal).ThenBy(e => e.SourceFile, StringComparer.Ordinal)
            .ToList();

        return new SchemaSet(folder, elements, issues.Distinct().ToList());
    }

    private static List<string> ReferencedFiles(string file, List<ValidationIssue> issues, HashSet<string> unreadable)
    {
        var result = new List<string>();
        try
        {
            using var reader = XmlReader.Create(file);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != XmlSchema.Namespace) continue;
                if (Xsd11Constructs.Contains(reader.LocalName))
                {
                    var line = (IXmlLineInfo)reader;
                    issues.Add(new ValidationIssue(
                        $"Construção XSD 1.1 não suportada: xs:{reader.LocalName}", file, line.LineNumber, line.LinePosition));
                }
                if (reader.LocalName is not ("include" or "import" or "redefine")) continue;
                var location = reader.GetAttribute("schemaLocation");
                if (location is null || Uri.TryCreate(location, UriKind.Absolute, out var abs) && !abs.IsFile) continue;
                result.Add(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, location)));
            }
        }
        catch (XmlException e)
        {
            issues.Add(new ValidationIssue(e.Message, file, e.LineNumber, e.LinePosition));
            unreadable.Add(file);
        }
        return result;
    }

    /// <summary>Compila um arquivo raiz; devolve null (e registra os problemas) se a compilação falhar.</summary>
    private static CompilationUnit? Compile(string file, List<ValidationIssue> issues)
    {
        var failed = false;
        var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemas.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) failed = true;
            issues.Add(ToIssue(e.Exception, file, e.Severity));
        };
        try
        {
            using (var reader = XmlReader.Create(file))
                schemas.Add(null, reader);
            schemas.Compile();
        }
        catch (XmlException e)
        {
            issues.Add(new ValidationIssue(e.Message, SourceFile(e.SourceUri) ?? file, e.LineNumber, e.LinePosition));
            return null;
        }
        catch (XmlSchemaException e)
        {
            issues.Add(ToIssue(e, file, XmlSeverityType.Error));
            return null;
        }
        return failed ? null : new CompilationUnit(file, schemas);
    }

    private static ValidationIssue ToIssue(XmlSchemaException e, string fallbackFile, XmlSeverityType severity) =>
        new(e.Message, SourceFile(e.SourceUri) ?? fallbackFile, e.LineNumber, e.LinePosition,
            severity == XmlSeverityType.Warning ? IssueSeverity.Warning : IssueSeverity.Error);

    private static string? SourceFile(string? uri) =>
        string.IsNullOrEmpty(uri) ? null : new Uri(uri).LocalPath;
}

internal sealed record CompilationUnit(string RootFile, XmlSchemaSet Schemas);
