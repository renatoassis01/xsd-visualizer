using XsdVisualizer.App.Services;

namespace XsdVisualizer.App.Themes;

public enum ThemeMode { System, Light, Dark }

/// <summary>Todas as cores de um tema, por papel na interface.</summary>
public sealed record ThemeColors(
    ThemeColor Window, ThemeColor Panel, ThemeColor Editor, ThemeColor Selection, ThemeColor Hover, ThemeColor Border,
    ThemeColor Text, ThemeColor TextMuted, ThemeColor Accent, ThemeColor OnAccent, ThemeColor Link,
    ThemeColor SyntaxTag, ThemeColor SyntaxAttribute, ThemeColor SyntaxValue, ThemeColor SyntaxComment,
    ThemeColor SyntaxDeclaration, ThemeColor SyntaxEntity, ThemeColor SyntaxCData,
    ThemeColor DiffAdded, ThemeColor DiffRemoved, ThemeColor DiffModified,
    ThemeColor DiffAddedLine, ThemeColor DiffRemovedLine, ThemeColor DiffModifiedLine, ThemeColor DiffBlankLine,
    ThemeColor IssueError, ThemeColor IssueWarning);

/// <summary>
/// Um tema da lista de Configurações. Os padrões (Sistema/Claro/Escuro) mantêm o Fluent e a cor de destaque
/// do sistema; os nomeados trazem paleta completa e cor de destaque próprias.
/// </summary>
public sealed record AppTheme(ThemeChoice Choice, string Name, ThemeMode Mode, ThemeColors Light, ThemeColors Dark)
{
    public bool IsNamed => Choice is not (ThemeChoice.System or ThemeChoice.Light or ThemeChoice.Dark);
    public bool UsesSystemAccent => !IsNamed;
    public ThemeColors ColorsFor(bool dark) => dark ? Dark : Light;
}

/// <summary>Os temas embutidos, na ordem da lista. Dados puros: nada aqui depende do Avalonia.</summary>
public static class ThemeCatalog
{
    private static readonly ThemeColors DefaultLight = new(
        Window: "#FFFFFF", Panel: "#F3F3F3", Editor: "#FFFFFF", Selection: "#CCE4F7", Hover: "#E5E5E5", Border: "#D1D1D1",
        Text: "#1B1B1B", TextMuted: "#616161", Accent: "#0067C0", OnAccent: "#FFFFFF", Link: "#0B57D0",
        SyntaxTag: "#8B008B", SyntaxAttribute: "#C4161C", SyntaxValue: "#0000FF", SyntaxComment: "#008000",
        SyntaxDeclaration: "#0000FF", SyntaxEntity: "#008080", SyntaxCData: "#0000FF",
        DiffAdded: "#1E7B34", DiffRemoved: "#C5221F", DiffModified: "#9A5800",
        DiffAddedLine: "#DDF4E4", DiffRemovedLine: "#FBE3E1", DiffModifiedLine: "#FEF3D0", DiffBlankLine: "#F3F3F3",
        IssueError: "#D32F2F", IssueWarning: "#9A5800");

    private static readonly ThemeColors DefaultDark = new(
        Window: "#202020", Panel: "#2B2B2B", Editor: "#1E1E1E", Selection: "#264F78", Hover: "#2D2D2D", Border: "#3C3C3C",
        Text: "#E4E4E4", TextMuted: "#A0A0A0", Accent: "#4CC2FF", OnAccent: "#000000", Link: "#8AB4F8",
        SyntaxTag: "#569CD6", SyntaxAttribute: "#9CDCFE", SyntaxValue: "#CE9178", SyntaxComment: "#6A9955",
        SyntaxDeclaration: "#C586C0", SyntaxEntity: "#D7BA7D", SyntaxCData: "#CE9178",
        DiffAdded: "#81C995", DiffRemoved: "#F28B82", DiffModified: "#FDD663",
        DiffAddedLine: "#163623", DiffRemovedLine: "#44201F", DiffModifiedLine: "#3A3112", DiffBlankLine: "#1C1C1C",
        IssueError: "#F48771", IssueWarning: "#CCA700");

