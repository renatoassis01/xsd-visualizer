using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using AvaloniaEdit;
using XsdVisualizer.App.Resources;
using XsdVisualizer.App.ViewModels;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.Views;

public partial class ComparisonWindow : Window
{
    private readonly DiffLineRenderer _beforeLines = new();
    private readonly DiffLineRenderer _afterLines = new();
    private bool _syncing;

    public ComparisonWindow()
    {
        InitializeComponent();
        foreach (var (editor, renderer) in new[] { (BeforeEditor, _beforeLines), (AfterEditor, _afterLines) })
        {
            XmlHighlighting.Apply(Application.Current?.ActualThemeVariant);
            editor.SyntaxHighlighting = XmlHighlighting.Definition;
            editor.TextArea.TextView.BackgroundRenderers.Add(renderer);
        }
        // Rolagem sincronizada: as duas colunas têm o mesmo número de linhas (linhas em branco alinham).
        BeforeEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => Sync(BeforeEditor, AfterEditor);
        AfterEditor.TextArea.TextView.ScrollOffsetChanged += (_, _) => Sync(AfterEditor, BeforeEditor);
        ApplyTheme();
        if (Application.Current is { } app) app.ActualThemeVariantChanged += (_, _) => ApplyTheme();
    }

    private ComparisonViewModel? Vm => DataContext as ComparisonViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Vm is not { } vm) return;
        vm.PropertyChanged += OnViewModelChanged;
        vm.RevealPathRequested += Reveal;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ComparisonViewModel.Diff)) return;
        var diff = Vm?.Diff;
        Show(BeforeEditor, _beforeLines, diff?.Before ?? []);
        Show(AfterEditor, _afterLines, diff?.After ?? []);
        if (Vm?.SelectedChange is { } change) Reveal(change.Model.Path);
    }

    private static void Show(TextEditor editor, DiffLineRenderer renderer, IReadOnlyList<DiffLine> lines)
    {
        renderer.Lines = lines;
        renderer.Highlighted = null;
        editor.Document = new AvaloniaEdit.Document.TextDocument(string.Join("\n", lines.Select(l => l.Text)));
    }

    /// <summary>Rola os dois lados até a primeira linha do elemento com esse caminho e a destaca.</summary>
    private void Reveal(string path)
    {
        var lines = _afterLines.Lines.Any(l => l.Path == path) ? _afterLines.Lines : _beforeLines.Lines;
        var index = lines.ToList().FindIndex(l => l.Path == path);
        if (index < 0) return;
        _beforeLines.Highlighted = _afterLines.Highlighted = index;
        AfterEditor.ScrollTo(index + 1, 1);
        BeforeEditor.ScrollTo(index + 1, 1);
        BeforeEditor.TextArea.TextView.InvalidateLayer(_beforeLines.Layer);
        AfterEditor.TextArea.TextView.InvalidateLayer(_afterLines.Layer);
    }

    private void Sync(TextEditor source, TextEditor target)
    {
        if (_syncing) return;
        _syncing = true;
        target.ScrollToVerticalOffset(source.TextArea.TextView.VerticalOffset);
        _syncing = false;
    }

    private void ApplyTheme()
    {
        var dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        _beforeLines.Dark = _afterLines.Dark = dark;
        Resources["DiffAddedText"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(dark ? "#81C995" : "#1E7B34"));
        Resources["DiffRemovedText"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(dark ? "#F28B82" : "#C5221F"));
        Resources["DiffModifiedText"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(dark ? "#FDD663" : "#B06000"));
        XmlHighlighting.Apply(Application.Current?.ActualThemeVariant);
        BeforeEditor.TextArea.TextView.Redraw();
        AfterEditor.TextArea.TextView.Redraw();
    }

    private async void OnCopySummary(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || Clipboard is not { } clipboard) return;
        await clipboard.SetTextAsync(vm.Summary(vm.ShowDocumentation));
        vm.Status = Strings.SummaryCopied;
    }

    private async void OnExportSummary(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = $"comparacao-{vm.Before.Name}-{vm.After.Name}.md",
            DefaultExtension = "md",
            FileTypeChoices = [new FilePickerFileType("Markdown") { Patterns = ["*.md"] }],
        });
        if (file?.TryGetLocalPath() is not { } path) return;
        await File.WriteAllTextAsync(path, vm.Summary(vm.ShowDocumentation));
        vm.Status = string.Format(Strings.Saved, path);
    }
}
