using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;
using CoreComparison = XsdVisualizer.Core.Comparison;

namespace XsdVisualizer.App.ViewModels;

/// <summary>Uma Comparison aberta: Element Pairs, árvore de Changes do par selecionado e o lado a lado dos Samples.</summary>
public sealed partial class ComparisonViewModel : ViewModelBase
{
    private readonly List<ElementPairViewModel> _allPairs = [];

    public ComparisonViewModel(SchemaSet before, SchemaSet after)
    {
        Before = before;
        After = after;
        // O Minimal é mais enxuto de ler e passa pela mudança selecionada; o Maximal fica como opção.
        UseMinimal = true;
    }

    public SchemaSet Before { get; }
    public SchemaSet After { get; }
    public string Title => string.Format(Strings.ComparisonTitle, Before.Name, After.Name);

    public CoreComparison? Model { get; private set; }

    [ObservableProperty] public partial bool IsBusy { get; set; } = true;
    [ObservableProperty] public partial string Status { get; set; } = "";

    /// <summary>Compara fora da thread de interface (pacotes grandes levam alguns segundos).</summary>
    public async Task LoadAsync()
    {
        Status = string.Format(Strings.Comparing, Before.Name, After.Name);
        var model = await Task.Run(() =>
        {
            var comparison = CoreComparison.Compare(Before, After);
            _ = comparison.Pairs.Select(p => p.Status).ToList(); // calcula as árvores aqui, não na interface
            return comparison;
        });
        Model = model;
        _allPairs.AddRange(model.Pairs
            // Só documentação fica junto dos iguais, como antes de o par ter esse status.
            .OrderBy(p => p.Status switch { ChangeKind.Modified => 0, ChangeKind.Added => 1, ChangeKind.Removed => 2, _ => 3 })
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => new ElementPairViewModel(p)));
        IsBusy = false;
        Status = "";
        RefreshPairs();
        SelectedPair = Pairs.FirstOrDefault();
    }

    // ---- Element Pairs ----
    public ObservableCollection<ElementPairViewModel> Pairs { get; } = [];
    [ObservableProperty] public partial bool OnlyChangedPairs { get; set; } = true;
    [ObservableProperty] public partial ElementPairViewModel? SelectedPair { get; set; }

    partial void OnOnlyChangedPairsChanged(bool value) => RefreshPairs();

    private void RefreshPairs()
    {
        var selected = SelectedPair;
        Pairs.Clear();
        // "Só com mudanças" esconde os iguais e, com as mudanças de documentação desligadas, os que só mudaram nela.
        foreach (var pair in _allPairs.Where(p => !OnlyChangedPairs || p == selected
                     || p.Model.Status is not (ChangeKind.Unchanged or ChangeKind.DocumentationOnly)
                     || (ShowDocumentation && p.Model.Status == ChangeKind.DocumentationOnly)))
            Pairs.Add(pair);
    }

    partial void OnSelectedPairChanged(ElementPairViewModel? value)
    {
        (AddedCount, RemovedCount, ModifiedCount) = value is null ? (0, 0, 0) : CountByKind(value.Model.Tree);
        RebuildTree();
        OnPropertyChanged(nameof(HasSelectedPair));
        OnPropertyChanged(nameof(PairCandidates));
        _ = LoadDiffAsync();
    }

    public bool HasSelectedPair => SelectedPair is not null;

    // Resumo do par por tipo de mudança, contado como o ChangeCount: o que entrou ou saiu conta uma vez só.
    [ObservableProperty] public partial int AddedCount { get; private set; }
    [ObservableProperty] public partial int RemovedCount { get; private set; }
    [ObservableProperty] public partial int ModifiedCount { get; private set; }

    private static (int Added, int Removed, int Modified) CountByKind(ChangeNode node)
    {
        var (added, removed, modified) = (0, 0, 0);
        foreach (var child in node.Children)
        {
            if (child.Kind == ChangeKind.Added) { added++; continue; }
            if (child.Kind == ChangeKind.Removed) { removed++; continue; }
            if (child.Kind == ChangeKind.Modified) modified++;
            var (a, r, m) = CountByKind(child);
            (added, removed, modified) = (added + a, removed + r, modified + m);
        }
        return (added, removed, modified);
    }

    /// <summary>Para um par sem After: os Global Elements do After ainda sem par, para parear à mão.</summary>
    public IReadOnlyList<GlobalElementChoice> PairCandidates =>
        SelectedPair?.Model is { Before: not null, After: null }
            ? _allPairs.Where(p => p.Model.Before is null).Select(p => new GlobalElementChoice(p.Model.After!)).ToList()
            : [];

    [ObservableProperty] public partial GlobalElementChoice? ManualPartner { get; set; }

    async partial void OnManualPartnerChanged(GlobalElementChoice? value)
    {
        if (value is null || SelectedPair?.Model.Before is not { } before) return;
        var manual = new ElementPairViewModel(ElementPair.Manual(before, value.Element), isManual: true);
        await Task.Run(() => manual.Model.Tree); // árvore grande (ex.: NF-e) fora da thread de interface
        _allPairs.Insert(0, manual);
        RefreshPairs();
        SelectedPair = manual;
        ManualPartner = null;
    }

    // ---- Árvore de Changes ----
    [ObservableProperty] public partial bool OnlyChanges { get; set; } = true;
    [ObservableProperty] public partial bool ShowDocumentation { get; set; }
    [ObservableProperty] public partial IReadOnlyList<ChangeNodeViewModel> TreeRoots { get; private set; } = [];
    [ObservableProperty] public partial ChangeNodeViewModel? SelectedChange { get; set; }

    /// <summary>Pedido para rolar o lado a lado até um caminho (a view escuta).</summary>
    public event Action<string>? RevealPathRequested;

    partial void OnOnlyChangesChanged(bool value) => RebuildTree();
    partial void OnShowDocumentationChanged(bool value)
    {
        RefreshPairs();
        RebuildTree();
    }

    partial void OnSelectedChangeChanged(ChangeNodeViewModel? value)
    {
        if (value is null) return;
        // A mudança está num ramo que o XML atual não mostra (ex.: segundo ramo de um choice): regera com foco nela.
        if (!InDiff(value.Model)) _ = LoadDiffAsync(value.Model);
        else RevealPathRequested?.Invoke(value.Model.Path);
    }

    private bool InDiff(ChangeNode node)
    {
        if (Diff is not { } diff) return true;
        var lines = node.Kind switch
        {
            ChangeKind.Removed => diff.Before,
            ChangeKind.Added => diff.After,
            _ => diff.Before.Concat(diff.After),
        };
        // Atributos e alternativas xsi:type não têm linha própria: vale o elemento que os contém.
        var path = System.Text.RegularExpressions.Regex.Replace(node.Path, @"\[[^\]]*\]", "");
        if (path.Contains("/@")) path = path[..path.IndexOf("/@", StringComparison.Ordinal)];
        return lines.Any(l => l.Path == path);
    }

    private void RebuildTree()
    {
        SelectedChange = null;
        TreeRoots = SelectedPair is null ? [] : [new ChangeNodeViewModel(SelectedPair.Model.Tree, this, null) { IsExpanded = true }];
    }

    internal bool Shows(ChangeNode node) =>
        (!OnlyChanges || node.Kind != ChangeKind.Unchanged || Counts(node) > 0)
        && (ShowDocumentation || node.Kind != ChangeKind.DocumentationOnly || node.ChangeCount > 0);

    internal int Counts(ChangeNode node) => node.ChangeCount + (ShowDocumentation ? node.DocumentationChangeCount : 0);

    [RelayCommand]
    private void NextChange() => MoveChange(+1);

    [RelayCommand]
    private void PreviousChange() => MoveChange(-1);

    private void MoveChange(int step)
    {
        if (SelectedPair is null) return;
        var changes = SelectedPair.Model.Changes(ShowDocumentation);
        if (changes.Count == 0) return;
        var current = SelectedChange is null ? -1 : changes.ToList().FindIndex(c => c.Path == SelectedChange.Model.Path);
        var next = changes[((current < 0 && step < 0 ? 0 : current) + step + changes.Count) % changes.Count];
        Reveal(next.Path);
    }

    /// <summary>Expande os ancestrais e seleciona o nó com o caminho dado.</summary>
    private void Reveal(string path)
    {
        var node = TreeRoots.FirstOrDefault();
        while (node is not null && node.Model.Path != path)
        {
            node.IsExpanded = true;
            node = node.Children.FirstOrDefault(c => path.StartsWith(c.Model.Path + "/", StringComparison.Ordinal) || c.Model.Path == path);
        }
        if (node is null) return;
        if (SelectedChange is { } previous) previous.IsSelected = false;
        node.IsSelected = true;
        SelectedChange = node;
    }

    internal void OnNodeSelected(ChangeNodeViewModel node)
    {
        if (SelectedChange is { } previous && previous != node) previous.IsSelected = false;
        SelectedChange = node;
    }

    // ---- Lado a lado ----
    [ObservableProperty] public partial bool UseMinimal { get; set; }
    [ObservableProperty] public partial SampleDiff? Diff { get; private set; }
    private int _diffVersion;

    partial void OnUseMinimalChanged(bool value) => _ = LoadDiffAsync(SelectedChange?.Model);

    private async Task LoadDiffAsync(ChangeNode? focus = null)
    {
        var version = ++_diffVersion;
        if (SelectedPair is not { } pair)
        {
            Diff = null;
            return;
        }
        var kind = UseMinimal ? SampleKind.Minimal : SampleKind.Maximal;
        var diff = await Task.Run(() => pair.Model.DiffSamples(kind, focus));
        Dispatcher.UIThread.Post(() =>
        {
            if (version == _diffVersion) Diff = diff;
        });
    }

    public string Summary(bool includeDocumentation) => Model?.ToMarkdown(includeDocumentation) ?? "";
}

