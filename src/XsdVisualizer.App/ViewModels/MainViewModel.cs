using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XsdVisualizer.App.Resources;
using XsdVisualizer.App.Services;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

public sealed partial class MainViewModel : ViewModelBase, IDisposable, IPayloadBindings
{
    private const int MaxSearchResults = 300;
    private readonly SessionStore _session;
    private readonly SchemaSetLoader _loader = new();
    private readonly Dictionary<string, SchemaFolderWatcher> _watchers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _opening = new(StringComparer.Ordinal);
    private CancellationTokenSource? _exportCancellation;

    public MainViewModel(SessionStore session)
    {
        _session = session;
        var saved = session.Current;
        Recent = new(SessionStore.Normalize(saved.Recent));
        foreach (var folder in SessionStore.Normalize(saved.OpenSchemaSets).Where(Directory.Exists))
            _ = OpenSchemaSetAsync(folder, remember: false);
    }

    public IDialogService? Dialogs { get; set; }

    public ObservableCollection<SchemaSetViewModel> SchemaSets { get; } = [];
    public ObservableCollection<string> Recent { get; }
    public ObservableCollection<EditorTabViewModel> Tabs { get; } = [];

    [ObservableProperty] public partial EditorTabViewModel? SelectedTab { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = Strings.Ready;
    [ObservableProperty] public partial bool IsBusy { get; set; }

    // ---- Global Element selecionado e árvore ----
    [ObservableProperty] public partial object? SelectedSchemaItem { get; set; }
    [ObservableProperty] public partial GlobalElementViewModel? SelectedElement { get; set; }
    [ObservableProperty] public partial IReadOnlyList<SchemaNodeViewModel> TreeRoots { get; set; } = [];
    [ObservableProperty] public partial SchemaNodeViewModel? SelectedNode { get; set; }
    public bool HasSelectedElement => SelectedElement is not null;

    partial void OnSelectedSchemaItemChanged(object? value)
    {
        switch (value)
        {
            case GlobalElementViewModel element:
                SelectedOperation = null;
                SelectedElement = element;
                break;
            case OperationViewModel operation:
                SelectedElement = null;
                SelectedOperation = operation;
                break;
        }
    }

    // ---- Operation selecionada (WSDL) ----
    [ObservableProperty] public partial OperationViewModel? SelectedOperation { get; set; }
    public bool HasSelectedOperation => SelectedOperation is not null;
    public bool ShowElementPanel => SelectedOperation is null;

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
        NotifyGenerate();
    }

    /// <summary>Fixar um ramo na árvore do Payload regera o Envelope Maximal (aba atualizada no lugar).</summary>
    private async void OnEnvelopePinsChanged(OperationViewModel operation)
    {
        var (message, endpoint, binding) = (operation.Message, operation.SelectedEndpoint, operation.CurrentBinding);
        var pins = new Dictionary<string, int>(operation.PayloadOwner?.Pins ?? new());
        var sample = (await Task.Run(() => message.GenerateEnvelopes(SampleKind.Maximal, endpoint, binding, pins)))[0];
        ShowSamples(t => t.IsMaximalEnvelopeOf(message), $"{operation.Name} · {DirectionLabel(message.Direction)} · max", [sample]);
    }

    private void ShowSamples(Func<EditorTabViewModel, bool> existing, string title, IReadOnlyList<Sample> samples)
    {
        if (Tabs.FirstOrDefault(existing) is { } tab)
            tab.ReplaceSamples(samples);
        else
            Tabs.Add(tab = EditorTabViewModel.ForSamples(title, samples));
        SelectedTab = tab;
    }

    private static string DirectionLabel(MessageDirection direction) => DirectionText.Of(direction).ToLowerInvariant();

