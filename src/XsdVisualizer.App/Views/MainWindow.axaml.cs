using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using XsdVisualizer.App.Services;
using XsdVisualizer.App.ViewModels;

namespace XsdVisualizer.App.Views;

public partial class MainWindow : Window, IDialogService
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        SearchShortcut.Text = OperatingSystem.IsMacOS() ? "⌘F" : "Ctrl+F";
        // Túnel: o atalho vale mesmo com o foco no editor, que consome as teclas.
        AddHandler(KeyDownEvent, OnSearchShortcut, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private void OnSearchShortcut(object? sender, KeyEventArgs e)
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key != Key.F || e.KeyModifiers != modifier || !SearchBox.IsEffectivelyVisible || !SearchBox.IsEnabled) return;
        SearchBox.Focus();
        SearchBox.SelectAll();
        e.Handled = true;
    }

    private async void OnCopyPathClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: SchemaNodeViewModel node } || Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(node.Node.Path);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm) vm.Dialogs = this;
    }

    private static void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToList() ?? [];
        await vm.OpenDroppedAsync(paths);
    }

    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickXmlFileAsync(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            FileTypeFilter = [new FilePickerFileType("XML") { Patterns = ["*.xml"] }, FilePickerFileTypes.All],
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickSavePathAsync(string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = "xml",
            FileTypeChoices = [new FilePickerFileType("XML") { Patterns = ["*.xml"] }],
        });
        return file?.TryGetLocalPath();
    }

    public async Task<(XsdVisualizer.Core.SchemaSet Before, XsdVisualizer.Core.SchemaSet After)?> PickComparisonAsync(
        IReadOnlyList<XsdVisualizer.Core.SchemaSet> sets) =>
        await new CompareDialog(sets).ShowDialog<(XsdVisualizer.Core.SchemaSet, XsdVisualizer.Core.SchemaSet)?>(this);

    public void ShowComparison(ComparisonViewModel comparison) =>
        new ComparisonWindow { DataContext = comparison }.Show(); // sem dono: a troca de idioma recria a janela principal

    public Task ShowAboutAsync() => new AboutWindow().ShowDialog(this);

    private async void OnRecentClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: string folder } || DataContext is not MainViewModel vm) return;
        RecentButton.Flyout?.Hide();
        await vm.OpenSchemaSetAsync(folder);
    }

    private void OnSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        (Avalonia.Application.Current as App)?.ShowSettings();

    private void OnAboutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = ShowAboutAsync();

    public Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel) =>
        new ConfirmDialog(title, message, confirm, cancel).ShowDialog<bool>(this);
}
