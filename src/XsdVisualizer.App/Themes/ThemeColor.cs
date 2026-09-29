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

    /// <summary>Mistura com outra cor: 0 é esta, 1 é a outra.</summary>
    public ThemeColor Mix(ThemeColor other, double amount)
    {
        byte Channel(byte from, byte to) => (byte)Math.Round(from + (to - from) * amount);
        return new ThemeColor(Channel(R, other.R), Channel(G, other.G), Channel(B, other.B));
    }

    /// <summary>Razão de contraste WCAG 2.x entre as duas cores (1 a 21).</summary>
    public double ContrastWith(ThemeColor other)
    {
        var (a, b) = (Luminance(), other.Luminance());
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private double Luminance()
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(R) + 0.7152 * Channel(G) + 0.0722 * Channel(B);
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}