    // GitHub: paleta Primer (github.com/primer/primitives).
    private static readonly ThemeColors GitHubLight = new(
        Window: "#FFFFFF", Panel: "#F6F8FA", Editor: "#FFFFFF", Selection: "#DDF4FF", Hover: "#EAEEF2", Border: "#D0D7DE",
        Text: "#1F2328", TextMuted: "#656D76", Accent: "#0969DA", OnAccent: "#FFFFFF", Link: "#0969DA",
        SyntaxTag: "#116329", SyntaxAttribute: "#0550AE", SyntaxValue: "#0A3069", SyntaxComment: "#6E7781",
        SyntaxDeclaration: "#CF222E", SyntaxEntity: "#0550AE", SyntaxCData: "#0A3069",
        DiffAdded: "#1A7F37", DiffRemoved: "#D1242F", DiffModified: "#9A6700",
        DiffAddedLine: "#DAFBE1", DiffRemovedLine: "#FFEBE9", DiffModifiedLine: "#FFF8C5", DiffBlankLine: "#F6F8FA",
        IssueError: "#D1242F", IssueWarning: "#9A6700");

    private static readonly ThemeColors GitHubDark = new(
        Window: "#0D1117", Panel: "#161B22", Editor: "#0D1117", Selection: "#1C2D45", Hover: "#1F242C", Border: "#30363D",
        Text: "#E6EDF3", TextMuted: "#8D96A0", Accent: "#2F81F7", OnAccent: "#0D1117", Link: "#58A6FF",
        SyntaxTag: "#7EE787", SyntaxAttribute: "#79C0FF", SyntaxValue: "#A5D6FF", SyntaxComment: "#8B949E",
        SyntaxDeclaration: "#FF7B72", SyntaxEntity: "#79C0FF", SyntaxCData: "#A5D6FF",
        DiffAdded: "#3FB950", DiffRemoved: "#F85149", DiffModified: "#D29922",
        DiffAddedLine: "#12261E", DiffRemovedLine: "#2D1619", DiffModifiedLine: "#272115", DiffBlankLine: "#161B22",
        IssueError: "#F85149", IssueWarning: "#D29922");

    // Dracula: draculatheme.com/contribute (cores oficiais).
    private static readonly ThemeColors Dracula = new(
        Window: "#282A36", Panel: "#21222C", Editor: "#282A36", Selection: "#44475A", Hover: "#343746", Border: "#44475A",
        Text: "#F8F8F2", TextMuted: "#A5ABC7", Accent: "#BD93F9", OnAccent: "#282A36", Link: "#8BE9FD",
        SyntaxTag: "#FF79C6", SyntaxAttribute: "#50FA7B", SyntaxValue: "#F1FA8C", SyntaxComment: "#7A86B8",
        SyntaxDeclaration: "#BD93F9", SyntaxEntity: "#BD93F9", SyntaxCData: "#F1FA8C",
        DiffAdded: "#50FA7B", DiffRemoved: "#FF5555", DiffModified: "#F1FA8C",
        DiffAddedLine: "#2A3C31", DiffRemovedLine: "#442A33", DiffModifiedLine: "#3A3A2B", DiffBlankLine: "#21222C",
        IssueError: "#FF5555", IssueWarning: "#FFB86C");

    // Gruvbox Light: paleta do morhetz/gruvbox (bg0, bg1… e as cores "faded" para fundo claro).
    private static readonly ThemeColors GruvboxLight = new(
        Window: "#FBF1C7", Panel: "#F2E5BC", Editor: "#FBF1C7", Selection: "#D5C4A1", Hover: "#EBDBB2", Border: "#D5C4A1",
        Text: "#3C3836", TextMuted: "#665C54", Accent: "#AF3A03", OnAccent: "#FBF1C7", Link: "#076678",
        SyntaxTag: "#9D0006", SyntaxAttribute: "#8F3F71", SyntaxValue: "#79740E", SyntaxComment: "#7C6F64",
        SyntaxDeclaration: "#076678", SyntaxEntity: "#AF3A03", SyntaxCData: "#427B58",
        DiffAdded: "#79740E", DiffRemoved: "#9D0006", DiffModified: "#955F10",
        DiffAddedLine: "#E4E7B7", DiffRemovedLine: "#F4D5C6", DiffModifiedLine: "#F6E4AE", DiffBlankLine: "#F2E5BC",
        IssueError: "#9D0006", IssueWarning: "#955F10");

