namespace XsdVisualizer.Core;

public enum ChangeKind { Unchanged, Added, Removed, Modified, DocumentationOnly }

/// <summary>
/// Uma propriedade que mudou num nó (Tipo, Cardinalidade, um facet, Valor fixo…): antes → depois e,
/// para enumerações, os valores que entraram e saíram.
/// </summary>
public sealed record PropertyChange(string Property, string? Before, string? After,
    IReadOnlyList<string> AddedValues, IReadOnlyList<string> RemovedValues)
{
    public bool IsDocumentation => Property == "documentation";
}

/// <summary>Nó da árvore de Changes: um elemento/atributo presente no Before, no After ou nos dois.</summary>
public sealed class ChangeNode
{
    internal ChangeNode(string label, string path, SchemaNode? before, SchemaNode? after, ChangeKind kind,
        IReadOnlyList<PropertyChange> differences, IReadOnlyList<ChangeNode> children)
    {
        Label = label;
        Path = path;
        Before = before;
        After = after;
        Kind = kind;
        Differences = differences;
        Children = children;
        // Um grupo que entrou ou saiu conta como uma mudança só: o conteúdo dele vem junto.
        ChangeCount = children.Sum(c => c.Kind is ChangeKind.Added or ChangeKind.Removed ? 1
            : c.ChangeCount + (c.Kind == ChangeKind.Modified ? 1 : 0));
        DocumentationChangeCount = children.Sum(c => c.Kind is ChangeKind.Added or ChangeKind.Removed ? 0
            : c.DocumentationChangeCount + (c.Kind == ChangeKind.DocumentationOnly ? 1 : 0));
    }

    public string Label { get; }
    /// <summary>Caminho de nomes (ex.: "pedido/itens/item/@codigo"); igual nos dois lados.</summary>
    public string Path { get; }
    public SchemaNode? Before { get; }
    public SchemaNode? After { get; }
    public ChangeKind Kind { get; }
    public IReadOnlyList<PropertyChange> Differences { get; }
    public IReadOnlyList<ChangeNode> Children { get; }
    /// <summary>Changes (sem contar só documentação) nos descendentes.</summary>
    public int ChangeCount { get; }
    public int DocumentationChangeCount { get; }

    /// <summary>Changes em ordem de árvore, sem descer em grupos que entraram ou saíram inteiros.</summary>
    internal IEnumerable<ChangeNode> ChangesAndSelf()
    {
        if (Kind != ChangeKind.Unchanged) yield return this;
        if (Kind is ChangeKind.Added or ChangeKind.Removed) yield break;
        foreach (var child in Children)
            foreach (var node in child.ChangesAndSelf())
                yield return node;
    }

    public IEnumerable<ChangeNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var node in child.DescendantsAndSelf())
                yield return node;
    }

    public override string ToString() => $"{Path} {Kind}";
}

/// <summary>Compara duas árvores de Global Element pelo caminho de nomes (compositores não entram no caminho).</summary>
internal static class StructuralDiff
{
    public static ChangeNode Build(SchemaNode? before, SchemaNode? after)
    {
        var any = (after ?? before)!;
        return Node(Key(any), Key(any), before, after);
    }

    private static ChangeNode Node(string label, string path, SchemaNode? before, SchemaNode? after)
    {
        var beforeChildren = before is null || before.IsRecursive ? [] : NamedChildren(before);
        var afterChildren = after is null || after.IsRecursive ? [] : NamedChildren(after);
        foreach (var (_, named) in beforeChildren.Concat(afterChildren)) Remember(named);
        var children = Merge(beforeChildren, afterChildren)
            .Select(k => Node(k.Key, $"{path}/{k.Key}", k.Before?.Node, k.After?.Node))
            .ToList();

        IReadOnlyList<PropertyChange> differences = [];
        ChangeKind kind;
        if (before is null) kind = ChangeKind.Added;
        else if (after is null) kind = ChangeKind.Removed;
        else
        {
            differences = Compare(before, after);
            kind = differences.Any(d => !d.IsDocumentation) ? ChangeKind.Modified
                : differences.Count > 0 ? ChangeKind.DocumentationOnly
                : ChangeKind.Unchanged;
        }
        return new ChangeNode(label, path, before, after, kind, differences, children);
    }

    /// <summary>Filho nomeado com a cardinalidade efetiva (somando a dos compositores entre ele e o pai).</summary>
    private sealed record Named(SchemaNode Node, int Min, int? Max);

