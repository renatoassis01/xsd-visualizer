using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

public sealed partial class SchemaNodeViewModel : ViewModelBase
{
    private readonly GlobalElementViewModel _owner;
    private ObservableCollection<SchemaNodeViewModel>? _children;

    public SchemaNodeViewModel(SchemaNode node, GlobalElementViewModel owner, SchemaNodeViewModel? parent)
    {
        Node = node;
        _owner = owner;
        Parent = parent;
    }

    public SchemaNode Node { get; }
    public SchemaNodeViewModel? Parent { get; }

    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value) _owner.OnNodeSelected(this);
    }

    /// <summary>Filhos criados só quando o TreeView pede (a árvore pode ser recursiva).</summary>
    public ObservableCollection<SchemaNodeViewModel> Children =>
        _children ??= new(Node.Children.Select(c => new SchemaNodeViewModel(c, _owner, this)));

    public string Label => Node.Label;
    public string Cardinality => Node.Cardinality;
    public string? TypeName => Node.XsiType ?? Node.TypeName;
    public bool IsRecursive => Node.IsRecursive;
    public string Icon => Node.Kind switch
    {
        NodeKind.Element => "◆",
        NodeKind.Attribute => "@",
        NodeKind.Sequence => "⋯",
        NodeKind.Choice => "⎇",
        NodeKind.All => "∀",
        _ => "✱",
    };

    public bool IsChoice => Node.Kind == NodeKind.Choice && Node.Children.Count > 1;
    public IReadOnlyList<string> Branches => Node.Children.Select(BranchLabel).ToList();

    public int PinnedBranch
    {
        get => _owner.Pins.GetValueOrDefault(Node.Path, 0);
        set
        {
            if (value < 0 || PinnedBranch == value) return;
            _owner.Pins[Node.Path] = value;
            OnPropertyChanged();
            _owner.OnPinsChanged();
        }
    }

    public string? Documentation => Node.Documentation;
    public string FacetsText => string.Join("\n", Node.Facets.Select(f => $"{f.Kind} = {f.Value}"));
    public bool HasFacets => Node.Facets.Count > 0;
    public string? Notes => Node.Kind is NodeKind.AnyElement or NodeKind.AnyAttribute
        ? string.Format(Strings.Wildcard, Node.WildcardNamespace)
        : Node.IsRecursive ? Strings.Recursive : null;

    private static string BranchLabel(SchemaNode branch) => branch.Kind switch
    {
        NodeKind.Element when branch.XsiType is not null => $"{branch.Label} ({branch.XsiType})",
        NodeKind.Element => branch.Label,
        _ => $"{branch.Label}({string.Join(", ", branch.Children.Select(BranchLabel))})",
    };
}
