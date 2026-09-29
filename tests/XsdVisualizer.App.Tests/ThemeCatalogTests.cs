using System.Globalization;
using XsdVisualizer.App.Services;
using XsdVisualizer.App.Themes;

namespace XsdVisualizer.App.Tests;

public class ThemeCatalogTests
{
    /// <summary>Cada tema, em cada modo que ele usa, com cada esquema de cores do diff.</summary>
    public static TheoryData<ThemeChoice, bool, DiffColorsChoice> Palettes()
    {
        var data = new TheoryData<ThemeChoice, bool, DiffColorsChoice>();
        foreach (var theme in ThemeCatalog.All)
            foreach (var dark in theme.Mode == ThemeMode.System ? new[] { false, true } : new[] { theme.Mode == ThemeMode.Dark })
                foreach (var diff in DiffPaletteCatalog.All)
                    data.Add(theme.Choice, dark, diff.Choice);
        return data;
    }

    private static ThemeColors ColorsOf(ThemeChoice choice, bool dark, DiffColorsChoice diff) =>
        DiffPaletteCatalog.Get(diff).ApplyTo(ThemeCatalog.Get(choice).ColorsFor(dark), dark);

    [Fact]
    public void The_list_offers_system_light_dark_and_the_named_themes_in_order()
    {
        Assert.Equal(
            [ThemeChoice.System, ThemeChoice.Light, ThemeChoice.Dark, ThemeChoice.Indigo, ThemeChoice.GitHubLight, ThemeChoice.GitHubDark,
             ThemeChoice.Dracula, ThemeChoice.GruvboxLight, ThemeChoice.Andromeda, ThemeChoice.OneLight, ThemeChoice.OneDark,
             ThemeChoice.Nord, ThemeChoice.CatppuccinLatte, ThemeChoice.CatppuccinMocha, ThemeChoice.TokyoNight,
             ThemeChoice.TokyoNightStorm, ThemeChoice.TokyoNightMoon, ThemeChoice.TokyoNightDay],
            ThemeCatalog.All.Select(t => t.Choice));
        Assert.Equal(
            ["Dracula", "Andromeda", "One Dark", "Nord", "Catppuccin Mocha", "Tokyo Night", "Tokyo Night Storm", "Tokyo Night Moon"],
            ThemeCatalog.All.Where(t => t.Mode == ThemeMode.Dark && t.IsNamed).Select(t => t.Name).Where(n => !n.StartsWith("GitHub")));
    }

