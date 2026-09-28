namespace XsdVisualizer.Core;

public enum PairStatus { Added, Removed, Modified, Unchanged }

/// <summary>Confronto entre dois Schema Sets: o Before (referência) e o After (avaliado contra ele).</summary>
public sealed class Comparison
{
    private Comparison(SchemaSet before, SchemaSet after, IReadOnlyList<ElementPair> pairs)
    {
        Before = before;
        After = after;
        Pairs = pairs;
    }

    public SchemaSet Before { get; }
    public SchemaSet After { get; }
    public IReadOnlyList<ElementPair> Pairs { get; }

    /// <summary>
    /// Pareia os Global Elements: primeiro por namespace + nome + arquivo declarante; depois por namespace + nome
    /// quando sobra exatamente um de cada lado. O resto fica sem par (Added ou Removed).
    /// </summary>
    public static Comparison Compare(SchemaSet before, SchemaSet after)
    {
        var pairs = new List<ElementPair>();
        var remainingBefore = before.GlobalElements.ToList();
        var remainingAfter = after.GlobalElements.ToList();

        foreach (var b in before.GlobalElements)
        {
            var a = remainingAfter.FirstOrDefault(x => SameName(b, x) && Path.GetFileName(x.SourceFile) == Path.GetFileName(b.SourceFile));
            if (a is null) continue;
            pairs.Add(new ElementPair(b, a));
            remainingBefore.Remove(b);
            remainingAfter.Remove(a);
        }
        foreach (var b in remainingBefore.ToList())
        {
            var sameBefore = remainingBefore.Where(x => SameName(b, x)).ToList();
            var sameAfter = remainingAfter.Where(x => SameName(b, x)).ToList();
            if (sameBefore.Count != 1 || sameAfter.Count != 1) continue;
            pairs.Add(new ElementPair(b, sameAfter[0]));
            remainingBefore.Remove(b);
            remainingAfter.Remove(sameAfter[0]);
        }
        pairs.AddRange(remainingBefore.Select(b => new ElementPair(b, null)));
        pairs.AddRange(remainingAfter.Select(a => new ElementPair(null, a)));

        return new Comparison(before, after, pairs
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ThenBy(p => (p.Before ?? p.After)!.SourceFile, StringComparer.Ordinal)
            .ToList());
    }

    private static bool SameName(GlobalElement x, GlobalElement y) => x.Name == y.Name && x.Namespace == y.Namespace;
}

/// <summary>Um Global Element do Before e o correspondente do After; um dos lados pode faltar.</summary>
public sealed class ElementPair
{
    private ChangeNode? _tree;

    internal ElementPair(GlobalElement? before, GlobalElement? after)
    {
        Before = before;
        After = after;
    }

    /// <summary>Par escolhido à mão (ex.: um nome que mudou entre as versões).</summary>
    public static ElementPair Manual(GlobalElement before, GlobalElement after) => new(before, after);

    public GlobalElement? Before { get; }
    public GlobalElement? After { get; }
    public string Name => (After ?? Before)!.Name;

    public PairStatus Status =>
        Before is null ? PairStatus.Added
        : After is null ? PairStatus.Removed
        : Tree.ChangeCount > 0 ? PairStatus.Modified
        : PairStatus.Unchanged;

    /// <summary>Árvore de Changes: a união das duas árvores, pareadas pelo caminho de nomes.</summary>
    public ChangeNode Tree => _tree ??= StructuralDiff.Build(Before?.Tree, After?.Tree);

    /// <summary>
    /// Diff linha a linha do Maximal (ou Minimal) do Before e do After, gerados com as mesmas regras:
    /// as diferenças vêm do schema, não do gerador. Um lado que falta é comparado com nada.
    /// </summary>
    public SampleDiff DiffSamples(SampleKind kind)
    {
        string Generate(GlobalElement? element) => element is null ? ""
            : kind == SampleKind.Minimal ? element.GenerateMinimal().Xml : element.GenerateMaximal().Xml;
        return SampleDiffer.Diff(Generate(Before), Generate(After));
    }

    /// <summary>Todas as Changes (sem os nós iguais), em ordem de árvore.</summary>
    public IReadOnlyList<ChangeNode> Changes(bool includeDocumentation = false) =>
        Tree.DescendantsAndSelf().Where(n => n.Kind != ChangeKind.Unchanged
            && (includeDocumentation || n.Kind != ChangeKind.DocumentationOnly)).ToList();
}
