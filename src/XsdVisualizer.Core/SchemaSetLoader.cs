using System.Xml;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

/// <summary>Abre pastas de XSD como Schema Sets.</summary>
/// <param name="downloader">Usado para schemas remotos não embutidos e fora do cache (padrão: HTTP).</param>
/// <param name="cacheDirectory">Onde schemas baixados ficam guardados (padrão: pasta local do usuário).</param>
public sealed class SchemaSetLoader(ISchemaDownloader? downloader = null, string? cacheDirectory = null)
{
    private readonly ISchemaDownloader _downloader = downloader ?? new HttpSchemaDownloader();
    private readonly string _cacheDirectory = cacheDirectory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XsdVisualizer", "schema-cache");

    private static readonly HashSet<string> Xsd11Constructs =
        ["assert", "assertion", "alternative", "openContent", "defaultOpenContent", "override"];

    /// <summary>Abre a pasta indicada, ou a pasta do arquivo .xsd indicado.</summary>
    public SchemaSet Open(string path)
    {
        var folder = FolderOf(path);
        // O que é WSDL e o que é XSD vem do elemento raiz: um WSDL salvo como .xsd (ou o contrário) é comum.
        var candidates = Directory.GetFiles(folder, "*.xsd").Concat(Directory.GetFiles(folder, "*.wsdl"))
            .Order(StringComparer.Ordinal).ToList();
        var wsdls = candidates.Where(IsWsdl).ToList();
        var files = candidates.Except(wsdls).ToList();
        var issues = new List<ValidationIssue>();
        var unreadable = new HashSet<string>(StringComparer.Ordinal);
        var missing = new List<(string File, ValidationIssue Issue)>();
        var referenced = files.SelectMany(f => ReferencedFiles(f, issues, unreadable, missing)).ToHashSet(StringComparer.Ordinal);
        var units = files
            .Where(f => !referenced.Contains(f) && !unreadable.Contains(f))
            .Select(f => Compile(f, issues, _downloader, _cacheDirectory))
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

        var services = wsdls
            .SelectMany(f => new WsdlReader(f, Resolver(f, issues), issues).Read())
            .ToList();

        // O aviso genérico do .NET ("Cannot resolve the 'schemaLocation' attribute") repete, sem o nome, o que a
        // Validation Issue de arquivo ausente já diz na mesma linha.
        var missingAt = missing.Select(m => (m.Issue.File, m.Issue.Line, m.Issue.Column)).ToHashSet();
        issues.RemoveAll(i => i.Severity == IssueSeverity.Warning && missingAt.Contains((i.File, i.Line, i.Column)));
        return new SchemaSet(folder, elements, issues.Distinct().ToList(), services,
            missing.Select(m => Path.GetFileName(m.File)).Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>Se o elemento raiz é de WSDL; sem conseguir ler, vale a extensão.</summary>
    private static bool IsWsdl(string file)
    {
        try
        {
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            reader.MoveToContent();
            return reader.NamespaceURI == Soap.Wsdl11 || reader.NamespaceURI == Soap.Wsdl20;
        }
        catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
        {
            return file.EndsWith(".wsdl", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>A pasta de um caminho (pasta ou arquivo .xsd), absoluta e sem separador no final.</summary>
    public static string FolderOf(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(File.Exists(path) ? Path.GetDirectoryName(path)! : path));

    /// <summary>
    /// Arquivos locais que o arquivo inclui/importa; os que não existem vão para <paramref name="missing"/> e viram
    /// uma Validation Issue que diz o nome do arquivo (sem isso só aparecem os tipos não declarados).
    /// </summary>
    private static List<string> ReferencedFiles(string file, List<ValidationIssue> issues, HashSet<string> unreadable,
        List<(string File, ValidationIssue Issue)> missing)
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
                var referenced = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, location));
                result.Add(referenced);
                if (File.Exists(referenced)) continue;
                var at = (IXmlLineInfo)reader;
                var issue = new ValidationIssue(
                    $"Arquivo não encontrado na pasta: {location} (referenciado por {Path.GetFileName(file)})",
                    file, at.LineNumber, at.LinePosition);
                missing.Add((referenced, issue));
                issues.Add(issue);
            }
        }
        catch (XmlException e)
        {
            issues.Add(SchemaValidation.Issue(e, file));
            unreadable.Add(file);
        }
        return result;
    }

    /// <summary>Compila um arquivo raiz; devolve null (e registra os problemas) se a compilação falhar.</summary>
    private static CompilationUnit? Compile(string file, List<ValidationIssue> issues, ISchemaDownloader downloader, string cacheDirectory)
    {
        var failed = false;
        var schemas = new XmlSchemaSet { XmlResolver = new SchemaResolver(downloader, cacheDirectory, uri => issues.Add(new ValidationIssue(
            $"Não foi possível obter o schema remoto {uri} (sem conexão e sem cópia em cache).", file, 0, 0))) };
        schemas.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error) failed = true;
            issues.Add(SchemaValidation.Issue(e.Exception, SourceFile(e.Exception.SourceUri) ?? file, e.Severity));
        };
        try
        {
            using (var reader = XmlReader.Create(file))
                schemas.Add(null, reader);
            schemas.Compile();
        }
        catch (XmlException e)
        {
            issues.Add(SchemaValidation.Issue(e, SourceFile(e.SourceUri) ?? file));
            return null;
        }
        catch (XmlSchemaException e)
        {
            issues.Add(SchemaValidation.Issue(e, SourceFile(e.SourceUri) ?? file, XmlSeverityType.Error));
            return null;
        }
        return failed ? null : new CompilationUnit(file, schemas);
    }

    private SchemaResolver Resolver(string file, List<ValidationIssue> issues) =>
        new(_downloader, _cacheDirectory, uri => issues.Add(new ValidationIssue(
            $"Não foi possível obter o schema remoto {uri} (sem conexão e sem cópia em cache).", file, 0, 0)));

    private static string? SourceFile(string? uri) =>
        string.IsNullOrEmpty(uri) ? null : new Uri(uri).LocalPath;
}

internal sealed record CompilationUnit(string RootFile, XmlSchemaSet Schemas);
