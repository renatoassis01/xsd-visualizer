using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using XsdVisualizer.App.Resources;
using XsdVisualizer.App.ViewModels;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.Views;

public partial class XmlEditorView : UserControl
{
    private readonly IssueUnderlineRenderer _underlines = new();
    private EditorTabViewModel? _tab;

    public XmlEditorView()
    {
        InitializeComponent();
        XmlHighlighting.Apply(Application.Current?.ActualThemeVariant);
        Editor.SyntaxHighlighting = XmlHighlighting.Definition;
        Editor.TextArea.TextView.LinkTextForegroundBrush = XmlHighlighting.LinkBrush(Application.Current?.ActualThemeVariant);
        Editor.TextArea.TextView.BackgroundRenderers.Add(_underlines);
        IssueList.SelectionChanged += (_, _) =>
        {
            if (IssueList.SelectedItem is ValidationIssue issue) GoTo(issue);
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Application.Current is { } app) app.ActualThemeVariantChanged += OnThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Application.Current is { } app) app.ActualThemeVariantChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        XmlHighlighting.Apply(Application.Current?.ActualThemeVariant);
        Editor.TextArea.TextView.LinkTextForegroundBrush = XmlHighlighting.LinkBrush(Application.Current?.ActualThemeVariant);
        Editor.TextArea.TextView.Redraw();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_tab is not null) _tab.Issues.CollectionChanged -= OnIssuesChanged;
        _tab = DataContext as EditorTabViewModel;
        if (_tab is not null) _tab.Issues.CollectionChanged += OnIssuesChanged;
        OnIssuesChanged(null, null);
    }

    private void OnIssuesChanged(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        _underlines.Issues = _tab?.Issues.ToList() ?? [];
        Editor.TextArea.TextView.InvalidateLayer(_underlines.Layer);
    }

    private async void OnCopyClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { Clipboard: { } clipboard } topLevel || Editor.Document is null) return;
        await clipboard.SetTextAsync(Editor.Document.Text);
        if (topLevel.DataContext is MainViewModel main) main.Status = Strings.Copied;
    }

    private void GoTo(ValidationIssue issue)
    {
        var document = Editor.Document;
        if (document is null || issue.Line < 1 || issue.Line > document.LineCount) return;
        var line = document.GetLineByNumber(issue.Line);
        Editor.CaretOffset = line.Offset + Math.Clamp(issue.Column - 1, 0, line.Length);
        Editor.ScrollTo(issue.Line, Math.Max(issue.Column, 1));
        Editor.TextArea.Focus();
    }
}
