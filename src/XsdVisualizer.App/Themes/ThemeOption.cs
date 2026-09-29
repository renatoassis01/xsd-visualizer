using Avalonia.Media;
using XsdVisualizer.App.Resources;
using XsdVisualizer.App.Services;

namespace XsdVisualizer.App.Themes;

/// <summary>Um item da lista de temas: nome no idioma atual e a amostra de cores (fundo, destaque, três de sintaxe).</summary>
public sealed record ThemeOption(AppTheme Theme)
{
    public ThemeChoice Choice => Theme.Choice;

    public string Name => Theme.Choice switch
    {
        ThemeChoice.System => Strings.ThemeSystem,
        ThemeChoice.Light => Strings.ThemeLight,
        ThemeChoice.Dark => Strings.ThemeDark,
        ThemeChoice.GitHubLight => Strings.ThemeGitHubLight,
        ThemeChoice.GitHubDark => Strings.ThemeGitHubDark,
        ThemeChoice.Indigo => Strings.ThemeIndigo,
        _ => Theme.Name,
    };

    // "Igual ao sistema" mostra a amostra do modo claro.
    private ThemeColors Swatch => Theme.ColorsFor(Theme.Mode == ThemeMode.Dark);
    public IBrush Background => ThemeBrushes.ToBrush(Swatch.Editor);
    public IBrush Accent => ThemeBrushes.ToBrush(Swatch.Accent);
    public IBrush Tag => ThemeBrushes.ToBrush(Swatch.SyntaxTag);
    public IBrush Attribute => ThemeBrushes.ToBrush(Swatch.SyntaxAttribute);
    public IBrush Value => ThemeBrushes.ToBrush(Swatch.SyntaxValue);
    public IBrush Border => ThemeBrushes.ToBrush(Swatch.Border);
}