    // Andromeda: EliverLara/Andromeda.
    private static readonly ThemeColors Andromeda = new(
        Window: "#23262E", Panel: "#20232B", Editor: "#23262E", Selection: "#3A3F4B", Hover: "#2E323C", Border: "#373941",
        Text: "#D5CED9", TextMuted: "#9FA0A6", Accent: "#00E8C6", OnAccent: "#23262E", Link: "#7CB7FF",
        SyntaxTag: "#FF00AA", SyntaxAttribute: "#FFE66D", SyntaxValue: "#96E072", SyntaxComment: "#A0A1A7",
        SyntaxDeclaration: "#C74DED", SyntaxEntity: "#F39C12", SyntaxCData: "#96E072",
        DiffAdded: "#96E072", DiffRemoved: "#EE5D43", DiffModified: "#FFE66D",
        DiffAddedLine: "#2A3A2E", DiffRemovedLine: "#43302C", DiffModifiedLine: "#312F26", DiffBlankLine: "#20232B",
        IssueError: "#EE5D43", IssueWarning: "#FFE66D");

    // One Dark: atom/one-dark-syntax; comentário #7F848E do One Dark Pro (o #5C6370 original fica ilegível).
    private static readonly ThemeColors OneDark = new(
        Window: "#282C34", Panel: "#21252B", Editor: "#282C34", Selection: "#3E4451", Hover: "#2C313A", Border: "#3E4451",
        Text: "#ABB2BF", TextMuted: "#8B929E", Accent: "#61AFEF", OnAccent: "#282C34", Link: "#61AFEF",
        SyntaxTag: "#E06C75", SyntaxAttribute: "#D19A66", SyntaxValue: "#98C379", SyntaxComment: "#7F848E",
        SyntaxDeclaration: "#C678DD", SyntaxEntity: "#56B6C2", SyntaxCData: "#98C379",
        DiffAdded: "#98C379", DiffRemoved: "#E06C75", DiffModified: "#E5C07B",
        DiffAddedLine: "#2E3A2F", DiffRemovedLine: "#3D2B30", DiffModifiedLine: "#3A3528", DiffBlankLine: "#21252B",
        IssueError: "#E06C75", IssueWarning: "#E5C07B");

    // One Light: atom/one-light-syntax; verde e azul um pouco mais escuros para ficarem legíveis no fundo claro.
    private static readonly ThemeColors OneLight = new(
        Window: "#FAFAFA", Panel: "#F0F0F1", Editor: "#FAFAFA", Selection: "#E5E5E6", Hover: "#EAEAEB", Border: "#DBDBDC",
        Text: "#383A42", TextMuted: "#696C77", Accent: "#3C71E3", OnAccent: "#FFFFFF", Link: "#3C71E3",
        SyntaxTag: "#E45649", SyntaxAttribute: "#986801", SyntaxValue: "#4A9649", SyntaxComment: "#696C77",
        SyntaxDeclaration: "#A626A4", SyntaxEntity: "#0184BC", SyntaxCData: "#4A9649",
        DiffAdded: "#4A9649", DiffRemoved: "#E45649", DiffModified: "#986801",
        DiffAddedLine: "#E3F1E2", DiffRemovedLine: "#FBE4E1", DiffModifiedLine: "#F6EDD5", DiffBlankLine: "#F0F0F1",
        IssueError: "#E45649", IssueWarning: "#986801");

