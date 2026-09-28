using Avalonia.Controls;
using Avalonia.Input;
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

    public Task ShowAboutAsync() => new AboutWindow().ShowDialog(this);

    private void OnAboutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _ = ShowAboutAsync();

    public Task<bool> ConfirmAsync(string title, string message, string confirm, string cancel) =>
        new ConfirmDialog(title, message, confirm, cancel).ShowDialog<bool>(this);
}
