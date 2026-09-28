using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.Views;

/// <summary>Pinta o fundo das linhas do lado a lado: verde entrou, vermelho saiu, âmbar mudou, hachurado para alinhamento.</summary>
internal sealed class DiffLineRenderer : IBackgroundRenderer
{
    public IReadOnlyList<DiffLine> Lines { get; set; } = [];
    public bool Dark { get; set; }
    public int? Highlighted { get; set; }
    public KnownLayer Layer => KnownLayer.Background;

    private IBrush? Fill(DiffLineKind kind) => kind switch
    {
        DiffLineKind.Added => new SolidColorBrush(Color.Parse(Dark ? "#1E4D2E" : "#DDF4E4")),
        DiffLineKind.Removed => new SolidColorBrush(Color.Parse(Dark ? "#5A2424" : "#FBE3E1")),
        DiffLineKind.Modified => new SolidColorBrush(Color.Parse(Dark ? "#5A4A14" : "#FEF3D0")),
        DiffLineKind.Imaginary => new SolidColorBrush(Color.Parse(Dark ? "#1C1C1C" : "#F3F3F3")),
        _ => null,
    };

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
                context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse(Dark ? "#8AB4F8" : "#0B57D0")), 1.5), rect.Deflate(0.75));
        }
    }
}