    // Nord: nordtheme.com (Polar Night, Snow Storm, Frost, Aurora); comentário clareado para ficar legível.
    private static readonly ThemeColors Nord = new(
        Window: "#2E3440", Panel: "#292E39", Editor: "#2E3440", Selection: "#434C5E", Hover: "#3B4252", Border: "#3B4252",
        Text: "#D8DEE9", TextMuted: "#A0A9BA", Accent: "#88C0D0", OnAccent: "#2E3440", Link: "#88C0D0",
        SyntaxTag: "#81A1C1", SyntaxAttribute: "#8FBCBB", SyntaxValue: "#A3BE8C", SyntaxComment: "#8792A8",
        SyntaxDeclaration: "#B48EAD", SyntaxEntity: "#EBCB8B", SyntaxCData: "#A3BE8C",
        DiffAdded: "#A3BE8C", DiffRemoved: "#BF616A", DiffModified: "#EBCB8B",
        DiffAddedLine: "#394037", DiffRemovedLine: "#43353C", DiffModifiedLine: "#433F3A", DiffBlankLine: "#292E39",
        IssueError: "#BF616A", IssueWarning: "#EBCB8B");

    // Catppuccin Mocha: catppuccin.com/palette.
    private static readonly ThemeColors CatppuccinMocha = new(
        Window: "#1E1E2E", Panel: "#181825", Editor: "#1E1E2E", Selection: "#45475A", Hover: "#313244", Border: "#45475A",
        Text: "#CDD6F4", TextMuted: "#A6ADC8", Accent: "#CBA6F7", OnAccent: "#1E1E2E", Link: "#89B4FA",
        SyntaxTag: "#89B4FA", SyntaxAttribute: "#F9E2AF", SyntaxValue: "#A6E3A1", SyntaxComment: "#9399B2",
        SyntaxDeclaration: "#CBA6F7", SyntaxEntity: "#F2CDCD", SyntaxCData: "#A6E3A1",
        DiffAdded: "#A6E3A1", DiffRemoved: "#F38BA8", DiffModified: "#F9E2AF",
        DiffAddedLine: "#2B3B35", DiffRemovedLine: "#3F2C3A", DiffModifiedLine: "#3C3935", DiffBlankLine: "#181825",
        IssueError: "#F38BA8", IssueWarning: "#F9E2AF");

    // Catppuccin Latte: catppuccin.com/palette; amarelo, verde e overlay2 escurecidos para ficarem legíveis no fundo claro.
    private static readonly ThemeColors CatppuccinLatte = new(
        Window: "#EFF1F5", Panel: "#E6E9EF", Editor: "#EFF1F5", Selection: "#CCD0DA", Hover: "#DCE0E8", Border: "#CCD0DA",
        Text: "#4C4F69", TextMuted: "#5C5F77", Accent: "#8839EF", OnAccent: "#EFF1F5", Link: "#1E66F5",
        SyntaxTag: "#1E66F5", SyntaxAttribute: "#B07017", SyntaxValue: "#3A9027", SyntaxComment: "#7A7C90",
        SyntaxDeclaration: "#8839EF", SyntaxEntity: "#179299", SyntaxCData: "#3A9027",
        DiffAdded: "#3A9027", DiffRemoved: "#D20F39", DiffModified: "#B07017",
        DiffAddedLine: "#DCEDD9", DiffRemovedLine: "#F4D8DE", DiffModifiedLine: "#F5E8D2", DiffBlankLine: "#E6E9EF",
        IssueError: "#D20F39", IssueWarning: "#B07017");

    // Tokyo Night (Night, Storm, Moon, Day): folke/tokyonight.nvim e enkia/tokyo-night-vscode-theme; comentários clareados
    // e, no Day, texto, destaque, tag e atributo um pouco mais escuros para ficarem legíveis.
    private static readonly ThemeColors TokyoNight = new(
        Window: "#1A1B26", Panel: "#16161E", Editor: "#1A1B26", Selection: "#283457", Hover: "#1F2335", Border: "#292E42",
        Text: "#C0CAF5", TextMuted: "#A9B1D6", Accent: "#7AA2F7", OnAccent: "#1A1B26", Link: "#7DCFFF",
        SyntaxTag: "#F7768E", SyntaxAttribute: "#BB9AF7", SyntaxValue: "#9ECE6A", SyntaxComment: "#7F87B0",
        SyntaxDeclaration: "#7DCFFF", SyntaxEntity: "#FF9E64", SyntaxCData: "#9ECE6A",
        DiffAdded: "#9ECE6A", DiffRemoved: "#F7768E", DiffModified: "#E0AF68",
        DiffAddedLine: "#20303B", DiffRemovedLine: "#37222C", DiffModifiedLine: "#2E2A2D", DiffBlankLine: "#16161E",
        IssueError: "#F7768E", IssueWarning: "#E0AF68");

