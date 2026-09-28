using System.Xml;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

/// <summary>Elemento de topo de um Schema Set, identificado pelo nome e pelo arquivo que o declara.</summary>
public sealed class GlobalElement
{
    internal GlobalElement(XmlSchemaElement declaration, CompilationUnit unit)
    {
        Declaration = declaration;
        Unit = unit;
    }

    internal XmlSchemaElement Declaration { get; }
    internal CompilationUnit Unit { get; }

    public SchemaSet SchemaSet { get; internal set; } = null!;
    public string Name => Declaration.QualifiedName.Name;
    public string Namespace => Declaration.QualifiedName.Namespace;

    /// <summary>Arquivo XSD que declara o elemento.</summary>
    public string SourceFile => new Uri(Declaration.SourceUri!).LocalPath;

    private SchemaNode? _tree;
    /// <summary>Árvore do elemento; os filhos de cada nó são construídos sob demanda.</summary>
    public SchemaNode Tree => _tree ??= new SchemaTreeBuilder(Unit.Schemas).Build(Declaration);

    /// <summary>Sample com todos os opcionais e, em cada choice, o ramo fixado em <paramref name="pins"/> (caminho do nó → índice) ou o primeiro.</summary>
    public Sample GenerateMaximal(IReadOnlyDictionary<string, int>? pins = null) =>
        Finish(SampleKind.Maximal, 0, new SampleGenerator(GenerationMode.Maximal, new PinnedChoices(pins)).Generate(Tree), []);

    /// <summary>Sample só com o que o Schema Set exige.</summary>
    public Sample GenerateMinimal() =>
        Finish(SampleKind.Minimal, 0, new SampleGenerator(GenerationMode.Minimal, new PinnedChoices(null)).Generate(Tree), []);

    /// <summary>
    /// Menor conjunto (guloso) de Samples em que cada ramo de choice, opcional e valor de enumeração
    /// aparece ao menos uma vez. Cada Sample traz no topo um comentário com o que cobre.
    /// </summary>
    public IReadOnlyList<Sample> GenerateCoverageSet()
    {
        const int limit = 1000;
        var choices = new CoverageChoices(Tree);
        var generated = new List<(string Xml, IReadOnlyList<string> Covers)>();
        do
        {
            var xml = new SampleGenerator(GenerationMode.Maximal, choices).Generate(Tree);
            var covers = choices.NextSample();
            if (generated.Count > 0 && covers.Count == 0) break;
            generated.Add((xml, covers));
        } while (choices.HasUncovered && generated.Count < limit);

        return generated
            .Select((g, i) => Finish(SampleKind.Coverage, i + 1, WithComment(g.Xml, CoverageComment(i + 1, generated.Count, g.Covers)), g.Covers))
            .ToList();
    }

    private string CoverageComment(int number, int total, IReadOnlyList<string> covers)
    {
        var lines = new List<string> { $" Coverage Set de {Name}: Sample {number} de {total}" };
        if (covers.Count > 0)
        {
            lines.Add(" cobre:");
            lines.AddRange(covers.Select(c => "   " + c));
        }
        return string.Join("\n", lines).Replace("--", "- -") + "\n";
    }

    private static string WithComment(string xml, string comment)
    {
        var endOfDeclaration = xml.IndexOf("?>", StringComparison.Ordinal) + 2;
        return $"{xml[..endOfDeclaration]}\n<!--{comment}-->{xml[endOfDeclaration..]}";
    }

    private Sample Finish(SampleKind kind, int number, string xml, IReadOnlyList<string> covers) =>
        new(this, kind, number, xml, covers, Validate(xml));

    /// <summary>Valida um XML contra a unidade de compilação em que este elemento foi declarado.</summary>
    public IReadOnlyList<ValidationIssue> Validate(string xml)
    {
        var issues = new List<ValidationIssue>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = Unit.Schemas,
            // As flags substituem o padrão: sem ProcessIdentityConstraints, xs:ID/xs:key/xs:unique não são checados.
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
                | XmlSchemaValidationFlags.ProcessIdentityConstraints
                | XmlSchemaValidationFlags.AllowXmlAttributes,
        };
        settings.ValidationEventHandler += (_, e) => issues.Add(new ValidationIssue(
            e.Message, null, e.Exception.LineNumber, e.Exception.LinePosition,
            e.Severity == XmlSeverityType.Warning ? IssueSeverity.Warning : IssueSeverity.Error));
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
        }
        catch (XmlException e)
        {
            issues.Add(new ValidationIssue(e.Message, null, e.LineNumber, e.LinePosition));
        }
        return issues;
    }
}
