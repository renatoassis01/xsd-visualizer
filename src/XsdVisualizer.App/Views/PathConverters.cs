using Avalonia.Data.Converters;

namespace XsdVisualizer.App.Views;

/// <summary>Partes de um caminho de pasta para exibição (nome em destaque, pasta-mãe abaixo).</summary>
public static class PathConverters
{
    public static readonly FuncValueConverter<string?, string> FolderName =
        new(path => path is null ? "" : Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));

    public static readonly FuncValueConverter<string?, string> FileName = new(path => Path.GetFileName(path) ?? "");

    public static readonly FuncValueConverter<string?, string> ParentFolder =
        new(path => path is null ? "" : Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)) ?? "");
}
