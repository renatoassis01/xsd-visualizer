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
        _ => Theme.Name,
    };

    // "Igual ao sistema" mostra a amostra do modo claro.
    private ThemeColors Swatch => Theme.ColorsFor(Theme.Mode == ThemeMode.Dark);
    public IBrush Background => ThemeApplier.Brush(Swatch.Editor);
    public IBrush Accent => ThemeApplier.Brush(Swatch.Accent);
    public IBrush Tag => ThemeApplier.Brush(Swatch.SyntaxTag);
    public IBrush Attribute => ThemeApplier.Brush(Swatch.SyntaxAttribute);
    public IBrush Value => ThemeApplier.Brush(Swatch.SyntaxValue);
    public IBrush Border => ThemeApplier.Brush(Swatch.Border);
}
