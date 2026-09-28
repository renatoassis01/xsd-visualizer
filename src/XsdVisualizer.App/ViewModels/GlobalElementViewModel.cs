using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

public sealed class GlobalElementViewModel(GlobalElement model, bool showSourceFile) : ViewModelBase
{
    public GlobalElement Model { get; } = model;

    /// <summary>Nome; com o arquivo quando o mesmo nome é declarado em mais de um arquivo do Schema Set.</summary>
    public string DisplayName { get; } = showSourceFile
        ? $"{model.Name}  ({Path.GetFileName(model.SourceFile)})"
        : model.Name;

    public string Namespace => Model.Namespace;
    public string SourceFileName => Path.GetFileName(Model.SourceFile);

    /// <summary>Ramos fixados na árvore (caminho do Choice → índice do ramo), usados pelo Maximal Sample.</summary>
    public Dictionary<string, int> Pins { get; } = new();

    /// <summary>Disparado quando um nó da árvore deste elemento é selecionado.</summary>
    public event Action<SchemaNodeViewModel>? NodeSelected;

    internal void OnNodeSelected(SchemaNodeViewModel node) => NodeSelected?.Invoke(node);

    /// <summary>Disparado quando uma alternativa é fixada na árvore.</summary>
    public event Action<GlobalElementViewModel>? PinsChanged;

    internal void OnPinsChanged() => PinsChanged?.Invoke(this);
}
