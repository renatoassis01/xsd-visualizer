using Avalonia.Data.Converters;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.Views;

/// <summary>Classes de estilo por ChangeKind (cor de entrou/saiu/mudou), usadas pela lista de pares.</summary>
public static class DiffConverters
{
    public static readonly FuncValueConverter<ChangeKind, bool> IsAdded = new(k => k == ChangeKind.Added);
    public static readonly FuncValueConverter<ChangeKind, bool> IsRemoved = new(k => k == ChangeKind.Removed);
    public static readonly FuncValueConverter<ChangeKind, bool> IsModified = new(k => k == ChangeKind.Modified);
    public static readonly FuncValueConverter<ChangeKind, bool> IsUnchanged = new(k => k is ChangeKind.Unchanged or ChangeKind.DocumentationOnly);
}
