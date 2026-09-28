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

    /// <summary>Nome do arquivo que declara o elemento (ex.: "enviNFe_v4.00.xsd").</summary>
    public string FileName => Path.GetFileName(SourceFile);

    private SchemaNode? _tree;
    /// <summary>Árvore do elemento; os filhos de cada nó são construídos sob demanda.</summary>
    public SchemaNode Tree => _tree ??= new SchemaTreeBuilder(Unit.Schemas).Build(Declaration);

    /// <summary>Sample com todos os opcionais e, em cada choice, o ramo fixado em <paramref name="pins"/> (caminho do nó → índice) ou o primeiro.</summary>
    public Sample GenerateMaximal(IReadOnlyDictionary<string, int>? pins = null) =>
        Finish(SampleKind.Maximal, 0, new SampleGenerator(GenerationMode.Maximal, new PinnedChoices(pins)).Generate(Tree), []);

    /// <summary>Sample só com o que o Schema Set exige.</summary>
    /// <param name="through">
    /// Nó desta árvore a incluir mesmo sendo opcional: o caminho até ele entra (uma vez) e cada choice no caminho usa
    /// o ramo que leva até ele; o resto continua mínimo.
    /// </param>
    public Sample GenerateMinimal(SchemaNode? through = null) =>
        Finish(SampleKind.Minimal, 0, new SampleGenerator(GenerationMode.Minimal,
            new PinnedChoices(SchemaNodePath.PinsTo(through)), SchemaNodePath.AncestorsAndSelf(through)).Generate(Tree), []);

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
            .Select((g, i) => Finish(SampleKind.Coverage, i + 1,
                SampleComments.Insert(g.Xml, SampleComments.Coverage($"Coverage Set de {Name}: Sample {i + 1} de {generated.Count}", g.Covers)),
                g.Covers))
            .ToList();
    }

    private Sample Finish(SampleKind kind, int number, string xml, IReadOnlyList<string> covers) =>
        new(this, kind, number, xml, covers, Validate(xml), Validate);

    /// <summary>Valida um XML contra a unidade de compilação em que este elemento foi declarado.</summary>
    public IReadOnlyList<ValidationIssue> Validate(string xml) => SchemaValidation.Validate(xml, Unit.Schemas);
}
