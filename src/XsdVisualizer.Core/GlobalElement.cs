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

    /// <summary>Sample só com o que o schema exige.</summary>
    public Sample GenerateMinimal() =>
        Finish(SampleKind.Minimal, 0, new SampleGenerator(GenerationMode.Minimal, new PinnedChoices(null)).Generate(Tree), []);

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
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings,
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
