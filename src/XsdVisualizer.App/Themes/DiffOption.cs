using Avalonia.Media;
using XsdVisualizer.App.Resources;
using XsdVisualizer.App.Services;

namespace XsdVisualizer.App.Themes;

/// <summary>Um item da lista de cores do diff: nome no idioma atual e amostra (entrou, saiu, mudou) sobre o tema atual.</summary>
public sealed record DiffOption(DiffPalette Palette, ThemeColors Swatch)
{
    public DiffColorsChoice Choice => Palette.Choice;

    public string Name => Palette.Choice switch
    {
        DiffColorsChoice.Theme => Strings.DiffFromTheme,
        DiffColorsChoice.ColorBlind => Strings.DiffColorBlind,
        DiffColorsChoice.Tritanopia => Strings.DiffTritanopia,
        DiffColorsChoice.Classic => Strings.DiffClassic,
        DiffColorsChoice.HighContrast => Strings.DiffHighContrast,
        _ => Palette.Name,
    };

    public IBrush Background => ThemeBrushes.ToBrush(Swatch.Editor);
    public IBrush Border => ThemeBrushes.ToBrush(Swatch.Border);
    public IBrush Added => ThemeBrushes.ToBrush(Swatch.DiffAdded);
    public IBrush Removed => ThemeBrushes.ToBrush(Swatch.DiffRemoved);
    public IBrush Modified => ThemeBrushes.ToBrush(Swatch.DiffModified);

    /// <summary>Os esquemas com a amostra aplicada ao tema e modo em uso.</summary>
    public static IReadOnlyList<DiffOption> For(AppTheme theme, bool dark) =>
        DiffPaletteCatalog.All.Select(p => new DiffOption(p, p.ApplyTo(theme.ColorsFor(dark), dark))).ToList();
}
