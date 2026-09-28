using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace XsdVisualizer.App.Themes;

/// <summary>
/// Aplica um tema do catálogo ao app: variante claro/escuro (e a barra de título), paleta do Fluent e os recursos
/// próprios (editor, diff, avisos). A paleta do Fluent só vale se estiver na instância quando ela carrega, então
/// trocar de tema troca a instância do FluentTheme.
/// </summary>
public static class ThemeApplier
{
    public static AppTheme Theme { get; private set; } = ThemeCatalog.All[0];

    /// <summary>Cores em uso agora (para "Igual ao sistema", as do modo atual do sistema).</summary>
    public static ThemeColors Colors { get; private set; } = ThemeCatalog.All[0].Light;

    /// <summary>Disparado depois de trocar o tema ou o modo do sistema: editores e diffs se redesenham.</summary>
    public static event Action<ThemeColors>? Changed;

    public static void Apply(Application app, AppTheme theme)
    {
        Theme = theme;
        app.RequestedThemeVariant = theme.Mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        var fluent = new FluentTheme();
        if (theme.IsNamed)
        {
            var dark = theme.Mode == ThemeMode.Dark;
            fluent.Palettes[dark ? ThemeVariant.Dark : ThemeVariant.Light] = Palette(theme.ColorsFor(dark));
        }
        var index = app.Styles.ToList().FindIndex(s => s is FluentTheme);
        if (index >= 0) app.Styles[index] = fluent;
        else app.Styles.Insert(0, fluent);

        ApplyResources(app);
    }

    /// <summary>Recursos próprios do app para o modo em vigor (chamado também quando o sistema troca claro/escuro).</summary>
    public static void ApplyResources(Application app)
    {
        var dark = Theme.Mode == ThemeMode.System ? app.ActualThemeVariant == ThemeVariant.Dark : Theme.Mode == ThemeMode.Dark;
        var c = Colors = Theme.ColorsFor(dark);
        void Set(string key, string color) => app.Resources[key] = new SolidColorBrush(Color.Parse(color));
        Set("EditorBackground", c.Editor);
        Set("EditorForeground", c.Text);
        Set("EditorLineNumbers", c.TextMuted);
        Set("DiffAddedText", c.DiffAdded);
        Set("DiffRemovedText", c.DiffRemoved);
        Set("DiffModifiedText", c.DiffModified);
        Set("WarningText", c.IssueWarning);
        Set("OnAccentText", c.OnAccent);
        Set("ErrorText", c.IssueError);
        Changed?.Invoke(c);
    }

    public static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private static ColorPaletteResources Palette(ThemeColors c)
    {
        Color C(string hex) => Color.Parse(hex);
        return new ColorPaletteResources
        {
            Accent = C(c.Accent),
            RegionColor = C(c.Window),
            AltHigh = C(c.Window),
            AltMediumHigh = C(c.Window),
            AltMedium = C(c.Panel),
            AltMediumLow = C(c.Panel),
            AltLow = C(c.Panel),
            BaseHigh = C(c.Text),
            BaseMediumHigh = C(c.Text),
            BaseMedium = C(c.TextMuted),
            BaseMediumLow = C(c.TextMuted),
            BaseLow = C(c.Border),
            ChromeAltLow = C(c.TextMuted),
            ChromeBlackHigh = C(c.Text),
            ChromeBlackMedium = C(c.TextMuted),
            ChromeBlackMediumLow = C(c.Border),
            ChromeBlackLow = C(c.Border),
            ChromeDisabledHigh = C(c.Border),
            ChromeDisabledLow = C(c.TextMuted),
            ChromeGray = C(c.TextMuted),
            ChromeHigh = C(c.Border),
            ChromeLow = C(c.Panel),
            ChromeMedium = C(c.Hover),
            ChromeMediumLow = C(c.Panel),
            ChromeWhite = C(c.Window),
            ListLow = C(c.Hover),
            ListMedium = C(c.Selection),
            ErrorText = C(c.IssueError),
        };
    }
}
