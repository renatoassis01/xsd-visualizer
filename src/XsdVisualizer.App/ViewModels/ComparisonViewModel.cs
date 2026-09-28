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
            .OrderBy(p => p.Status switch { PairStatus.Modified => 0, PairStatus.Added => 1, PairStatus.Removed => 2, _ => 3 })
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
        foreach (var pair in _allPairs.Where(p => !OnlyChangedPairs || p.Model.Status != PairStatus.Unchanged || p == selected))
            Pairs.Add(pair);
    }

    partial void OnSelectedPairChanged(ElementPairViewModel? value)
    {
        RebuildTree();
        OnPropertyChanged(nameof(HasSelectedPair));
        OnPropertyChanged(nameof(PairCandidates));
        _ = LoadDiffAsync();
    }

    public bool HasSelectedPair => SelectedPair is not null;

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
    partial void OnShowDocumentationChanged(bool value) => RebuildTree();

    partial void OnSelectedChangeChanged(ChangeNodeViewModel? value)
    {
        if (value is not null) RevealPathRequested?.Invoke(value.Model.Path);
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

    partial void OnUseMinimalChanged(bool value) => _ = LoadDiffAsync();

    private async Task LoadDiffAsync()
    {
        var version = ++_diffVersion;
        if (SelectedPair is not { } pair)
        {
            Diff = null;
            return;
        }
        var kind = UseMinimal ? SampleKind.Minimal : SampleKind.Maximal;
        var diff = await Task.Run(() => pair.Model.DiffSamples(kind));
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
        { Before: { } b, After: { } a } when Path.GetFileName(b.SourceFile) != Path.GetFileName(a.SourceFile) =>
            $"{Path.GetFileName(b.SourceFile)} → {Path.GetFileName(a.SourceFile)}",
        _ => Path.GetFileName((Model.After ?? Model.Before)!.SourceFile),
    };
    public PairStatus Status => Model.Status;
    public string StatusText => Status switch
    {
        PairStatus.Added => Strings.StatusAdded,
        PairStatus.Removed => Strings.StatusRemoved,
        PairStatus.Modified => string.Format(Strings.ChangesCount, Model.Tree.ChangeCount),
        _ => Strings.StatusUnchanged,
    };
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
    public string KindText => Kind switch
    {
        ChangeKind.Added => Strings.KindAdded,
        ChangeKind.Removed => Strings.KindRemoved,
        ChangeKind.Modified => Strings.KindModified,
        ChangeKind.DocumentationOnly => Strings.KindDocumentation,
        _ => Strings.KindUnchanged,
    };

    /// <summary>Linhas "propriedade: antes → depois" (e valores de enumeração que entraram/saíram).</summary>
    public IReadOnlyList<string> DifferenceLines => Model.Differences.Select(d => d.Property switch
    {
        "enumeration" => $"enumeration: {string.Join(", ", d.AddedValues.Select(v => "+" + v).Concat(d.RemovedValues.Select(v => "−" + v)))}",
        _ => $"{PropertyName(d.Property)}: {d.Before ?? "—"}  →  {d.After ?? "—"}",
    }).ToList();

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
