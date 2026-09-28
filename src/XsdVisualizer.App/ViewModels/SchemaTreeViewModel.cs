using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

/// <summary>
/// O que está selecionado na árvore da esquerda (um Global Element ou uma Operation), a árvore da coluna do meio,
/// o nó selecionado e a pesquisa na árvore.
/// </summary>
public sealed partial class SchemaTreeViewModel : ViewModelBase
{
    private const int MaxSearchResults = 300;

    /// <summary>Global Element ou Operation selecionado mudou (os botões de gerar dependem disso).</summary>
    public event Action? SelectionChanged;

    /// <summary>Uma alternativa foi fixada na árvore de um Global Element (regera o Maximal).</summary>
    public event Action<GlobalElementViewModel>? ElementPinsChanged;

    /// <summary>Uma alternativa foi fixada na árvore do Payload de uma Operation (regera o Envelope Maximal).</summary>
    public event Action<OperationViewModel>? EnvelopePinsChanged;

    [ObservableProperty] public partial object? SelectedSchemaItem { get; set; }
    [ObservableProperty] public partial GlobalElementViewModel? SelectedElement { get; set; }
    /// <summary>Schema Set selecionado na árvore (a coluna do meio mostra as Validation Issues da carga).</summary>
    [ObservableProperty] public partial SchemaSetViewModel? SelectedSchemaSet { get; set; }
    [ObservableProperty] public partial OperationViewModel? SelectedOperation { get; set; }
    [ObservableProperty] public partial IReadOnlyList<SchemaNodeViewModel> TreeRoots { get; set; } = [];
    [ObservableProperty] public partial SchemaNodeViewModel? SelectedNode { get; set; }

    public bool HasSelectedElement => SelectedElement is not null;
    public bool HasSelectedOperation => SelectedOperation is not null;
    public bool ShowElementPanel => SelectedOperation is null;
    public bool ShowSelectElementHint => SelectedElement is null && SelectedSchemaSet is not { HasLoadIssues: true };

    partial void OnSelectedSchemaItemChanged(object? value)
    {
        switch (value)
        {
            case GlobalElementViewModel element:
                SelectedOperation = null;
                SelectedSchemaSet = null;
                SelectedElement = element;
                break;
            case OperationViewModel operation:
                SelectedElement = null;
                SelectedSchemaSet = null;
                SelectedOperation = operation;
                break;
            case SchemaSetViewModel set:
                SelectedElement = null;
                SelectedOperation = null;
                SelectedSchemaSet = set;
                break;
        }
    }

    partial void OnSelectedSchemaSetChanged(SchemaSetViewModel? value) => OnPropertyChanged(nameof(ShowSelectElementHint));

