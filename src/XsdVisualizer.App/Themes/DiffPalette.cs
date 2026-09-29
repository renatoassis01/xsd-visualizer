using XsdVisualizer.App.Services;

namespace XsdVisualizer.App.Themes;

/// <summary>As três cores de um esquema de diff: entrou, saiu, mudou.</summary>
public sealed record DiffHues(ThemeColor Added, ThemeColor Removed, ThemeColor Modified);

/// <summary>
/// Cores do diff da Comparação. "Do tema" mantém as do tema; os demais esquemas trocam só as cores do diff, em
/// qualquer tema: o texto usa a cor do esquema e o fundo da linha é o fundo do editor tingido com ela.
/// </summary>
public sealed record DiffPalette(DiffColorsChoice Choice, string Name, DiffHues? Light, DiffHues? Dark)
{
    private static readonly ThemeColor Black = "#000000";
    private static readonly ThemeColor White = "#FFFFFF";

    public bool FollowsTheme => Light is null || Dark is null;

    /// <summary>As cores do esquema para o modo (null em "Do tema").</summary>
    public DiffHues? ColorsFor(bool dark) => dark ? Dark : Light;

    public ThemeColors ApplyTo(ThemeColors theme, bool dark)
    {
        if (ColorsFor(dark) is not { } hues) return theme;
        // A base da linha afasta do texto (mais escura no tema escuro, mais clara no claro) antes do tingimento.
        var lineBase = dark ? theme.Editor.Mix(Black, 0.35) : theme.Editor.Mix(White, 0.8);
        var tint = Tint(theme, lineBase, hues);
        return theme with
        {
            DiffAdded = hues.Added,
            DiffRemoved = hues.Removed,
            DiffModified = hues.Modified,
            DiffAddedLine = lineBase.Mix(hues.Added, tint),
            DiffRemovedLine = lineBase.Mix(hues.Removed, tint),
            DiffModifiedLine = lineBase.Mix(hues.Modified, tint),
        };
    }

    private const double MaxTint = 0.18;
    private const double MinTint = 0.10;

    /// <summary>
    /// O tingimento mais forte (até 18%) em que texto e sintaxe do tema seguem legíveis nas três linhas; temas com
    /// cores de sintaxe no limite (One Light, Catppuccin Latte, Tokyo Night Day) ficam mais claros. Nunca abaixo de 10%.
    /// </summary>
    private static double Tint(ThemeColors theme, ThemeColor lineBase, DiffHues hues)
    {
        ThemeColor[] syntax = [theme.SyntaxTag, theme.SyntaxAttribute, theme.SyntaxValue, theme.SyntaxComment, theme.SyntaxDeclaration];
        for (var tint = MaxTint; tint > MinTint; tint -= 0.01)
        {
            var lines = new[] { hues.Added, hues.Removed, hues.Modified }.Select(h => lineBase.Mix(h, tint));
            if (lines.All(line => theme.Text.ContrastWith(line) >= 4.5 && syntax.All(s => s.ContrastWith(line) >= 3)))
                return tint;
        }
        return MinTint;
    }
}

/// <summary>Os esquemas de diff, na ordem da lista. Dados puros: nada aqui depende do Avalonia.</summary>
public static class DiffPaletteCatalog
{
    public static IReadOnlyList<DiffPalette> All { get; } =
    [
        new(DiffColorsChoice.Theme, "Theme", null, null),
        // GitHub: paleta Primer (fg.success, fg.danger, fg.attention).
        new(DiffColorsChoice.GitHub, "GitHub",
            new("#1A7F37", "#D1242F", "#9A6700"), new("#3FB950", "#F85149", "#D29922")),
        // VS Code: gitDecoration (added, deleted, modified); o vermelho escuro clareado para ficar legível nos painéis.
        new(DiffColorsChoice.VSCode, "VS Code",
            new("#587C0C", "#AD0707", "#895503"), new("#81B88B", "#D2584A", "#E2C08D")),
        // Daltônico: azul entrou, laranja saiu (esquema do GitHub para protanopia e deuteranopia).
        new(DiffColorsChoice.ColorBlind, "Color blind",
            new("#0969DA", "#BC4C00", "#9A6700"), new("#58A6FF", "#DB6D28", "#D29922")),
        // Tritanopia (daltonismo azul/amarelo): azul entrou, vermelho saiu, roxo mudou (Primer, tema tritanopia).
        new(DiffColorsChoice.Tritanopia, "Tritanopia",
            new("#0969DA", "#CF222E", "#8250DF"), new("#58A6FF", "#FF7B72", "#A371F7")),
        // Clássico: mudou em azul, como no Eclipse e no Beyond Compare (o âmbar é o mais difícil de ler em fundo claro).
        new(DiffColorsChoice.Classic, "Classic",
            new("#1A7F37", "#CF222E", "#0969DA"), new("#3FB950", "#F85149", "#58A6FF")),
        // Alto contraste: cores saturadas; o verde do escuro um pouco menos claro para a sintaxe seguir legível.
        new(DiffColorsChoice.HighContrast, "High contrast",
            new("#006B1A", "#B3001B", "#0040C0"), new("#51E06D", "#FF6B6B", "#6CB6FF")),
        // Monokai: verde, rosa e amarelo do Monokai; versões mais escuras no claro e o verde e o amarelo do escuro
        // levemente escurecidos para a sintaxe seguir legível.
        new(DiffColorsChoice.Monokai, "Monokai",
            new("#588400", "#D01B5C", "#8A6D00"), new("#9ED72C", "#F92672", "#CFC568")),
        // Solarized: acentos de Ethan Schoonover; verde e amarelo escurecidos no claro, vermelho clareado no escuro.
        new(DiffColorsChoice.Solarized, "Solarized",
            new("#6E7F00", "#DC322F", "#967200"), new("#859900", "#DD3633", "#B58900")),
        // Claude: verde e vermelho do diff do Claude Code e o laranja da marca Claude para "mudou" (o Claude Code não
        // tem cor de alteração); no claro, verde e laranja escurecidos, no escuro, o vermelho levemente clareado.
        new(DiffColorsChoice.Claude, "Claude",
            new("#29893B", "#D1454B", "#B06247"), new("#38A660", "#B45B6C", "#D77757")),
    ];

    public static DiffPalette Get(DiffColorsChoice choice) => All.FirstOrDefault(p => p.Choice == choice) ?? All[0];
}