    private static readonly ThemeColors TokyoNightStorm = new(
        Window: "#24283B", Panel: "#1F2335", Editor: "#24283B", Selection: "#2E3C64", Hover: "#292E42", Border: "#3B4261",
        Text: "#C0CAF5", TextMuted: "#A9B1D6", Accent: "#7AA2F7", OnAccent: "#24283B", Link: "#7DCFFF",
        SyntaxTag: "#F7768E", SyntaxAttribute: "#BB9AF7", SyntaxValue: "#9ECE6A", SyntaxComment: "#7F87B0",
        SyntaxDeclaration: "#7DCFFF", SyntaxEntity: "#FF9E64", SyntaxCData: "#9ECE6A",
        DiffAdded: "#9ECE6A", DiffRemoved: "#F7768E", DiffModified: "#E0AF68",
        DiffAddedLine: "#283B4D", DiffRemovedLine: "#3F2D3D", DiffModifiedLine: "#38343D", DiffBlankLine: "#1F2335",
        IssueError: "#F7768E", IssueWarning: "#E0AF68");

    private static readonly ThemeColors TokyoNightMoon = new(
        Window: "#222436", Panel: "#1E2030", Editor: "#222436", Selection: "#2D3F76", Hover: "#2F334D", Border: "#3B4261",
        Text: "#C8D3F5", TextMuted: "#828BB8", Accent: "#82AAFF", OnAccent: "#222436", Link: "#86E1FC",
        SyntaxTag: "#FF757F", SyntaxAttribute: "#C099FF", SyntaxValue: "#C3E88D", SyntaxComment: "#828BB8",
        SyntaxDeclaration: "#86E1FC", SyntaxEntity: "#FF966C", SyntaxCData: "#C3E88D",
        DiffAdded: "#C3E88D", DiffRemoved: "#FF757F", DiffModified: "#FFC777",
        DiffAddedLine: "#2A3A3F", DiffRemovedLine: "#3A2B3A", DiffModifiedLine: "#38343A", DiffBlankLine: "#1E2030",
        IssueError: "#FF757F", IssueWarning: "#FFC777");

    private static readonly ThemeColors TokyoNightDay = new(
        Window: "#E1E2E7", Panel: "#D0D5E3", Editor: "#E1E2E7", Selection: "#CBD2EC", Hover: "#C4C8DA", Border: "#A8AECB",
        Text: "#2E54AA", TextMuted: "#6172B0", Accent: "#3760BF", OnAccent: "#FFFFFF", Link: "#007197",
        SyntaxTag: "#D81F55", SyntaxAttribute: "#8A45E8", SyntaxValue: "#587539", SyntaxComment: "#6172B0",
        SyntaxDeclaration: "#007197", SyntaxEntity: "#B15C00", SyntaxCData: "#587539",
        DiffAdded: "#587539", DiffRemoved: "#D81F55", DiffModified: "#8C6C3E",
        DiffAddedLine: "#DAE6DE", DiffRemovedLine: "#F2E1E7", DiffModifiedLine: "#E9E2DA", DiffBlankLine: "#D0D5E3",
        IssueError: "#F52A65", IssueWarning: "#8C6C3E");

    // Índigo: base slate (neutros frios) com destaque índigo; claro e escuro, segue o sistema.
    private static readonly ThemeColors IndigoLight = new(
        Window: "#F8FAFC", Panel: "#F1F5F9", Editor: "#FFFFFF", Selection: "#E0E7FF", Hover: "#EEF2FF", Border: "#E2E8F0",
        Text: "#0F172A", TextMuted: "#5B6B82", Accent: "#4F46E5", OnAccent: "#FFFFFF", Link: "#4338CA",
        SyntaxTag: "#3730A3", SyntaxAttribute: "#0E7490", SyntaxValue: "#9D174D", SyntaxComment: "#64748B",
        SyntaxDeclaration: "#6D28D9", SyntaxEntity: "#0E7490", SyntaxCData: "#9D174D",
        DiffAdded: "#15803D", DiffRemoved: "#B91C1C", DiffModified: "#B45309",
        DiffAddedLine: "#F0FDF4", DiffRemovedLine: "#FEF2F2", DiffModifiedLine: "#FFFBEB", DiffBlankLine: "#F8FAFC",
        IssueError: "#DC2626", IssueWarning: "#B45309");

