using System.Globalization;
using XsdVisualizer.App.Services;
using XsdVisualizer.App.Themes;

namespace XsdVisualizer.App.Tests;

public class ThemeCatalogTests
{
    public static TheoryData<ThemeChoice, bool> Palettes()
    {
        var data = new TheoryData<ThemeChoice, bool>();
        foreach (var theme in ThemeCatalog.All)
            foreach (var dark in theme.Mode == ThemeMode.System ? new[] { false, true } : new[] { theme.Mode == ThemeMode.Dark })
                data.Add(theme.Choice, dark);
        return data;
    }

    [Fact]
    public void The_list_offers_system_light_dark_and_the_named_themes_in_order()
    {
        Assert.Equal(
            [ThemeChoice.System, ThemeChoice.Light, ThemeChoice.Dark, ThemeChoice.GitHubLight, ThemeChoice.GitHubDark,
             ThemeChoice.Dracula, ThemeChoice.GruvboxLight, ThemeChoice.Andromeda],
            ThemeCatalog.All.Select(t => t.Choice));
        Assert.Equal(["Dracula", "Andromeda"], ThemeCatalog.All.Where(t => t.Mode == ThemeMode.Dark && t.IsNamed).Select(t => t.Name).Where(n => !n.StartsWith("GitHub")));
    }

    [Fact]
    public void Named_themes_bring_their_own_accent_and_the_defaults_keep_the_system_accent()
    {
        Assert.All(ThemeCatalog.All, t => Assert.Equal(!t.IsNamed, t.UsesSystemAccent));
        Assert.Equal("#BD93F9", ThemeCatalog.Get(ThemeChoice.Dracula).ColorsFor(dark: true).Accent, ignoreCase: true);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Every_theme_defines_every_color_as_a_hex_value(ThemeChoice choice, bool dark)
    {
        var colors = ThemeCatalog.Get(choice).ColorsFor(dark);

        foreach (var property in typeof(ThemeColors).GetProperties())
        {
            var value = property.GetValue(colors) as string;
            Assert.True(value is { Length: 7 } && value[0] == '#' && int.TryParse(value[1..], NumberStyles.HexNumber, null, out _),
                $"{choice}/{(dark ? "dark" : "light")}: {property.Name} = '{value}'");
        }
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Text_is_readable_on_every_background(ThemeChoice choice, bool dark)
    {
        var c = ThemeCatalog.Get(choice).ColorsFor(dark);

        AssertContrast(c.Text, c.Window, 4.5, "texto/janela");
        AssertContrast(c.Text, c.Panel, 4.5, "texto/painel");
        AssertContrast(c.Text, c.Editor, 4.5, "texto/editor");
        AssertContrast(c.Text, c.Selection, 4.5, "texto/seleção");
        AssertContrast(c.TextMuted, c.Panel, 3, "texto secundário/painel");
        foreach (var line in new[] { c.DiffAddedLine, c.DiffRemovedLine, c.DiffModifiedLine })
            AssertContrast(c.Text, line, 4.5, "texto/linha do diff");

        void AssertContrast(string fg, string bg, double min, string what) =>
            Assert.True(Contrast(fg, bg) >= min, $"{choice}/{(dark ? "dark" : "light")} {what}: {fg} sobre {bg} = {Contrast(fg, bg):0.00} (< {min})");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Syntax_diff_link_and_underline_colors_stand_out_from_their_background(ThemeChoice choice, bool dark)
    {
        var c = ThemeCatalog.Get(choice).ColorsFor(dark);
        var onEditor = new Dictionary<string, string>
        {
            ["tag"] = c.SyntaxTag, ["atributo"] = c.SyntaxAttribute, ["valor"] = c.SyntaxValue, ["comentário"] = c.SyntaxComment,
            ["declaração"] = c.SyntaxDeclaration, ["entidade"] = c.SyntaxEntity, ["cdata"] = c.SyntaxCData, ["link"] = c.Link,
            ["erro"] = c.IssueError, ["aviso"] = c.IssueWarning,
        };
        var onPanel = new Dictionary<string, string>
        {
            ["entrou"] = c.DiffAdded, ["saiu"] = c.DiffRemoved, ["mudou"] = c.DiffModified, ["destaque"] = c.Accent,
        };

        var failures = onEditor.Where(kv => Contrast(kv.Value, c.Editor) < 3).Select(kv => $"{kv.Key} {kv.Value}/{c.Editor} {Contrast(kv.Value, c.Editor):0.00}")
            .Concat(onPanel.Where(kv => Contrast(kv.Value, c.Panel) < 3).Select(kv => $"{kv.Key} {kv.Value}/{c.Panel} {Contrast(kv.Value, c.Panel):0.00}"))
            .ToList();

        Assert.True(failures.Count == 0, $"{choice}/{(dark ? "dark" : "light")}: " + string.Join("; ", failures));
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Colors_stay_readable_where_they_are_also_drawn(ThemeChoice choice, bool dark)
    {
        var c = ThemeCatalog.Get(choice).ColorsFor(dark);
        var pairs = new List<(string What, string Fg, string Bg)> { ("texto secundário/editor (números de linha)", c.TextMuted, c.Editor) };
        foreach (var line in new[] { c.DiffAddedLine, c.DiffRemovedLine, c.DiffModifiedLine })
            foreach (var syntax in new[] { c.SyntaxTag, c.SyntaxAttribute, c.SyntaxValue, c.SyntaxComment, c.SyntaxDeclaration })
                pairs.Add(("sintaxe/linha do diff", syntax, line));

        var failures = pairs.Where(p => Contrast(p.Fg, p.Bg) < 3).Select(p => $"{p.What}: {p.Fg}/{p.Bg} {Contrast(p.Fg, p.Bg):0.00}").ToList();

        Assert.True(failures.Count == 0, $"{choice}/{(dark ? "dark" : "light")}: " + string.Join("; ", failures));
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Text_on_the_accent_color_is_readable(ThemeChoice choice, bool dark)
    {
        var theme = ThemeCatalog.Get(choice);
        if (theme.UsesSystemAccent) return; // a cor de destaque vem do sistema operacional, não do catálogo
        var c = theme.ColorsFor(dark);

        Assert.True(Contrast(c.OnAccent, c.Accent) >= 4.5, $"{choice}: {c.OnAccent} sobre {c.Accent} = {Contrast(c.OnAccent, c.Accent):0.00}");
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
    private static double Contrast(string a, string b)
    {
        var (la, lb) = (Luminance(a), Luminance(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int i)
        {
            var c = int.Parse(hex.Substring(1 + i * 2, 2), NumberStyles.HexNumber) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
    }
}
