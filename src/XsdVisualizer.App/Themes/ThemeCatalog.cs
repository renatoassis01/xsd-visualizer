using XsdVisualizer.App.Services;

namespace XsdVisualizer.App.Themes;

public enum ThemeMode { System, Light, Dark }

/// <summary>Todas as cores de um tema, em "#RRGGBB", por papel na interface.</summary>
public sealed record ThemeColors(
    string Window, string Panel, string Editor, string Selection, string Hover, string Border,
    string Text, string TextMuted, string Accent, string Link,
    string SyntaxTag, string SyntaxAttribute, string SyntaxValue, string SyntaxComment,
    string SyntaxDeclaration, string SyntaxEntity, string SyntaxCData,
    string DiffAdded, string DiffRemoved, string DiffModified,
    string DiffAddedLine, string DiffRemovedLine, string DiffModifiedLine, string DiffBlankLine,
    string IssueError, string IssueWarning);

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
        Text: "#1B1B1B", TextMuted: "#616161", Accent: "#0067C0", Link: "#0B57D0",
        SyntaxTag: "#8B008B", SyntaxAttribute: "#C4161C", SyntaxValue: "#0000FF", SyntaxComment: "#008000",
        SyntaxDeclaration: "#0000FF", SyntaxEntity: "#008080", SyntaxCData: "#0000FF",
        DiffAdded: "#1E7B34", DiffRemoved: "#C5221F", DiffModified: "#9A5800",
        DiffAddedLine: "#DDF4E4", DiffRemovedLine: "#FBE3E1", DiffModifiedLine: "#FEF3D0", DiffBlankLine: "#F3F3F3",
        IssueError: "#D32F2F", IssueWarning: "#9A5800");

    private static readonly ThemeColors DefaultDark = new(
        Window: "#202020", Panel: "#2B2B2B", Editor: "#1E1E1E", Selection: "#264F78", Hover: "#2D2D2D", Border: "#3C3C3C",
        Text: "#E4E4E4", TextMuted: "#A0A0A0", Accent: "#4CC2FF", Link: "#8AB4F8",
        SyntaxTag: "#569CD6", SyntaxAttribute: "#9CDCFE", SyntaxValue: "#CE9178", SyntaxComment: "#6A9955",
        SyntaxDeclaration: "#C586C0", SyntaxEntity: "#D7BA7D", SyntaxCData: "#CE9178",
        DiffAdded: "#81C995", DiffRemoved: "#F28B82", DiffModified: "#FDD663",
        DiffAddedLine: "#1E4D2E", DiffRemovedLine: "#5A2424", DiffModifiedLine: "#5A4A14", DiffBlankLine: "#1C1C1C",
        IssueError: "#F48771", IssueWarning: "#CCA700");

    // GitHub: paleta Primer (github.com/primer/primitives).
    private static readonly ThemeColors GitHubLight = new(
        Window: "#FFFFFF", Panel: "#F6F8FA", Editor: "#FFFFFF", Selection: "#DDF4FF", Hover: "#EAEEF2", Border: "#D0D7DE",
        Text: "#1F2328", TextMuted: "#656D76", Accent: "#0969DA", Link: "#0969DA",
        SyntaxTag: "#116329", SyntaxAttribute: "#0550AE", SyntaxValue: "#0A3069", SyntaxComment: "#6E7781",
        SyntaxDeclaration: "#CF222E", SyntaxEntity: "#0550AE", SyntaxCData: "#0A3069",
        DiffAdded: "#1A7F37", DiffRemoved: "#D1242F", DiffModified: "#9A6700",
        DiffAddedLine: "#DAFBE1", DiffRemovedLine: "#FFEBE9", DiffModifiedLine: "#FFF8C5", DiffBlankLine: "#F6F8FA",
        IssueError: "#D1242F", IssueWarning: "#9A6700");

    private static readonly ThemeColors GitHubDark = new(
        Window: "#0D1117", Panel: "#161B22", Editor: "#0D1117", Selection: "#1C2D45", Hover: "#1F242C", Border: "#30363D",
        Text: "#E6EDF3", TextMuted: "#8D96A0", Accent: "#2F81F7", Link: "#58A6FF",
        SyntaxTag: "#7EE787", SyntaxAttribute: "#79C0FF", SyntaxValue: "#A5D6FF", SyntaxComment: "#8B949E",
        SyntaxDeclaration: "#FF7B72", SyntaxEntity: "#79C0FF", SyntaxCData: "#A5D6FF",
        DiffAdded: "#3FB950", DiffRemoved: "#F85149", DiffModified: "#D29922",
        DiffAddedLine: "#12261E", DiffRemovedLine: "#2D1619", DiffModifiedLine: "#272115", DiffBlankLine: "#161B22",
        IssueError: "#F85149", IssueWarning: "#D29922");

    // Dracula: draculatheme.com/contribute (cores oficiais).
    private static readonly ThemeColors Dracula = new(
        Window: "#282A36", Panel: "#21222C", Editor: "#282A36", Selection: "#44475A", Hover: "#343746", Border: "#44475A",
        Text: "#F8F8F2", TextMuted: "#A5ABC7", Accent: "#BD93F9", Link: "#8BE9FD",
        SyntaxTag: "#FF79C6", SyntaxAttribute: "#50FA7B", SyntaxValue: "#F1FA8C", SyntaxComment: "#7A86B8",
        SyntaxDeclaration: "#BD93F9", SyntaxEntity: "#BD93F9", SyntaxCData: "#F1FA8C",
        DiffAdded: "#50FA7B", DiffRemoved: "#FF5555", DiffModified: "#F1FA8C",
        DiffAddedLine: "#2B4234", DiffRemovedLine: "#4D2A34", DiffModifiedLine: "#454532", DiffBlankLine: "#21222C",
        IssueError: "#FF5555", IssueWarning: "#FFB86C");

    // Gruvbox Light: paleta do morhetz/gruvbox (bg0, bg1… e as cores "faded" para fundo claro).
    private static readonly ThemeColors GruvboxLight = new(
        Window: "#FBF1C7", Panel: "#F2E5BC", Editor: "#FBF1C7", Selection: "#D5C4A1", Hover: "#EBDBB2", Border: "#D5C4A1",
        Text: "#3C3836", TextMuted: "#665C54", Accent: "#AF3A03", Link: "#076678",
        SyntaxTag: "#9D0006", SyntaxAttribute: "#8F3F71", SyntaxValue: "#79740E", SyntaxComment: "#7C6F64",
        SyntaxDeclaration: "#076678", SyntaxEntity: "#AF3A03", SyntaxCData: "#427B58",
        DiffAdded: "#79740E", DiffRemoved: "#9D0006", DiffModified: "#955F10",
        DiffAddedLine: "#E4E7B7", DiffRemovedLine: "#F4D5C6", DiffModifiedLine: "#F6E4AE", DiffBlankLine: "#F2E5BC",
        IssueError: "#9D0006", IssueWarning: "#955F10");

    // Andromeda: EliverLara/Andromeda.
    private static readonly ThemeColors Andromeda = new(
        Window: "#23262E", Panel: "#20232B", Editor: "#23262E", Selection: "#3A3F4B", Hover: "#2E323C", Border: "#373941",
        Text: "#D5CED9", TextMuted: "#9FA0A6", Accent: "#00E8C6", Link: "#7CB7FF",
        SyntaxTag: "#FF00AA", SyntaxAttribute: "#FFE66D", SyntaxValue: "#96E072", SyntaxComment: "#A0A1A7",
        SyntaxDeclaration: "#C74DED", SyntaxEntity: "#F39C12", SyntaxCData: "#96E072",
        DiffAdded: "#96E072", DiffRemoved: "#EE5D43", DiffModified: "#FFE66D",
        DiffAddedLine: "#2A3A2E", DiffRemovedLine: "#43302C", DiffModifiedLine: "#3F3D2A", DiffBlankLine: "#20232B",
        IssueError: "#EE5D43", IssueWarning: "#FFE66D");

    public static IReadOnlyList<AppTheme> All { get; } =
    [
        new(ThemeChoice.System, "System", ThemeMode.System, DefaultLight, DefaultDark),
        new(ThemeChoice.Light, "Light", ThemeMode.Light, DefaultLight, DefaultLight),
        new(ThemeChoice.Dark, "Dark", ThemeMode.Dark, DefaultDark, DefaultDark),
        new(ThemeChoice.GitHubLight, "GitHub Light", ThemeMode.Light, GitHubLight, GitHubLight),
        new(ThemeChoice.GitHubDark, "GitHub Dark", ThemeMode.Dark, GitHubDark, GitHubDark),
        new(ThemeChoice.Dracula, "Dracula", ThemeMode.Dark, Dracula, Dracula),
        new(ThemeChoice.GruvboxLight, "Gruvbox Light", ThemeMode.Light, GruvboxLight, GruvboxLight),
        new(ThemeChoice.Andromeda, "Andromeda", ThemeMode.Dark, Andromeda, Andromeda),
    ];

    public static AppTheme Get(ThemeChoice choice) => All.FirstOrDefault(t => t.Choice == choice) ?? All[0];
}
