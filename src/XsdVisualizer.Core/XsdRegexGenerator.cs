using System.Text;
using System.Text.RegularExpressions;

namespace XsdVisualizer.Core;

/// <summary>
/// Gera strings que casam com uma expressão regular de XSD (implicitamente ancorada).
/// Determinístico: o mesmo padrão, variante e política de repetição produzem sempre a mesma string.
/// </summary>
internal sealed class XsdRegexGenerator
{
    private readonly Node _root;

    private XsdRegexGenerator(Node root) => _root = root;

    public static XsdRegexGenerator? TryParse(string pattern)
    {
        try
        {
            var parser = new Parser(pattern);
            var node = parser.ParseAlternation();
            return parser.AtEnd ? new XsdRegexGenerator(node) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <param name="variant">Varia ramos de alternância e caracteres escolhidos.</param>
    /// <param name="repetitions">Quantidade preferida de repetições para quantificadores (limitada por min/max).</param>
    public string Generate(int variant, int repetitions)
    {
        var builder = new StringBuilder();
        _root.Emit(builder, variant, repetitions);
        return builder.ToString();
    }

    private abstract class Node
    {
        public abstract void Emit(StringBuilder output, int variant, int repetitions);
    }

    private sealed class Alternation(List<Node> branches) : Node
    {
        public override void Emit(StringBuilder output, int variant, int repetitions) =>
            branches[variant % branches.Count].Emit(output, variant / branches.Count, repetitions);
    }

    private sealed class Sequence(List<Node> items) : Node
    {
        public override void Emit(StringBuilder output, int variant, int repetitions)
        {
            foreach (var item in items) item.Emit(output, variant, repetitions);
        }
    }

    private sealed class Repeat(Node item, int min, int? max) : Node
    {
        public override void Emit(StringBuilder output, int variant, int repetitions)
        {
            var count = Math.Max(min, Math.Min(max ?? int.MaxValue, repetitions));
            for (var i = 0; i < count; i++) item.Emit(output, variant + i, repetitions);
        }
    }

    private sealed class CharSet(Func<char, bool> contains) : Node
    {
        // Ordem de preferência: legível primeiro; o resto só se necessário.
        private const string Preferred = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0";
        private List<char>? _allowed;

        public Func<char, bool> Contains { get; } = contains;

        public override void Emit(StringBuilder output, int variant, int repetitions)
        {
            _allowed ??= Allowed();
            if (_allowed.Count == 0) throw new InvalidOperationException("Classe de caracteres vazia");
            output.Append(_allowed[(variant + output.Length) % _allowed.Count]);
        }

        private List<char> Allowed()
        {
            var nice = Preferred.Where(Contains).ToList();
            if (nice.Count > 0) return nice;
            for (var c = '!'; c < '�'; c++)
                if (!char.IsSurrogate(c) && Contains(c)) return [c];
            return Contains(' ') ? [' '] : [];
        }
    }

    private sealed class Parser(string pattern)
    {
        private int _pos;

        public bool AtEnd => _pos >= pattern.Length;
        private char Peek => pattern[_pos];

        public Node ParseAlternation()
        {
            var branches = new List<Node> { ParseSequence() };
            while (!AtEnd && Peek == '|')
            {
                _pos++;
                branches.Add(ParseSequence());
            }
            return branches.Count == 1 ? branches[0] : new Alternation(branches);
        }

        private Node ParseSequence()
        {
            var items = new List<Node>();
            while (!AtEnd && Peek is not ('|' or ')'))
                items.Add(ParseQuantified(ParseAtom()));
            return new Sequence(items);
        }

        private Node ParseQuantified(Node atom)
        {
            if (AtEnd) return atom;
            switch (Peek)
            {
                case '?': _pos++; return new Repeat(atom, 0, 1);
                case '*': _pos++; return new Repeat(atom, 0, null);
                case '+': _pos++; return new Repeat(atom, 1, null);
                case '{':
                    var close = pattern.IndexOf('}', _pos);
                    if (close < 0) throw new FormatException();
                    var parts = pattern[(_pos + 1)..close].Split(',');
                    _pos = close + 1;
                    var min = int.Parse(parts[0]);
                    int? max = parts.Length == 1 ? min : parts[1].Length == 0 ? null : int.Parse(parts[1]);
                    return new Repeat(atom, min, max);
                default: return atom;
            }
        }

        private Node ParseAtom()
        {
            var c = pattern[_pos++];
            switch (c)
            {
                case '(':
                    var inner = ParseAlternation();
                    if (AtEnd || pattern[_pos++] != ')') throw new FormatException();
                    return inner;
                case '[':
                    return new CharSet(ParseClassBody());
                case '.':
                    return new CharSet(ch => ch is not ('\n' or '\r'));
                case '\\':
                    return new CharSet(ParseEscape());
                default:
                    return new CharSet(ch => ch == c);
            }
        }

        /// <summary>Lê o conteúdo de [...] (após o '['), incluindo negação e subtração.</summary>
        private Func<char, bool> ParseClassBody()
        {
            var negated = !AtEnd && Peek == '^';
            if (negated) _pos++;
            var parts = new List<Func<char, bool>>();
            Func<char, bool>? subtraction = null;
            var first = true;
            while (true)
            {
                if (AtEnd) throw new FormatException();
                if (Peek == ']' && !first) { _pos++; break; }
                first = false;
                if (Peek == '-' && _pos + 1 < pattern.Length && pattern[_pos + 1] == '[')
                {
                    _pos += 2;
                    subtraction = ParseClassBody();
                    if (AtEnd || pattern[_pos++] != ']') throw new FormatException();
                    break;
                }
                var (single, set) = ParseClassAtom();
                if (set is not null)
                {
                    parts.Add(set);
                    continue;
                }
                if (!AtEnd && Peek == '-' && _pos + 1 < pattern.Length && pattern[_pos + 1] is not (']' or '['))
                {
                    _pos++;
                    var (to, _) = ParseClassAtom();
                    var from = single;
                    parts.Add(ch => ch >= from && ch <= to);
                }
                else
                {
                    var only = single;
                    parts.Add(ch => ch == only);
                }
            }
            return ch =>
            {
                var inSet = parts.Any(p => p(ch)) != negated;
                return inSet && (subtraction is null || !subtraction(ch));
            };
        }

        private (char Single, Func<char, bool>? Set) ParseClassAtom()
        {
            var c = pattern[_pos++];
            if (c != '\\') return (c, null);
            var escape = pattern[_pos];
            if (SingleCharEscape(escape) is { } single)
            {
                _pos++;
                return (single, null);
            }
            return ('\0', ParseEscape());
        }

        private static char? SingleCharEscape(char c) => c switch
        {
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            '\\' or '|' or '.' or '-' or '^' or '?' or '*' or '+' or '{' or '}' or '(' or ')' or '[' or ']' or '$' => c,
            _ => null,
        };

        /// <summary>Lê um escape (após a barra) e devolve o conjunto de caracteres correspondente.</summary>
        private Func<char, bool> ParseEscape()
        {
            var c = pattern[_pos++];
            if (SingleCharEscape(c) is { } single) return ch => ch == single;
            switch (c)
            {
                case 'd': return char.IsDigit;
                case 'D': return ch => !char.IsDigit(ch);
                case 's': return ch => ch is ' ' or '\t' or '\n' or '\r';
                case 'S': return ch => ch is not (' ' or '\t' or '\n' or '\r');
                case 'w': return ch => !char.IsPunctuation(ch) && !char.IsSeparator(ch) && !char.IsControl(ch) && !char.IsWhiteSpace(ch);
                case 'W': return ch => char.IsPunctuation(ch) || char.IsSeparator(ch) || char.IsControl(ch) || char.IsWhiteSpace(ch);
                case 'i': return IsNameStart;
                case 'I': return ch => !IsNameStart(ch);
                case 'c': return IsNameChar;
                case 'C': return ch => !IsNameChar(ch);
                case 'p' or 'P':
                    if (AtEnd || pattern[_pos] != '{') throw new FormatException();
                    var close = pattern.IndexOf('}', _pos);
                    if (close < 0) throw new FormatException();
                    var property = pattern[(_pos + 1)..close];
                    _pos = close + 1;
                    var regex = new Regex($"^\\{c}{{{property}}}$");
                    return ch => regex.IsMatch(ch.ToString());
                default:
                    throw new FormatException();
            }
        }

        private static bool IsNameStart(char ch) => char.IsLetter(ch) || ch is '_' or ':';
        private static bool IsNameChar(char ch) => IsNameStart(ch) || char.IsDigit(ch) || ch is '-' or '.' || ch == '·';
    }
}
