using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.Views;

/// <summary>Pinta o fundo das linhas do lado a lado: verde entrou, vermelho saiu, âmbar mudou, hachurado para alinhamento.</summary>
internal sealed class DiffLineRenderer : IBackgroundRenderer
{
    public IReadOnlyList<DiffLine> Lines { get; set; } = [];
    public int? Highlighted { get; set; }
    public KnownLayer Layer => KnownLayer.Background;

    private static IBrush? Fill(DiffLineKind kind)
    {
        var brushes = Themes.ThemeApplier.Brushes;
        return kind switch
        {
            DiffLineKind.Added => brushes.DiffAddedLine,
            DiffLineKind.Removed => brushes.DiffRemovedLine,
            DiffLineKind.Modified => brushes.DiffModifiedLine,
            DiffLineKind.Imaginary => brushes.DiffBlankLine,
            _ => null,
        };
    }

    public void Draw(TextView textView, DrawingContext context)
    {
        if (!textView.VisualLinesValid) return;
        foreach (var visual in textView.VisualLines)
        {
            var index = visual.FirstDocumentLine.LineNumber - 1;
            if (index < 0 || index >= Lines.Count) continue;
            var y = visual.VisualTop - textView.VerticalOffset;
            var rect = new Rect(0, y, textView.Bounds.Width, visual.Height);
            if (Fill(Lines[index].Kind) is { } brush) context.FillRectangle(brush, rect);
            if (Highlighted == index)
                context.DrawRectangle(null, Themes.ThemeApplier.Brushes.RevealedLine, rect.Deflate(0.75));
        }
    }
}
