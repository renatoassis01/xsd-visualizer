using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using XsdVisualizer.App.Themes;

namespace XsdVisualizer.App.Views;

/// <summary>Cores do destaque de XML (e dos links) conforme o tema ativo.</summary>
internal static class XmlHighlighting
{
    public static IHighlightingDefinition Definition => HighlightingManager.Instance.GetDefinition("XML");

    public static void Apply(ThemeColors c)
    {
        var palette = new Dictionary<string, string>
        {
            ["Comment"] = c.SyntaxComment,
            ["CData"] = c.SyntaxCData,
            ["DocType"] = c.SyntaxComment,
            ["XmlDeclaration"] = c.SyntaxDeclaration,
            ["XmlTag"] = c.SyntaxTag,
            ["AttributeName"] = c.SyntaxAttribute,
            ["AttributeValue"] = c.SyntaxValue,
            ["Entity"] = c.SyntaxEntity,
            ["BrokenEntity"] = c.IssueError,
        };
        foreach (var color in Definition.NamedHighlightingColors)
            if (palette.TryGetValue(color.Name, out var hex))
                color.Foreground = new SimpleHighlightingBrush(Color.Parse(hex));
    }

    public static IBrush LinkBrush(ThemeColors c) => ThemeApplier.Brush(c.Link);
}