    partial void OnSelectedElementChanged(GlobalElementViewModel? oldValue, GlobalElementViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.NodeSelected -= OnNodeSelected;
            oldValue.PinsChanged -= OnPinsChanged;
        }
        if (newValue is not null)
        {
            newValue.NodeSelected += OnNodeSelected;
            newValue.PinsChanged += OnPinsChanged;
        }
        TreeRoots = newValue is null ? [] : [new SchemaNodeViewModel(newValue.Model.Tree, newValue, null) { IsExpanded = true }];
        SelectedNode = null;
        if (TreeRoots.FirstOrDefault() is { } root) root.IsSelected = true;
        SearchText = "";
        OnPropertyChanged(nameof(HasSelectedElement));
        NotifyGenerate();
    }

    private void NotifyGenerate()
    {
        GenerateMaximalCommand.NotifyCanExecuteChanged();
        GenerateMinimalCommand.NotifyCanExecuteChanged();
        GenerateCoverageSetCommand.NotifyCanExecuteChanged();
    }

    /// <summary>A seleção vem de IsSelected nos nós (o SelectedItem do TreeView perde nós ainda não renderizados).</summary>
    private void OnNodeSelected(SchemaNodeViewModel node)
    {
        if (SelectedNode is { } previous && previous != node) previous.IsSelected = false;
        SelectedNode = node;
    }

    /// <summary>Fixar uma alternativa regera o Maximal na hora: atualiza a aba de Maximal do elemento ou abre uma.</summary>
    private async void OnPinsChanged(GlobalElementViewModel element)
    {
        var pins = new Dictionary<string, int>(element.Pins);
        var sample = await Task.Run(() => element.Model.GenerateMaximal(pins));
        ShowSamples(t => t.IsMaximalOf(element.Model), $"{element.Model.Name} · max", [sample]);
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

    // ---- Abrir ----
    [RelayCommand]
    private async Task OpenFolder()
    {
        if (Dialogs is null || await Dialogs.PickFolderAsync(Strings.ChooseSchemaFolder) is not { } folder) return;
        await OpenSchemaSetAsync(folder);
    }

    [RelayCommand]
    private async Task OpenXml()
    {
        if (Dialogs is null || await Dialogs.PickXmlFileAsync(Strings.ChooseXml) is not { } file) return;
        await OpenDocumentAsync(file);
    }

    /// <summary>Trata o que foi arrastado: pastas, .xsd e .wsdl abrem Schema Sets; .xml abre Documents.</summary>
    public async Task OpenDroppedAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path) || path.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".wsdl", StringComparison.OrdinalIgnoreCase))
                await OpenSchemaSetAsync(path);
            else if (File.Exists(path))
                await OpenDocumentAsync(path);
        }
    }

    public async Task OpenSchemaSetAsync(string path, bool remember = true)
    {
        var folder = SchemaSetLoader.FolderOf(path);
        var existing = SchemaSets.FirstOrDefault(s => s.Folder == folder);
        if (existing is not null)
        {
            SelectedSchemaItem = existing;
            return;
        }
        // A mesma pasta pode ser pedida de novo enquanto ainda carrega (sessão + linha de comando, duplo drop).
        if (_opening.TryGetValue(folder, out var inFlight))
        {
            await inFlight;
            return;
        }
        var opening = LoadSchemaSetAsync(folder, remember);
        _opening[folder] = opening;
        try { await opening; }
        finally { _opening.Remove(folder); }
    }

    private async Task LoadSchemaSetAsync(string folder, bool remember)
    {
        await RunBusy(string.Format(Strings.Loading, Path.GetFileName(folder)), async () =>
        {
            var set = await Task.Run(() => _loader.Open(folder));
            var vm = new SchemaSetViewModel(set, this);
            SchemaSets.Add(vm);
            SchemaSetsChanged();
            Watch(folder);
            if (remember) Recent.Replace(SessionStore.Touch(Recent, folder));
            SaveSession();
            RefreshBindings();
            Status = string.Format(Strings.Opened, set.Name, set.GlobalElements.Count);
        });
    }

    private async Task OpenDocumentAsync(string file)
    {
        await RunBusy(string.Format(Strings.Loading, Path.GetFileName(file)), async () =>
        {
            var text = await File.ReadAllTextAsync(file);
            // Schema Sets ainda carregando (ex.: vindos na mesma linha de comando) entram no Binding.
            await Task.WhenAll(_opening.Values.ToList());
            var tab = EditorTabViewModel.ForDocument(file, text, SchemaSets.Select(s => s.Model), Get);
            Tabs.Add(tab);
            SelectedTab = tab;
            Status = Strings.Ready;
        });
    }

    [RelayCommand]
    private void CloseSchemaSet(SchemaSetViewModel set)
    {
        SchemaSets.Remove(set);
        if (_watchers.Remove(set.Folder, out var watcher)) watcher.Dispose();
        if (SelectedElement is not null && set.GlobalElements.Contains(SelectedElement)) SelectedElement = null;
        if (SelectedOperation is not null && set.Operations.Contains(SelectedOperation)) SelectedOperation = null;
        SchemaSetsChanged();
        SaveSession();
        RefreshBindings();
    }

    private void Watch(string folder)
    {
        _watchers[folder] = new SchemaFolderWatcher(folder, () => Dispatcher.UIThread.Post(() => _ = ReloadAsync(folder)));
    }

    private async Task ReloadAsync(string folder)
    {
        var index = SchemaSets.ToList().FindIndex(s => s.Folder == folder);
        if (index < 0) return;
        var selectedName = SelectedElement is { } e && SchemaSets[index].GlobalElements.Contains(e) ? (e.Model.Name, e.Model.SourceFile) : default;
        var set = await Task.Run(() => _loader.Open(folder));
        var vm = new SchemaSetViewModel(set, this);
        SchemaSets[index] = vm;
        SchemaSetsChanged();
        if (selectedName != default)
            SelectedElement = vm.GlobalElements.FirstOrDefault(g => (g.Model.Name, g.Model.SourceFile) == selectedName);
        RefreshBindings();
        Status = string.Format(Strings.Reloaded, set.Name);
    }

    private void RefreshBindings()
    {
        foreach (var tab in Tabs) tab.RefreshBinding(SchemaSets.Select(s => s.Model));
    }

    private void SaveSession() => _session.SaveSchemaSets(SchemaSets.Select(s => s.Folder), Recent);

    // ---- Payload Bindings (IPayloadBindings) ----
    public IReadOnlyList<GlobalElementChoice> PayloadChoices { get; private set; } = [];

    /// <summary>Schema Sets mudaram: a lista de Global Elements para Payload e os vínculos salvos são refeitos.</summary>
    private void SchemaSetsChanged()
    {
        PayloadChoices = SchemaSets.SelectMany(s => s.Model.GlobalElements).Select(e => new GlobalElementChoice(e)).ToList();
        foreach (var operation in SchemaSets.SelectMany(s => s.Operations)) operation.RefreshChoices();
    }

    public PayloadBinding? Get(OperationMessage message)
    {
        if (!_session.Current.PayloadBindings.TryGetValue(SessionKeys.Of(message), out var saved)) return null;
        var element = SchemaSets.SelectMany(s => s.Model.GlobalElements).FirstOrDefault(e =>
            e.Name == saved.ElementName && e.Namespace == saved.ElementNamespace && e.SourceFile == saved.ElementSourceFile);
        return element is null ? null : new PayloadBinding(element, saved.Compressed);
    }

    public void Set(OperationMessage message, GlobalElement? element, bool compressed)
    {
        var key = SessionKeys.Of(message);
        if (element is null) _session.Current.PayloadBindings.Remove(key);
        else _session.Current.PayloadBindings[key] = new SavedPayloadBinding
        {
            ElementName = element.Name,
            ElementNamespace = element.Namespace,
            ElementSourceFile = element.SourceFile,
            Compressed = compressed,
        };
        _session.Save();
        RefreshBindings();
    }

    private Endpoint? ChosenEndpoint(Operation operation) =>
        GetEndpoint(operation) is { } name ? operation.Endpoints.FirstOrDefault(e => e.Name == name) : null;

    public string? GetEndpoint(Operation operation) =>
        _session.Current.Endpoints.GetValueOrDefault(SessionKeys.Of(operation));

    public void SetEndpoint(Operation operation, Endpoint endpoint)
    {
        _session.Current.Endpoints[SessionKeys.Of(operation)] = endpoint.Name;
        _session.Save();
    }

    [RelayCommand]
    private void OpenPayload(EditorTabViewModel tab)
    {
        if (tab.PayloadText is not { } text) return;
        var payload = EditorTabViewModel.ForPayload(string.Format(Strings.PayloadTitle, tab.Title), text, tab.PayloadElement);
        Tabs.Add(payload);
        SelectedTab = payload;
    }

    /// <summary>Depois de trocar o idioma: textos que o view model guarda voltam a ser lidos dos recursos.</summary>
    public void RefreshTexts()
    {
        Status = Strings.Ready;
        foreach (var set in SchemaSets) set.RefreshTexts();
    }

    // ---- Comparar ----
    [RelayCommand]
    private async Task Compare()
    {
        if (Dialogs is null) return;
        if (SchemaSets.Count < 2)
        {
            Status = Strings.NeedTwoSets;
            return;
        }
        if (await Dialogs.PickComparisonAsync(SchemaSets.Select(s => s.Model).ToList()) is not var (before, after)) return;
        var comparison = new ComparisonViewModel(before, after);
        Dialogs.ShowComparison(comparison);
        await comparison.LoadAsync();
    }

    // ---- Gerar ----
    private bool CanGenerate() => (SelectedElement is not null || SelectedOperation is not null) && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private Task GenerateMaximal() => SelectedOperation is { } op
        ? GenerateEnvelopesAsync(op, SampleKind.Maximal, "max")
        : GenerateAsync(e => [e.Model.GenerateMaximal(new Dictionary<string, int>(e.Pins))], "max");

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private Task GenerateMinimal() => SelectedOperation is { } op
        ? GenerateEnvelopesAsync(op, SampleKind.Minimal, "min")
        : GenerateAsync(e => [e.Model.GenerateMinimal()], "min");

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private Task GenerateCoverageSet() => SelectedOperation is { } op
        ? GenerateEnvelopesAsync(op, SampleKind.Coverage, Strings.CoverageSet)
        : GenerateAsync(e => e.Model.GenerateCoverageSet(), Strings.CoverageSet);

    private async Task GenerateEnvelopesAsync(OperationViewModel operation, SampleKind kind, string suffix)
    {
        var (message, endpoint, binding) = (operation.Message, operation.SelectedEndpoint, operation.CurrentBinding);
        var pins = new Dictionary<string, int>(operation.PayloadOwner?.Pins ?? new());
        await RunBusy(string.Format(Strings.Generating, operation.Name), async () =>
        {
            var samples = await Task.Run(() => message.GenerateEnvelopes(kind, endpoint, binding, pins));
            var tab = EditorTabViewModel.ForSamples($"{operation.Name} · {DirectionLabel(message.Direction)} · {suffix}", samples);
            Tabs.Add(tab);
            // Payload compactado: o legível abre ao lado; o Envelope continua em foco.
            if (tab.PayloadText is { } payload)
                Tabs.Add(EditorTabViewModel.ForPayload(string.Format(Strings.PayloadTitle, tab.Title), payload, tab.PayloadElement));
            SelectedTab = tab;
            Status = Strings.Ready;
        });
    }

    private async Task GenerateAsync(Func<GlobalElementViewModel, IReadOnlyList<Sample>> generate, string suffix)
    {
        var element = SelectedElement!;
        await RunBusy(string.Format(Strings.Generating, element.Model.Name), async () =>
        {
            var samples = await Task.Run(() => generate(element));
            var tab = EditorTabViewModel.ForSamples($"{element.Model.Name} · {suffix}", samples);
            Tabs.Add(tab);
            SelectedTab = tab;
            Status = Strings.Ready;
        });
    }

    [RelayCommand]
    private async Task ExportAll(SchemaSetViewModel set)
    {
        if (Dialogs is null || await Dialogs.PickFolderAsync(Strings.ChooseExportFolder) is not { } output) return;
        _exportCancellation = new CancellationTokenSource();
        var cancellation = _exportCancellation.Token;
        await RunBusy(string.Format(Strings.Exporting, set.Name), async () =>
        {
            var progress = new Progress<GlobalElement>(e => Status = string.Format(Strings.Exporting, e.Name));
            var result = await Task.Run(() => SampleExporter.ExportAll(set.Model, output, progress, cancellation,
                payloadBindings: Get, endpoints: ChosenEndpoint), cancellation);
            Status = string.Format(Strings.ExportDone, result.Written, result.Invalid, Path.Combine(output, set.Name));
        });
    }

    // ---- Salvar e fechar abas ----
    [RelayCommand]
    private async Task Save(EditorTabViewModel? tab)
    {
        tab ??= SelectedTab;
        if (tab is null) return;
        if (!tab.IsDocument || tab.FilePath is null)
        {
            await SaveAs(tab);
            return;
        }
        if (!await ConfirmIfInvalid(tab)) return;
        await WriteTab(tab, tab.FilePath);
    }

    [RelayCommand]
    private async Task SaveAs(EditorTabViewModel? tab)
    {
        tab ??= SelectedTab;
        if (tab is null || Dialogs is null) return;
        if (await Dialogs.PickSavePathAsync(tab.SuggestedFileName) is not { } path) return;
        if (tab.IsDocument && !await ConfirmIfInvalid(tab)) return;
        await WriteTab(tab, path);
    }

    private async Task<bool> ConfirmIfInvalid(EditorTabViewModel tab)
    {
        var issues = tab.Issues.Count;
        return issues == 0 || Dialogs is null ||
               await Dialogs.ConfirmAsync(Strings.ConfirmSaveTitle, string.Format(Strings.ConfirmSaveInvalid, issues), Strings.ConfirmSave, Strings.Cancel);
    }

    private async Task WriteTab(EditorTabViewModel tab, string path)
    {
        try
        {
            await File.WriteAllTextAsync(path, tab.Document.Text);
            if (tab.IsDocument) tab.MarkSaved(path);
            Status = string.Format(Strings.Saved, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = string.Format(Strings.Error, e.Message);
        }
    }

    [RelayCommand]
    private void CloseTab(EditorTabViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (Tabs.Count > 0) SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
    }

    private async Task RunBusy(string status, Func<Task> action)
    {
        IsBusy = true;
        Status = status;
        GenerateMaximalCommand.NotifyCanExecuteChanged();
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            Status = Strings.Ready;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Status = string.Format(Strings.Error, e.Message);
        }
        finally
        {
            IsBusy = false;
            NotifyGenerate();
        }
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers.Values) watcher.Dispose();
        _exportCancellation?.Dispose();
    }
}

internal static class CollectionExtensions
{
    public static void Replace<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        var list = items.ToList();
        collection.Clear();
        foreach (var item in list) collection.Add(item);
    }
}
