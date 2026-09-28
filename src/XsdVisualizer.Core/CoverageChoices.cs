namespace XsdVisualizer.Core;

/// <summary>
/// Escolhas gulosas para o Coverage Set (ADR-0001): em cada choice, o primeiro ramo ainda não coberto
/// (ou um ramo cuja subárvore ainda tem algo a cobrir); em cada enumeração, o primeiro valor não coberto.
/// Os itens são marcados como cobertos no momento em que são usados, então repetições de um mesmo nó
/// já cobrem alternativas diferentes dentro do mesmo Sample.
/// </summary>
internal sealed class CoverageChoices : IGenerationChoices
{
    private readonly Dictionary<string, string> _universe = new(); // chave → rótulo
    private readonly Dictionary<string, IReadOnlyList<string>> _subtreeItems = new();
    private readonly HashSet<string> _covered = [];
    private List<string> _newlyCovered = [];

    public CoverageChoices(SchemaNode root) => Collect(root);

    public bool HasUncovered => _universe.Keys.Any(k => !_covered.Contains(k));

    /// <summary>Começa um novo Sample; devolve os rótulos cobertos pela primeira vez no Sample anterior.</summary>
    public IReadOnlyList<string> NextSample()
    {
        var previous = _newlyCovered;
        _newlyCovered = [];
        return previous;
    }

    public int PickBranch(SchemaNode choice)
    {
        var branches = choice.Children;
        var index = Enumerable.Range(0, branches.Count).FirstOrDefault(i => !IsCovered(BranchKey(choice, i)), -1);
        if (index < 0)
            index = Enumerable.Range(0, branches.Count)
                .FirstOrDefault(i => SubtreeItems(branches[i]).Any(k => !IsCovered(k)), 0);
        Cover(BranchKey(choice, index));
        return index;
    }

    public string? PickEnumeration(SchemaNode node, IReadOnlyList<string> values)
    {
        var value = values.FirstOrDefault(v => !IsCovered(EnumerationKey(node, v))) ?? values[0];
        Cover(EnumerationKey(node, value));
        return value;
    }

    private bool IsCovered(string key) => !_universe.ContainsKey(key) || _covered.Contains(key);

    private void Cover(string key)
    {
        if (_universe.TryGetValue(key, out var label) && _covered.Add(key))
            _newlyCovered.Add(label);
    }

    private static string BranchKey(SchemaNode choice, int index) => $"{choice.Path}#{index}";
    private static string EnumerationKey(SchemaNode node, string value) => $"{node.Path}={value}";

    /// <summary>Registra os itens a cobrir; não entra em nós recursivos (lá o Sample só tem o mínimo).</summary>
    private void Collect(SchemaNode node)
    {
        if (node.Kind == NodeKind.Choice)
            for (var i = 0; i < node.Children.Count; i++)
                _universe[BranchKey(node, i)] = $"{ElementPath(node.Parent)}: {BranchLabel(node.Children[i])}";

        if (node.SimpleType is not null && node.FixedValue is null)
            foreach (var value in ValueGenerator.EnumerationsOf(node.SimpleType))
                _universe[EnumerationKey(node, value)] = $"{ElementPath(node)} = {value}";

        if (node.IsRecursive) return;
        foreach (var child in node.Children) Collect(child);
    }

    private IReadOnlyList<string> SubtreeItems(SchemaNode node)
    {
        if (_subtreeItems.TryGetValue(node.Path, out var cached)) return cached;
        var items = new List<string>();
        if (node.Kind == NodeKind.Choice)
            items.AddRange(Enumerable.Range(0, node.Children.Count).Select(i => BranchKey(node, i)));
        items.AddRange(_universe.Keys.Where(k => k.StartsWith(node.Path + "=", StringComparison.Ordinal)));
        if (!node.IsRecursive)
            foreach (var child in node.Children) items.AddRange(SubtreeItems(child));
        return _subtreeItems[node.Path] = items;
    }

    private static string ElementPath(SchemaNode? node)
    {
        var names = new List<string>();
        for (var n = node; n is not null; n = n.Parent)
            if (n.Kind is NodeKind.Element or NodeKind.Attribute)
                names.Add(n.Label);
        names.Reverse();
        return string.Join("/", names);
    }

    private static string BranchLabel(SchemaNode branch) => branch.Kind switch
    {
        NodeKind.Element when branch.XsiType is not null => $"{branch.Label} (xsi:type {branch.XsiType})",
        NodeKind.Element => branch.Label,
        NodeKind.Sequence or NodeKind.All or NodeKind.Choice =>
            $"{branch.Label}({string.Join(", ", branch.Children.Select(BranchLabel))})",
        _ => branch.Label,
    };
}
