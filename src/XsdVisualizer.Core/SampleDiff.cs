using System.Xml;
using System.Xml.Linq;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace XsdVisualizer.Core;

/// <summary>Imaginary: linha em branco que alinha o lado a lado quando o outro lado tem uma linha a mais.</summary>
public enum DiffLineKind { Unchanged, Added, Removed, Modified, Imaginary }

/// <summary>Uma linha do lado a lado; Path é o caminho de nomes do elemento que começa nela (se houver).</summary>
public sealed record DiffLine(int? Number, string Text, DiffLineKind Kind, string? Path);

/// <summary>Samples do Before e do After lado a lado, alinhados (mesma quantidade de linhas dos dois lados).</summary>
public sealed record SampleDiff(IReadOnlyList<DiffLine> Before, IReadOnlyList<DiffLine> After);

internal static class SampleDiffer
{
    public static SampleDiff Diff(string beforeXml, string afterXml)
    {
        var model = SideBySideDiffBuilder.Diff(new Differ(), beforeXml, afterXml, ignoreWhiteSpace: false);
        var beforePaths = PathsByLine(beforeXml);
        var afterPaths = PathsByLine(afterXml);
        var before = new List<DiffLine>();
        var after = new List<DiffLine>();
        for (var i = 0; i < model.OldText.Lines.Count; i++)
        {
            var b = Line(model.OldText.Lines[i], beforePaths, isBefore: true);
            var a = Line(model.NewText.Lines[i], afterPaths, isBefore: false);
            // A DiffPlex junta "saiu uma linha, entrou outra" num Modified. Se são elementos diferentes
            // (ex.: <fax> saiu e <IBSCBS> entrou), não é modificação: vira Removed + Added, alinhados com brancos.
            if (b.Kind == DiffLineKind.Modified && a.Kind == DiffLineKind.Modified && b.Path != a.Path)
            {
                before.Add(b with { Kind = DiffLineKind.Removed });
                after.Add(Blank);
                before.Add(Blank);
                after.Add(a with { Kind = DiffLineKind.Added });
                continue;
            }
            before.Add(b);
            after.Add(a);
        }
        return new SampleDiff(before, after);
    }

    private static readonly DiffLine Blank = new(null, "", DiffLineKind.Imaginary, null);

    private static DiffLine Line(DiffPiece piece, IReadOnlyDictionary<int, string> paths, bool isBefore)
    {
        var kind = piece.Type switch
        {
            ChangeType.Deleted => isBefore ? DiffLineKind.Removed : DiffLineKind.Imaginary,
            ChangeType.Inserted => isBefore ? DiffLineKind.Imaginary : DiffLineKind.Added,
            ChangeType.Modified => DiffLineKind.Modified,
            ChangeType.Imaginary => DiffLineKind.Imaginary,
            _ => DiffLineKind.Unchanged,
        };
        var number = kind == DiffLineKind.Imaginary ? null : piece.Position;
        return new DiffLine(number, kind == DiffLineKind.Imaginary ? "" : piece.Text ?? "", kind,
            number is { } n && paths.TryGetValue(n, out var path) ? path : null);
    }

    /// <summary>Linha (1..n) → caminho de nomes do elemento que abre nela, como nos nós da árvore de Changes.</summary>
    private static Dictionary<int, string> PathsByLine(string xml)
    {
        var paths = new Dictionary<int, string>();
        if (xml.Length == 0) return paths;
        try
        {
            var root = XDocument.Parse(xml, LoadOptions.SetLineInfo).Root!;
            foreach (var element in root.DescendantsAndSelf())
            {
                var line = ((IXmlLineInfo)element).LineNumber;
                if (!paths.ContainsKey(line))
                    paths[line] = string.Join("/", element.AncestorsAndSelf().Reverse().Select(e => e.Name.LocalName));
            }
        }
        catch (XmlException) { }
        return paths;
    }
}
