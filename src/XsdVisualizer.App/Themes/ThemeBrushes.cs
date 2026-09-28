using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace XsdVisualizer.App.Themes;

/// <summary>Pincéis e canetas do tema ativo, montados uma vez a cada aplicação de tema (não a cada desenho).</summary>
public sealed class ThemeBrushes
{
    public ThemeBrushes(ThemeColors c)
    {
        Link = ToBrush(c.Link);
        DiffAddedLine = ToBrush(c.DiffAddedLine);
        DiffRemovedLine = ToBrush(c.DiffRemovedLine);
        DiffModifiedLine = ToBrush(c.DiffModifiedLine);
        DiffBlankLine = ToBrush(c.DiffBlankLine);
        ErrorUnderline = new Pen(ToBrush(c.IssueError), 1.5);
        WarningUnderline = new Pen(ToBrush(c.IssueWarning), 1.5);
        RevealedLine = new Pen(ToBrush(c.Accent), 1.5);
    }

    public IBrush Link { get; }
    public IBrush DiffAddedLine { get; }
    public IBrush DiffRemovedLine { get; }
    public IBrush DiffModifiedLine { get; }
    public IBrush DiffBlankLine { get; }
    public IPen ErrorUnderline { get; }
    public IPen WarningUnderline { get; }
    public IPen RevealedLine { get; }

    public static Color ToColor(ThemeColor c) => Color.FromRgb(c.R, c.G, c.B);
    public static IImmutableSolidColorBrush ToBrush(ThemeColor c) => new ImmutableSolidColorBrush(ToColor(c));
}
