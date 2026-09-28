using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

/// <summary>Resultado da busca na árvore: o nó e o caminho até ele (ex.: "NFe/infNFe/ide/tpAmb").</summary>
public sealed record SearchResultViewModel(SchemaNode Node, string Display)
{
    public string Label => Node.Label;
    public string ParentPath => Display.Contains('/') ? Display[..Display.LastIndexOf('/')] : "";
}