public sealed class ElementPairViewModel(ElementPair model, bool isManual = false) : ViewModelBase
{
    public ElementPair Model { get; } = model;
    public bool IsManual { get; } = isManual;
    public string Name => IsManual ? $"{Model.Before!.Name} → {Model.After!.Name}" : Model.Name;
    public string Files => Model switch
    {
        { Before: { } b, After: { } a } when b.FileName != a.FileName => $"{b.FileName} → {a.FileName}",
        _ => (Model.After ?? Model.Before)!.FileName,
    };
    public ChangeKind Status => Model.Status;
    public string StatusText => Status == ChangeKind.Modified
        ? string.Format(Strings.ChangesCount, Model.Tree.ChangeCount)
        : ChangeKindText.Of(Status).ToLowerInvariant(); // a lista de pares usa minúsculas (entrou, saiu, igual)
    /// <summary>Pílula à direita do par: o número de mudanças ou, se entrou/saiu/igual, o tipo.</summary>
    public string BadgeText => Status == ChangeKind.Modified ? Model.Tree.ChangeCount.ToString() : ChangeKindText.Of(Status);
}

public sealed partial class ChangeNodeViewModel : ViewModelBase
{
    private readonly ComparisonViewModel _owner;
    private IReadOnlyList<ChangeNodeViewModel>? _children;

