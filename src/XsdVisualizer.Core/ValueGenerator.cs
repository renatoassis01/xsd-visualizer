using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

/// <summary>Gera valores determinísticos que satisfazem um tipo simples (facets incluídos).</summary>
internal sealed class ValueGenerator
{
    private const int MaxCandidates = 400;
    private readonly NameTable _nameTable = new();
    private readonly XmlNamespaceManager _namespaces;
    private readonly Dictionary<string, XsdRegexGenerator?> _regexCache = new();

    public ValueGenerator() => _namespaces = new XmlNamespaceManager(_nameTable);

    /// <param name="pickEnumeration">Escolhe um valor de enumeração (null = primeiro).</param>
    public string Generate(XmlSchemaSimpleType type, string name, int variant, Func<IReadOnlyList<string>, string?> pickEnumeration)
    {
        var constraints = Constraints.Of(type);
        if (constraints.Enumerations.Count > 0)
            return pickEnumeration(constraints.Enumerations) ?? constraints.Enumerations[0];

        string? fallback = null;
        foreach (var candidate in Candidates(type, constraints, name, variant).Take(MaxCandidates))
        {
            fallback ??= candidate;
            if (IsValid(type, candidate)) return candidate;
        }
        return fallback ?? name;
    }

    public bool IsValid(XmlSchemaSimpleType type, string value)
    {
        try
        {
            type.Datatype!.ParseValue(value, _nameTable, _namespaces);
            return true;
        }
        catch (Exception e) when (e is XmlSchemaException or FormatException or OverflowException or InvalidCastException)
        {
            return false;
        }
    }

    private IEnumerable<string> Candidates(XmlSchemaSimpleType type, Constraints constraints, string name, int variant)
    {
        switch (type.Datatype!.Variety)
        {
            case XmlSchemaDatatypeVariety.List when ListItemType(type) is { } item:
                // Um item só; se houver restrições de tamanho da lista, repete o item.
                var one = Generate(item, name, variant, _ => null);
                var count = Math.Max(1, constraints.MinLength ?? constraints.Length ?? 1);
                yield return string.Join(' ', Enumerable.Repeat(one, count));
                yield break;
            case XmlSchemaDatatypeVariety.Union when UnionMembers(type) is { Count: > 0 } members:
                foreach (var member in members)
                    yield return Generate(member, name, variant, _ => null);
                yield break;
        }

        if (constraints.Patterns.Count > 0)
        {
            foreach (var candidate in PatternCandidates(constraints, name, variant))
                yield return candidate;
            yield break;
        }

        foreach (var candidate in BuiltInCandidates(BuiltInCode(type), constraints, name, variant))
            yield return candidate;
    }

    private IEnumerable<string> PatternCandidates(Constraints constraints, string name, int variant)
    {
        yield return name;
        var generators = constraints.Patterns
            .Select(p => _regexCache.TryGetValue(p, out var g) ? g : _regexCache[p] = XsdRegexGenerator.TryParse(p))
            .OfType<XsdRegexGenerator>()
            .ToList();
        int[] repetitions =
        [
            1, 0, 2, 3, 5, 8,
            .. new[] { constraints.Length, constraints.MinLength, constraints.MaxLength, constraints.TotalDigits }
                .OfType<int>().SelectMany(n => new[] { n, n - 1, n - 2 }).Where(n => n > 0),
            13, 21, 34,
        ];
        foreach (var generator in generators)
            for (var attempt = 0; attempt < 8; attempt++)
                foreach (var r in repetitions)
                {
                    string value;
                    try { value = generator.Generate(variant + attempt, r); }
                    catch (InvalidOperationException) { yield break; }
                    yield return value;
                }
    }