    [Fact]
    public void Named_themes_bring_their_own_accent_and_the_defaults_keep_the_system_accent()
    {
        Assert.All(ThemeCatalog.All, t => Assert.Equal(!t.IsNamed, t.UsesSystemAccent));
        Assert.Equal("#BD93F9", ThemeCatalog.Get(ThemeChoice.Dracula).ColorsFor(dark: true).Accent.ToString(), ignoreCase: true);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Every_theme_defines_every_color(ThemeChoice choice, bool dark, DiffColorsChoice diff)
    {
        var colors = ColorsOf(choice, dark, diff);

        foreach (var property in typeof(ThemeColors).GetProperties())
        {
            var value = property.GetValue(colors);
            Assert.True(value is ThemeColor color && ThemeColor.Parse(color.ToString()) == color,
                $"{choice}/{(dark ? "dark" : "light")}/{diff}: {property.Name} = '{value}'");
        }
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Text_is_readable_on_every_background(ThemeChoice choice, bool dark, DiffColorsChoice diff)
    {
        var c = ColorsOf(choice, dark, diff);

        AssertContrast(c.Text, c.Window, 4.5, "texto/janela");
        AssertContrast(c.Text, c.Panel, 4.5, "texto/painel");
        AssertContrast(c.Text, c.Editor, 4.5, "texto/editor");
        AssertContrast(c.Text, c.Selection, 4.5, "texto/seleção");
        AssertContrast(c.TextMuted, c.Panel, 3, "texto secundário/painel");
        foreach (var line in new[] { c.DiffAddedLine, c.DiffRemovedLine, c.DiffModifiedLine })
            AssertContrast(c.Text, line, 4.5, "texto/linha do diff");

        void AssertContrast(ThemeColor fg, ThemeColor bg, double min, string what) =>
            Assert.True(Contrast(fg, bg) >= min, $"{choice}/{(dark ? "dark" : "light")}/{diff} {what}: {fg} sobre {bg} = {Contrast(fg, bg):0.00} (< {min})");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Syntax_diff_link_and_underline_colors_stand_out_from_their_background(ThemeChoice choice, bool dark, DiffColorsChoice diff)
    {
        var c = ColorsOf(choice, dark, diff);
        var onEditor = new Dictionary<string, ThemeColor>
        {
            ["tag"] = c.SyntaxTag, ["atributo"] = c.SyntaxAttribute, ["valor"] = c.SyntaxValue, ["comentário"] = c.SyntaxComment,
            ["declaração"] = c.SyntaxDeclaration, ["entidade"] = c.SyntaxEntity, ["cdata"] = c.SyntaxCData, ["link"] = c.Link,
            ["erro"] = c.IssueError, ["aviso"] = c.IssueWarning,
        };
        var onPanel = new Dictionary<string, ThemeColor>
        {
            ["entrou"] = c.DiffAdded, ["saiu"] = c.DiffRemoved, ["mudou"] = c.DiffModified, ["destaque"] = c.Accent,
        };

        var failures = onEditor.Where(kv => Contrast(kv.Value, c.Editor) < 3).Select(kv => $"{kv.Key} {kv.Value}/{c.Editor} {Contrast(kv.Value, c.Editor):0.00}")
            .Concat(onPanel.Where(kv => Contrast(kv.Value, c.Panel) < 3).Select(kv => $"{kv.Key} {kv.Value}/{c.Panel} {Contrast(kv.Value, c.Panel):0.00}"))
            .ToList();

        Assert.True(failures.Count == 0, $"{choice}/{(dark ? "dark" : "light")}/{diff}: " + string.Join("; ", failures));
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Colors_stay_readable_where_they_are_also_drawn(ThemeChoice choice, bool dark, DiffColorsChoice diff)
    {
        var c = ColorsOf(choice, dark, diff);
        var pairs = new List<(string What, ThemeColor Fg, ThemeColor Bg)> { ("texto secundário/editor (números de linha)", c.TextMuted, c.Editor) };
        foreach (var line in new[] { c.DiffAddedLine, c.DiffRemovedLine, c.DiffModifiedLine })
            foreach (var syntax in new[] { c.SyntaxTag, c.SyntaxAttribute, c.SyntaxValue, c.SyntaxComment, c.SyntaxDeclaration })
                pairs.Add(("sintaxe/linha do diff", syntax, line));

        var failures = pairs.Where(p => Contrast(p.Fg, p.Bg) < 3).Select(p => $"{p.What}: {p.Fg}/{p.Bg} {Contrast(p.Fg, p.Bg):0.00}").ToList();

        Assert.True(failures.Count == 0, $"{choice}/{(dark ? "dark" : "light")}/{diff}: " + string.Join("; ", failures));
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Text_on_the_accent_color_is_readable(ThemeChoice choice, bool dark, DiffColorsChoice diff)
    {
        var theme = ThemeCatalog.Get(choice);
        if (theme.UsesSystemAccent) return; // a cor de destaque vem do sistema operacional, não do catálogo
        var c = ColorsOf(choice, dark, diff);

        Assert.True(Contrast(c.OnAccent, c.Accent) >= 4.5, $"{choice}: {c.OnAccent} sobre {c.Accent} = {Contrast(c.OnAccent, c.Accent):0.00}");
    }

    [Fact]
    public void The_diff_colors_offer_the_theme_ones_and_the_app_schemes_in_order()
    {
        Assert.Equal(
            [DiffColorsChoice.Theme, DiffColorsChoice.GitHub, DiffColorsChoice.VSCode, DiffColorsChoice.ColorBlind,
             DiffColorsChoice.Tritanopia, DiffColorsChoice.Classic, DiffColorsChoice.HighContrast, DiffColorsChoice.Monokai,
             DiffColorsChoice.Solarized, DiffColorsChoice.Claude],
            DiffPaletteCatalog.All.Select(p => p.Choice));
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void A_diff_scheme_changes_only_the_diff_colors(ThemeChoice choice, bool dark, DiffColorsChoice diff)
    {
        var theme = ThemeCatalog.Get(choice).ColorsFor(dark);
        var colors = ColorsOf(choice, dark, diff);
        string[] diffColors =
            [nameof(ThemeColors.DiffAdded), nameof(ThemeColors.DiffRemoved), nameof(ThemeColors.DiffModified),
             nameof(ThemeColors.DiffAddedLine), nameof(ThemeColors.DiffRemovedLine), nameof(ThemeColors.DiffModifiedLine)];

        foreach (var property in typeof(ThemeColors).GetProperties())
        {
            var (before, after) = (property.GetValue(theme), property.GetValue(colors));
            if (diff == DiffColorsChoice.Theme || !diffColors.Contains(property.Name))
                Assert.Equal(before, after);
        }
        if (diff != DiffColorsChoice.Theme)
            Assert.Equal(DiffPaletteCatalog.Get(diff).ColorsFor(dark).Added, colors.DiffAdded);
    }

    [Fact]
    public void Each_named_theme_mode_matches_its_background()
    {
        Assert.All(ThemeCatalog.All.Where(t => t.Mode != ThemeMode.System), t =>
        {
            var dark = t.Mode == ThemeMode.Dark;
            Assert.Equal(dark, Luminance(t.ColorsFor(dark).Window) < 0.2);
        });
    }

    // Fórmulas da WCAG 2.x (luminância relativa e razão de contraste), independentes do código de produção.
    private static double Contrast(ThemeColor a, ThemeColor b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(ThemeColor color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