    public ChangeNodeViewModel(ChangeNode model, ComparisonViewModel owner, ChangeNodeViewModel? parent)
    {
        Model = model;
        _owner = owner;
        Parent = parent;
    }

    public ChangeNode Model { get; }
    public ChangeNodeViewModel? Parent { get; }

    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value) _owner.OnNodeSelected(this);
    }

    public IReadOnlyList<ChangeNodeViewModel> Children =>
        _children ??= Model.Children.Where(_owner.Shows).Select(c => new ChangeNodeViewModel(c, _owner, this)).ToList();

    public string Label => Model.Label;
    public ChangeKind Kind => Model.Kind;
    public bool IsAdded => Kind == ChangeKind.Added;
    public bool IsRemoved => Kind == ChangeKind.Removed;
    public bool IsModified => Kind == ChangeKind.Modified;
    public bool IsDocumentationOnly => Kind == ChangeKind.DocumentationOnly;
    public int Count => _owner.Counts(Model);
    /// <summary>Quantas mudanças há dentro; um grupo que entrou ou saiu é uma mudança só, sem contador.</summary>
    public string CountText => Count > 0 && Kind is not (ChangeKind.Added or ChangeKind.Removed) ? string.Format(Strings.ChangesCount, Count) : "";
    public string? TypeName => (Model.After ?? Model.Before)?.TypeName;
    /// <summary>Pílula do tipo de mudança, onde a cor sozinha não basta; um grupo alterado mostra o contador no lugar.</summary>
    public string BadgeText => Kind == ChangeKind.Unchanged || CountText != "" ? "" : KindText;
    public bool HasBadge => BadgeText != "";

    /// <summary>Documentação do campo (a do After; se saiu, a do Before).</summary>
    public string? Documentation => (Model.After ?? Model.Before)?.Documentation;
    public bool HasDocumentation => !string.IsNullOrWhiteSpace(Documentation);
    public string KindText => ChangeKindText.Of(Kind);

    /// <summary>O que mudou, uma caixa por propriedade: antes → depois, ou os valores de enumeração que entraram/saíram.</summary>
    public IReadOnlyList<DifferenceRow> Differences => Model.Differences.Select(d => new DifferenceRow(
        PropertyName(d.Property), d.Before ?? "—", d.After ?? "—",
        d.Property == "enumeration" ? string.Join(", ", d.AddedValues.Select(v => "+" + v)) : null,
        d.Property == "enumeration" ? string.Join(", ", d.RemovedValues.Select(v => "−" + v)) : null)).ToList();

    /// <summary>Definição do campo (tipo, cardinalidade, facets, valor fixo/padrão), propriedade e valor.</summary>
    public IReadOnlyList<DefinitionRow> Definition => Model.Definition.Select(d => new DefinitionRow(PropertyName(d.Property), d.Value)).ToList();
    public bool HasDefinition => Model.Definition.Count > 0;

    private static string PropertyName(string property) => property switch
    {
        "type" => Strings.Type,
        "cardinality" => Strings.Cardinality,
        "fixed" => Strings.Fixed,
        "default" => Strings.Default,
        "documentation" => Strings.Documentation,
        _ => property,
    };
}

/// <summary>Uma propriedade que mudou; numa enumeração, Added/Removed trazem os valores e Before/After não valem.</summary>
public sealed record DifferenceRow(string Name, string Before, string After, string? Added, string? Removed)
{
    public bool IsEnumeration => Added is not null;
    public bool HasAdded => !string.IsNullOrEmpty(Added);
    public bool HasRemoved => !string.IsNullOrEmpty(Removed);
}

public sealed record DefinitionRow(string Name, string Value);
