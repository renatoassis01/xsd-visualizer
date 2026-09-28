using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit.Highlighting;
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
        Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        Editor.TextArea.TextView.BackgroundRenderers.Add(_underlines);
        IssueList.SelectionChanged += (_, _) =>
        {
            if (IssueList.SelectedItem is ValidationIssue issue) GoTo(issue);
        };
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