    private static readonly ThemeColors IndigoDark = new(
        Window: "#0F172A", Panel: "#0B1220", Editor: "#0F172A", Selection: "#312E81", Hover: "#1E293B", Border: "#334155",
        Text: "#E2E8F0", TextMuted: "#94A3B8", Accent: "#818CF8", OnAccent: "#0F172A", Link: "#A5B4FC",
        SyntaxTag: "#A5B4FC", SyntaxAttribute: "#67E8F9", SyntaxValue: "#F9A8D4", SyntaxComment: "#94A3B8",
        SyntaxDeclaration: "#C4B5FD", SyntaxEntity: "#67E8F9", SyntaxCData: "#F9A8D4",
        DiffAdded: "#4ADE80", DiffRemoved: "#F87171", DiffModified: "#FBBF24",
        DiffAddedLine: "#0F2A22", DiffRemovedLine: "#2F1A22", DiffModifiedLine: "#2B2415", DiffBlankLine: "#0B1220",
        IssueError: "#F87171", IssueWarning: "#FBBF24");

    public static IReadOnlyList<AppTheme> All { get; } =
    [
        new(ThemeChoice.System, "System", ThemeMode.System, DefaultLight, DefaultDark),
        new(ThemeChoice.Light, "Light", ThemeMode.Light, DefaultLight, DefaultLight),
        new(ThemeChoice.Dark, "Dark", ThemeMode.Dark, DefaultDark, DefaultDark),
        new(ThemeChoice.Indigo, "Indigo", ThemeMode.System, IndigoLight, IndigoDark),
        new(ThemeChoice.GitHubLight, "GitHub Light", ThemeMode.Light, GitHubLight, GitHubLight),
        new(ThemeChoice.GitHubDark, "GitHub Dark", ThemeMode.Dark, GitHubDark, GitHubDark),
        new(ThemeChoice.Dracula, "Dracula", ThemeMode.Dark, Dracula, Dracula),
        new(ThemeChoice.GruvboxLight, "Gruvbox Light", ThemeMode.Light, GruvboxLight, GruvboxLight),
        new(ThemeChoice.Andromeda, "Andromeda", ThemeMode.Dark, Andromeda, Andromeda),
        new(ThemeChoice.OneLight, "One Light", ThemeMode.Light, OneLight, OneLight),
        new(ThemeChoice.OneDark, "One Dark", ThemeMode.Dark, OneDark, OneDark),
        new(ThemeChoice.Nord, "Nord", ThemeMode.Dark, Nord, Nord),
        new(ThemeChoice.CatppuccinLatte, "Catppuccin Latte", ThemeMode.Light, CatppuccinLatte, CatppuccinLatte),
        new(ThemeChoice.CatppuccinMocha, "Catppuccin Mocha", ThemeMode.Dark, CatppuccinMocha, CatppuccinMocha),
        new(ThemeChoice.TokyoNight, "Tokyo Night", ThemeMode.Dark, TokyoNight, TokyoNight),
        new(ThemeChoice.TokyoNightStorm, "Tokyo Night Storm", ThemeMode.Dark, TokyoNightStorm, TokyoNightStorm),
        new(ThemeChoice.TokyoNightMoon, "Tokyo Night Moon", ThemeMode.Dark, TokyoNightMoon, TokyoNightMoon),
        new(ThemeChoice.TokyoNightDay, "Tokyo Night Day", ThemeMode.Light, TokyoNightDay, TokyoNightDay),
    ];

    public static AppTheme Get(ThemeChoice choice) => All.FirstOrDefault(t => t.Choice == choice) ?? All[0];
}