    private static List<(string Key, Named Named)> NamedChildren(SchemaNode parent)
    {
        var result = new List<(string, Named)>();
        void Walk(SchemaNode node, int min, int? max)
        {
            foreach (var child in node.Children)
            {
                var childMin = min * child.MinOccurs;
                int? childMax = max is null || child.MaxOccurs is null ? null : max * child.MaxOccurs;
                switch (child.Kind)
                {
                    case NodeKind.Element or NodeKind.Attribute or NodeKind.AnyElement or NodeKind.AnyAttribute:
                        result.Add((Key(child), new Named(child, childMin, childMax)));
                        break;
                    case NodeKind.Choice when child.Children.Count > 1:
                        // Num choice com alternativas, cada ramo é efetivamente opcional.
                        Walk(child, 0, childMax);
                        break;
                    default:
                        Walk(child, childMin, childMax);
                        break;
                }
            }
        }
        Walk(parent, 1, 1);
        return result.GroupBy(r => r.Item1).Select(g => g.First()).ToList();
    }

    private static string Key(SchemaNode node) => node.XsiType is { } t ? $"{node.Label}[{t}]" : node.Label;

    /// <summary>União ordenada: a ordem do After, com os que saíram inseridos após o vizinho anterior do Before.</summary>
    private static List<(string Key, Named? Before, Named? After)> Merge(
        List<(string Key, Named Named)> before, List<(string Key, Named Named)> after)
    {
        var merged = after.Select(a => (a.Key, Before: (Named?)before.FirstOrDefault(b => b.Key == a.Key).Named, After: (Named?)a.Named)).ToList();
        for (var i = 0; i < before.Count; i++)
        {
            if (after.Any(a => a.Key == before[i].Key)) continue;
            var previous = i == 0 ? -1 : merged.FindIndex(m => m.Key == before[i - 1].Key);
            merged.Insert(previous + 1, (before[i].Key, before[i].Named, null));
        }
        return merged;
    }

    private static List<PropertyChange> Compare(SchemaNode before, SchemaNode after)
    {
        var result = new List<PropertyChange>();
        void Add(string property, string? b, string? a)
        {
            if (b != a) result.Add(new PropertyChange(property, b, a, [], []));
        }

        Add("type", before.XsiType ?? before.TypeName, after.XsiType ?? after.TypeName);
        Add("cardinality", EffectiveCardinality(before), EffectiveCardinality(after));
        Add("fixed", before.FixedValue, after.FixedValue);
        Add("default", before.DefaultValue, after.DefaultValue);

        var beforeEnums = before.Facets.Where(f => f.Kind == "enumeration").Select(f => f.Value).ToList();
        var afterEnums = after.Facets.Where(f => f.Kind == "enumeration").Select(f => f.Value).ToList();
        var added = afterEnums.Except(beforeEnums).ToList();
        var removed = beforeEnums.Except(afterEnums).ToList();
        if (added.Count > 0 || removed.Count > 0)
            result.Add(new PropertyChange("enumeration", null, null, added, removed));

        foreach (var kind in before.Facets.Concat(after.Facets).Select(f => f.Kind).Where(k => k != "enumeration").Distinct())
        {
            var (b, a) = (FacetValue(before, kind), FacetValue(after, kind));
            // Mesma regra escrita de outro jeito ("*" e "{0,}") não é mudança.
            if (kind == "pattern" && b is not null && a is not null && NormalizePattern(b) == NormalizePattern(a)) continue;
            Add(kind, b, a);
        }

        Add("documentation", before.Documentation, after.Documentation);
        return result;
    }

    // A cardinalidade efetiva é calculada pelo pai (Named) e anotada aqui por nó.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SchemaNode, string> Cardinalities = new();

    private static string EffectiveCardinality(SchemaNode node) =>
        Cardinalities.TryGetValue(node, out var value) ? value : node.Cardinality;

    private static readonly (System.Text.RegularExpressions.Regex Pattern, string Replacement)[] EquivalentQuantifiers =
    [
        (new(@"\{0,\}"), "*"),
        (new(@"\{1,\}"), "+"),
        (new(@"\{0,1\}"), "?"),
        (new(@"\{(\d+),\1\}"), "{$1}"),
        (new(@"\{1\}"), ""),
    ];

    /// <summary>Forma canônica dos quantificadores de uma regex XSD, para comparar regras e não grafias.</summary>
    internal static string NormalizePattern(string pattern) =>
        EquivalentQuantifiers.Aggregate(pattern, (p, q) => q.Pattern.Replace(p, q.Replacement));

    private static string? FacetValue(SchemaNode node, string kind)
    {
        var values = node.Facets.Where(f => f.Kind == kind).Select(f => f.Value).ToList();
        return values.Count == 0 ? null : string.Join(" | ", values);
    }

    private static void Remember(Named named) =>
        Cardinalities.AddOrUpdate(named.Node, $"{named.Min}..{named.Max?.ToString() ?? "n"}");
}
