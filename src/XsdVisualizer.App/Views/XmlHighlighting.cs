using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;

namespace XsdVisualizer.App.Views;

/// <summary>
/// Cores do destaque de XML conforme o tema: as cores padrão do AvaloniaEdit são pensadas para fundo claro
/// e somem no escuro, então no tema escuro trocamos por uma paleta de alto contraste.
/// </summary>
internal static class XmlHighlighting
{
    private static readonly Dictionary<string, Color> Dark = new()
    {
        ["Comment"] = Color.Parse("#6A9955"),
        ["CData"] = Color.Parse("#CE9178"),
        ["DocType"] = Color.Parse("#808080"),
        ["XmlDeclaration"] = Color.Parse("#C586C0"),
        ["XmlTag"] = Color.Parse("#569CD6"),
        ["AttributeName"] = Color.Parse("#9CDCFE"),
        ["AttributeValue"] = Color.Parse("#CE9178"),
        ["Entity"] = Color.Parse("#D7BA7D"),
        ["BrokenEntity"] = Color.Parse("#F44747"),
    };

    private static Dictionary<string, HighlightingBrush?>? _light;

    // URLs viram links no editor; o azul padrão do AvaloniaEdit é forte demais no fundo escuro.
    private static readonly IBrush LightLink = new SolidColorBrush(Color.Parse("#0B57D0"));
    private static readonly IBrush DarkLink = new SolidColorBrush(Color.Parse("#8AB4F8"));

    public static IBrush LinkBrush(ThemeVariant? variant) => variant == ThemeVariant.Dark ? DarkLink : LightLink;

    public static IHighlightingDefinition Definition => HighlightingManager.Instance.GetDefinition("XML");

    public static void Apply(ThemeVariant? variant)
    {
        var definition = Definition;
        _light ??= definition.NamedHighlightingColors.ToDictionary(c => c.Name, c => (HighlightingBrush?)c.Foreground);
        var dark = variant == ThemeVariant.Dark;
        foreach (var color in definition.NamedHighlightingColors)
            color.Foreground = dark && Dark.TryGetValue(color.Name, out var c)
                ? new SimpleHighlightingBrush(c)
                : _light.GetValueOrDefault(color.Name);
    }
}