    private IEnumerable<string> BuiltInCandidates(XmlTypeCode code, Constraints c, string name, int variant)
    {
        var n = variant + 1;
        switch (code)
        {
            case XmlTypeCode.Boolean:
                yield return variant % 2 == 0 ? "true" : "false";
                yield return variant % 2 == 0 ? "false" : "true";
                break;
            case XmlTypeCode.Decimal or XmlTypeCode.Float or XmlTypeCode.Double:
                foreach (var v in NumericCandidates(c, n, fractional: true)) yield return v;
                break;
            case XmlTypeCode.Integer or XmlTypeCode.Long or XmlTypeCode.Int or XmlTypeCode.Short or XmlTypeCode.Byte
                or XmlTypeCode.NonNegativeInteger or XmlTypeCode.PositiveInteger or XmlTypeCode.NonPositiveInteger
                or XmlTypeCode.NegativeInteger or XmlTypeCode.UnsignedLong or XmlTypeCode.UnsignedInt
                or XmlTypeCode.UnsignedShort or XmlTypeCode.UnsignedByte:
                foreach (var v in NumericCandidates(c, n, fractional: false)) yield return v;
                yield return (-n).ToString(CultureInfo.InvariantCulture);
                break;
            case XmlTypeCode.Date:
                yield return new DateTime(2026, 1, 1).AddDays(variant).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                break;
            case XmlTypeCode.DateTime:
                yield return new DateTime(2026, 1, 1, 10, 30, 0).AddDays(variant).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "-03:00";
                break;
            case XmlTypeCode.Time:
                yield return new TimeSpan(10, 30 + variant % 30, 0).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                break;
            case XmlTypeCode.GYear: yield return (2026 + variant).ToString(CultureInfo.InvariantCulture); break;
            case XmlTypeCode.GYearMonth: yield return $"2026-{variant % 12 + 1:00}"; break;
            case XmlTypeCode.GMonth: yield return $"--{variant % 12 + 1:00}"; break;
            case XmlTypeCode.GDay: yield return $"---{variant % 28 + 1:00}"; break;
            case XmlTypeCode.GMonthDay: yield return $"--{variant % 12 + 1:00}-15"; break;
            case XmlTypeCode.Duration or XmlTypeCode.DayTimeDuration or XmlTypeCode.YearMonthDuration:
                yield return $"P{n}D";
                yield return $"P{n}M";
                break;
            case XmlTypeCode.Base64Binary:
                foreach (var length in Lengths(c, 3))
                    yield return Convert.ToBase64String(Enumerable.Range(0, length).Select(i => (byte)(i + variant)).ToArray());
                break;
            case XmlTypeCode.HexBinary:
                foreach (var length in Lengths(c, 2))
                    yield return string.Concat(Enumerable.Range(0, length).Select(i => ((byte)(i + variant)).ToString("X2")));
                break;
            case XmlTypeCode.AnyUri:
                yield return $"https://exemplo.com/{Slug(name)}";
                yield return "https://exemplo.com";
                break;
            case XmlTypeCode.Language:
                yield return "pt-BR";
                break;
            case XmlTypeCode.Id or XmlTypeCode.Idref or XmlTypeCode.NCName or XmlTypeCode.Name or XmlTypeCode.QName
                or XmlTypeCode.NmToken or XmlTypeCode.Entity:
                foreach (var v in TextCandidates(c, NameLike(name) + (variant == 0 ? "" : n.ToString(CultureInfo.InvariantCulture))))
                    yield return v;
                break;
            default:
                foreach (var v in TextCandidates(c, variant == 0 ? name : $"{name} {n}")) yield return v;
                break;
        }
    }

