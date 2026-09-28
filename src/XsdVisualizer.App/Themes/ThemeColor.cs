using System.Globalization;

namespace XsdVisualizer.App.Themes;

/// <summary>Uma cor RGB de tema, interpretada uma vez a partir de "#RRGGBB". Não depende do Avalonia.</summary>
public readonly record struct ThemeColor(byte R, byte G, byte B)
{
    public static ThemeColor Parse(string hex)
    {
        if (hex is not { Length: 7 } || hex[0] != '#' || !int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, null, out var rgb))
            throw new FormatException($"Cor de tema inválida: '{hex}' (esperado #RRGGBB).");
        return new ThemeColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    /// <summary>As paletas do catálogo são escritas em "#RRGGBB".</summary>
    public static implicit operator ThemeColor(string hex) => Parse(hex);

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
