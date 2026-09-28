using Avalonia.Data.Converters;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.Views;

public static class DiffConverters
{
    public static readonly FuncValueConverter<PairStatus, bool> IsAdded = new(s => s == PairStatus.Added);
    public static readonly FuncValueConverter<PairStatus, bool> IsRemoved = new(s => s == PairStatus.Removed);
    public static readonly FuncValueConverter<PairStatus, bool> IsModified = new(s => s == PairStatus.Modified);
    public static readonly FuncValueConverter<PairStatus, bool> IsUnchanged = new(s => s == PairStatus.Unchanged);
}