    private static IEnumerable<string> NumericCandidates(Constraints c, int n, bool fractional)
    {
        var decimals = fractional ? Math.Min(c.FractionDigits ?? 2, 2) : 0;
        var values = new List<decimal> { n, 0, 1 };
        if (c.MinInclusive is { } minI) values.InsertRange(0, [minI + n - 1, minI]);
        if (c.MinExclusive is { } minE) values.InsertRange(0, [minE + n, minE + 1, minE + 0.01m]);
        if (c.MaxInclusive is { } maxI) values.AddRange([maxI - n + 1, maxI]);
        if (c.MaxExclusive is { } maxE) values.AddRange([maxE - n, maxE - 1, maxE - 0.01m]);
        foreach (var value in values)
        {
            var rounded = Math.Round(value, decimals, MidpointRounding.ToZero);
            yield return rounded.ToString("F" + decimals, CultureInfo.InvariantCulture);
            yield return Math.Truncate(value).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static IEnumerable<int> Lengths(Constraints c, int preferred)
    {
        if (c.Length is { } exact) { yield return exact; yield break; }
        yield return Math.Clamp(preferred, c.MinLength ?? 0, c.MaxLength ?? int.MaxValue);
    }

    /// <summary>Texto legível (derivado do nome do nó) ajustado aos limites de tamanho.</summary>
    private static IEnumerable<string> TextCandidates(Constraints c, string text)
    {
        var min = c.Length ?? c.MinLength ?? 0;
        var max = c.Length ?? c.MaxLength ?? int.MaxValue;
        var value = text;
        while (value.Length < min) value += text.Length > 0 ? text : "x";
        if (value.Length > max) value = value[..max];
        yield return value;
        yield return new string('x', Math.Max(min, Math.Min(max, 1)));
    }

    private static string NameLike(string name)
    {
        var chars = name.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.').ToArray();
        var result = new string(chars);
        return result.Length == 0 || !(char.IsLetter(result[0]) || result[0] == '_') ? "n" + result : result;
    }

    private static string Slug(string name) => Uri.EscapeDataString(name);

    private static XmlTypeCode BuiltInCode(XmlSchemaSimpleType type)
    {
        for (XmlSchemaType? t = type; t is not null; t = t.BaseXmlSchemaType)
            if (t.QualifiedName.Namespace == XmlSchema.Namespace)
                return t.TypeCode;
        return type.TypeCode;
    }

    private static XmlSchemaSimpleType? ListItemType(XmlSchemaSimpleType type)
    {
        for (XmlSchemaType? t = type; t is XmlSchemaSimpleType simple; t = t.BaseXmlSchemaType)
            if (simple.Content is XmlSchemaSimpleTypeList list)
                return list.BaseItemType;
        return null;
    }

    private static List<XmlSchemaSimpleType> UnionMembers(XmlSchemaSimpleType type)
    {
        for (XmlSchemaType? t = type; t is XmlSchemaSimpleType simple; t = t.BaseXmlSchemaType)
            if (simple.Content is XmlSchemaSimpleTypeUnion union)
                return union.BaseMemberTypes?.ToList() ?? [];
        return [];
    }

    /// <summary>Facets efetivos de um tipo simples, somando a cadeia de restrições.</summary>
    private sealed class Constraints
    {
        public List<string> Enumerations { get; } = [];
        public List<string> Patterns { get; } = [];
        public int? Length, MinLength, MaxLength, TotalDigits, FractionDigits;
        public decimal? MinInclusive, MaxInclusive, MinExclusive, MaxExclusive;

        public static Constraints Of(XmlSchemaSimpleType type)
        {
            var result = new Constraints();
            var enumerationsFound = false;
            for (XmlSchemaType? t = type; t is XmlSchemaSimpleType simple; t = t.BaseXmlSchemaType)
            {
                if (simple.Content is not XmlSchemaSimpleTypeRestriction restriction) continue;
                var enumerations = restriction.Facets.OfType<XmlSchemaEnumerationFacet>().Select(f => f.Value!).ToList();
                if (!enumerationsFound && enumerations.Count > 0)
                {
                    result.Enumerations.AddRange(enumerations);
                    enumerationsFound = true;
                }
                // Padrões do mesmo nível são alternativos; de níveis diferentes, cumulativos.
                // Gera a partir do nível mais derivado e deixa ParseValue checar os demais.
                var patterns = restriction.Facets.OfType<XmlSchemaPatternFacet>().Select(f => f.Value!).ToList();
                if (result.Patterns.Count == 0) result.Patterns.AddRange(patterns);
                foreach (var facet in restriction.Facets.OfType<XmlSchemaFacet>())
                {
                    switch (facet)
                    {
                        case XmlSchemaLengthFacet: result.Length ??= Int(facet); break;
                        case XmlSchemaMinLengthFacet: result.MinLength ??= Int(facet); break;
                        case XmlSchemaMaxLengthFacet: result.MaxLength ??= Int(facet); break;
                        case XmlSchemaTotalDigitsFacet: result.TotalDigits ??= Int(facet); break;
                        case XmlSchemaFractionDigitsFacet: result.FractionDigits ??= Int(facet); break;
                        case XmlSchemaMinInclusiveFacet: result.MinInclusive ??= Dec(facet); break;
                        case XmlSchemaMaxInclusiveFacet: result.MaxInclusive ??= Dec(facet); break;
                        case XmlSchemaMinExclusiveFacet: result.MinExclusive ??= Dec(facet); break;
                        case XmlSchemaMaxExclusiveFacet: result.MaxExclusive ??= Dec(facet); break;
                    }
                }
            }
            return result;
        }

        private static int? Int(XmlSchemaFacet f) => int.TryParse(f.Value, CultureInfo.InvariantCulture, out var v) ? v : null;
        private static decimal? Dec(XmlSchemaFacet f) =>
            decimal.TryParse(f.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