    partial void OnSelectedElementChanged(GlobalElementViewModel? oldValue, GlobalElementViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.NodeSelected -= OnNodeSelected;
            oldValue.PinsChanged -= OnElementPinsChanged;
        }
        if (newValue is not null)
        {
            newValue.NodeSelected += OnNodeSelected;
            newValue.PinsChanged += OnElementPinsChanged;
        }
        TreeRoots = newValue is null ? [] : [new SchemaNodeViewModel(newValue.Model.Tree, newValue, null) { IsExpanded = true }];
        SelectedNode = null;
        if (TreeRoots.FirstOrDefault() is { } root) root.IsSelected = true;
        SearchText = "";
        OnPropertyChanged(nameof(HasSelectedElement));
        OnPropertyChanged(nameof(ShowSelectElementHint));
        SelectionChanged?.Invoke();
    }

    partial void OnSelectedOperationChanged(OperationViewModel? oldValue, OperationViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PinsChanged -= OnEnvelopePinsChanged;
            oldValue.NodeSelected -= OnNodeSelected;
        }
        SelectedNode = null;
        if (newValue is not null)
        {
            newValue.PinsChanged += OnEnvelopePinsChanged;
            newValue.NodeSelected += OnNodeSelected;
            newValue.RefreshChoices();
        }
        OnPropertyChanged(nameof(HasSelectedOperation));
        OnPropertyChanged(nameof(ShowElementPanel));
        SelectionChanged?.Invoke();
    }

    private void OnElementPinsChanged(GlobalElementViewModel element) => ElementPinsChanged?.Invoke(element);

    private void OnEnvelopePinsChanged(OperationViewModel operation) => EnvelopePinsChanged?.Invoke(operation);

    /// <summary>A seleção vem de IsSelected nos nós (o SelectedItem do TreeView perde nós ainda não renderizados).</summary>
    private void OnNodeSelected(SchemaNodeViewModel node)
    {
        if (SelectedNode is { } previous && previous != node) previous.IsSelected = false;
        SelectedNode = node;
    }

    /// <summary>Um Schema Set foi fechado: se a seleção era dele, some.</summary>
    public void Forget(SchemaSetViewModel set)
    {
        if (SelectedElement is not null && set.GlobalElements.Contains(SelectedElement)) SelectedElement = null;
        if (SelectedOperation is not null && set.Operations.Contains(SelectedOperation)) SelectedOperation = null;
        if (SelectedSchemaSet == set) SelectedSchemaSet = null;
    }

    /// <summary>O Global Element selecionado, se for deste Schema Set (lembrado antes de recarregá-lo).</summary>
    public GlobalElementViewModel? SelectionIn(SchemaSetViewModel set) =>
        SelectedElement is { } e && set.GlobalElements.Contains(e) ? e : null;

    /// <summary>Um Schema Set foi recarregado: volta a selecionar o mesmo Global Element (nome + arquivo), se ainda existir.</summary>
    public void Reselect(GlobalElementViewModel? before, SchemaSetViewModel after)
    {
        if (before is null) return;
        SelectedElement = after.GlobalElements.FirstOrDefault(g => g.Model.Name == before.Model.Name && g.Model.SourceFile == before.Model.SourceFile);
    }

    // ---- Pesquisa na árvore ----
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<SearchResultViewModel> SearchResults { get; set; } = [];
    [ObservableProperty] public partial SearchResultViewModel? SelectedSearchResult { get; set; }

    partial void OnSearchTextChanged(string value)
    {
        SearchResults = Search(value);
        OnPropertyChanged(nameof(SearchSummary));
        OnPropertyChanged(nameof(HasSearchText));
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    public string SearchSummary => SearchResults.Count == 0
        ? Strings.NoSearchResults
        : string.Format(Strings.SearchResults, SearchResults.Count);

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    partial void OnSelectedSearchResultChanged(SearchResultViewModel? value)
    {
        if (value is not null) Reveal(value.Node);
    }

    private IReadOnlyList<SearchResultViewModel> Search(string text)
    {
        if (SelectedElement is null || string.IsNullOrWhiteSpace(text)) return [];
        var results = new List<SearchResultViewModel>();
        var pending = new Stack<(SchemaNode Node, string Display)>();
        var root = SelectedElement.Model.Tree;
        pending.Push((root, root.Label));
        while (pending.Count > 0 && results.Count < MaxSearchResults)
        {
            var (node, display) = pending.Pop();
            if (Matches(node, text)) results.Add(new SearchResultViewModel(node, display));
            if (node.IsRecursive) continue;
            foreach (var child in node.Children.Reverse())
                pending.Push((child, child.Kind is NodeKind.Element or NodeKind.Attribute ? $"{display}/{child.Label}" : display));
        }
        return results;
    }

    private static bool Matches(SchemaNode node, string text) =>
        node.Kind is NodeKind.Element or NodeKind.Attribute or NodeKind.Choice &&
        (node.Label.Contains(text, StringComparison.OrdinalIgnoreCase) ||
         (node.Documentation?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));

    /// <summary>Expande os ancestrais de um nó na árvore e o seleciona.</summary>
    private void Reveal(SchemaNode target)
    {
        var chain = new List<SchemaNode>();
        for (var n = target; n is not null; n = n.Parent) chain.Insert(0, n);
        var current = TreeRoots.FirstOrDefault(r => r.Node.Path == chain[0].Path);
        foreach (var node in chain.Skip(1))
        {
            if (current is null) return;
            current.IsExpanded = true;
            current = current.Children.FirstOrDefault(c => c.Node.Path == node.Path);
        }
        current?.IsSelected = true;
    }
}
