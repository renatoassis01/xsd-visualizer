using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.Views;

/// <summary>Sublinha no editor o trecho apontado por cada Validation Issue.</summary>
internal sealed class IssueUnderlineRenderer : IBackgroundRenderer
{
    private static IPen ErrorPen => new Pen(Themes.ThemeApplier.Brush(Themes.ThemeApplier.Colors.IssueError), 1.5);
    private static IPen WarningPen => new Pen(Themes.ThemeApplier.Brush(Themes.ThemeApplier.Colors.IssueWarning), 1.5);

    public IReadOnlyList<ValidationIssue> Issues { get; set; } = [];
    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var document = textView.Document;
        if (document is null || !textView.VisualLinesValid) return;
        foreach (var issue in Issues)
        {
            if (issue.Line < 1 || issue.Line > document.LineCount) continue;
            var segment = Segment(document, issue);
            var pen = issue.Severity == IssueSeverity.Warning ? WarningPen : ErrorPen;
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                DrawWave(drawingContext, pen, rect);
        }
    }

    /// <summary>Do ponto indicado até o fim da palavra/tag; a linha inteira se o ponto não servir.</summary>
    private static TextSegment Segment(TextDocument document, ValidationIssue issue)
    {
        var line = document.GetLineByNumber(issue.Line);
        var start = line.Offset + Math.Clamp(issue.Column - 1, 0, line.Length);
        var end = start;
        while (end < line.EndOffset && !char.IsWhiteSpace(document.GetCharAt(end)) && document.GetCharAt(end) is not ('>' or '<' or '/'))
            end++;
        if (end == start)
        {
            start = line.Offset;
            end = line.EndOffset;
        }
        return new TextSegment { StartOffset = start, EndOffset = end };
    }

    private static void DrawWave(DrawingContext context, IPen pen, Rect rect)
    {
        var y = rect.Bottom - 1;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(rect.Left, y), false);
            var up = true;
            for (var x = rect.Left + 2; x <= rect.Right; x += 2, up = !up)
                g.LineTo(new Point(x, up ? y - 2 : y));
        }
        context.DrawGeometry(null, pen, geometry);
    }
}
