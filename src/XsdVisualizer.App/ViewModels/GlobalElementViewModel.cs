using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

public sealed class GlobalElementViewModel(GlobalElement model) : ViewModelBase
{
    public GlobalElement Model { get; } = model;

    public string Name => Model.Name;
    public string Namespace => Model.Namespace;
    public string SourceFileName => Model.FileName;

    /// <summary>Documentação do Global Element (xs:documentation), para a dica da lista.</summary>
    public string? Documentation => Model.Tree.Documentation;
    public bool HasDocumentation => !string.IsNullOrWhiteSpace(Documentation);

    /// <summary>Ramos fixados na árvore (caminho do Choice → índice do ramo), usados pelo Maximal Sample.</summary>
    public Dictionary<string, int> Pins { get; } = new();

    /// <summary>Disparado quando um nó da árvore deste elemento é selecionado.</summary>
    public event Action<SchemaNodeViewModel>? NodeSelected;

    internal void OnNodeSelected(SchemaNodeViewModel node) => NodeSelected?.Invoke(node);

    /// <summary>Disparado quando uma alternativa é fixada na árvore.</summary>
    public event Action<GlobalElementViewModel>? PinsChanged;

    internal void OnPinsChanged() => PinsChanged?.Invoke(this);
}
