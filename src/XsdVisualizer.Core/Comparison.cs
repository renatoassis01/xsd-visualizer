namespace XsdVisualizer.Core;

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
            var a = remainingAfter.FirstOrDefault(x => SameName(b, x) && x.FileName == b.FileName);
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

    /// <summary>Resumo para o time: Global Elements novos, removidos e alterados, com as Changes de cada alterado.</summary>
    public string ToMarkdown(bool includeDocumentation = false)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine($"# Comparação: {Before.Name} → {After.Name}").AppendLine();

        void Section(string title, IEnumerable<ElementPair> pairs, Func<ElementPair, GlobalElement> side)
        {
            var list = pairs.ToList();
            if (list.Count == 0) return;
            md.AppendLine($"## {title} ({list.Count})").AppendLine();
            foreach (var pair in list) md.AppendLine($"- {pair.Name} ({side(pair).FileName})");
            md.AppendLine();
        }
        Section("Global Elements que entraram", Pairs.Where(p => p.Status == ChangeKind.Added), p => p.After!);
        Section("Global Elements que saíram", Pairs.Where(p => p.Status == ChangeKind.Removed), p => p.Before!);

        var changed = Pairs.Where(p => p.Before is not null && p.After is not null && p.Changes(includeDocumentation).Count > 0).ToList();
        if (changed.Count > 0)
        {
            md.AppendLine($"## Global Elements com mudanças ({changed.Count})").AppendLine();
            foreach (var pair in changed)
            {
                md.AppendLine($"### {pair.Name} ({pair.After!.FileName})").AppendLine();
                md.AppendLine("| Caminho | Mudança | Detalhes |").AppendLine("|---|---|---|");
                foreach (var change in pair.Changes(includeDocumentation))
                {
                    var details = string.Join("; ", change.Differences
                        .Where(d => includeDocumentation || !d.IsDocumentation)
                        .Select(d => d.Describe()));
                    md.AppendLine(details.Length == 0
                        ? $"| {change.Path} | {change.Kind} | |"
                        : $"| {change.Path} | {change.Kind} | {details.Replace("|", "\\|").Replace("\n", " ")} |");
                }
                md.AppendLine();
            }
        }
        return md.ToString();
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

    /// <summary>Added/Removed sem um dos lados; Modified com qualquer Change além de documentação; DocumentationOnly se só a documentação mudou.</summary>
    public ChangeKind Status =>
        Before is null ? ChangeKind.Added
        : After is null ? ChangeKind.Removed
        : Tree.Kind == ChangeKind.Modified || Tree.ChangeCount > 0 ? ChangeKind.Modified
        : Tree.Kind == ChangeKind.DocumentationOnly || Tree.DocumentationChangeCount > 0 ? ChangeKind.DocumentationOnly
        : ChangeKind.Unchanged;

    /// <summary>Árvore de Changes: a união das duas árvores, pareadas pelo caminho de nomes.</summary>
    public ChangeNode Tree => _tree ??= StructuralDiff.Build(Before?.Tree, After?.Tree);

    /// <summary>
    /// Diff linha a linha do Maximal (ou Minimal) do Before e do After, gerados com as mesmas regras:
    /// as diferenças vêm do schema, não do gerador. Um lado que falta é comparado com nada.
    /// </summary>
    /// <param name="focus">
    /// Mudança a trazer para o XML: cada lado escolhe, em cada choice, o ramo que leva até ela (ou até o ancestral
    /// mais próximo que exista naquele lado), e no Minimal os opcionais desse caminho entram. Sem isso, o exemplo usa
    /// o primeiro ramo (e, no Minimal, omite opcionais) e mudanças fora dele não aparecem.
    /// </param>
    public SampleDiff DiffSamples(SampleKind kind, ChangeNode? focus = null)
    {
        var path = focus is null ? [] : PathTo(focus);
        string Generate(GlobalElement? element, Func<ChangeNode, SchemaNode?> side)
        {
            if (element is null) return "";
            var target = path.Select(side).LastOrDefault(n => n is not null);
            return kind == SampleKind.Minimal
                ? element.GenerateMinimal(through: target).Xml
                : element.GenerateMaximal(SchemaNodePath.PinsTo(target)).Xml;
        }
        return SampleDiffer.Diff(Generate(Before, n => n.Before), Generate(After, n => n.After));
    }

    /// <summary>Da raiz até o nó, na árvore de Changes deste par.</summary>
    private List<ChangeNode> PathTo(ChangeNode focus)
    {
        var path = new List<ChangeNode>();
        bool Find(ChangeNode node)
        {
            path.Add(node);
            if (node == focus || node.Children.Any(Find)) return true;
            path.RemoveAt(path.Count - 1);
            return false;
        }
        Find(Tree);
        return path;
    }

    /// <summary>Todas as Changes (sem os nós iguais), em ordem de árvore.</summary>
    public IReadOnlyList<ChangeNode> Changes(bool includeDocumentation = false) =>
        Tree.ChangesAndSelf().Where(n => includeDocumentation || n.Kind != ChangeKind.DocumentationOnly).ToList();
}
