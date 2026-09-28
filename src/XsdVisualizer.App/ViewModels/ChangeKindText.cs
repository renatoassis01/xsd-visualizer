using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

/// <summary>Rótulo de cada ChangeKind no idioma da interface (pares e nós usam o mesmo).</summary>
public static class ChangeKindText
{
    public static string Of(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => Strings.KindAdded,
        ChangeKind.Removed => Strings.KindRemoved,
        ChangeKind.Modified => Strings.KindModified,
        ChangeKind.DocumentationOnly => Strings.KindDocumentation,
        _ => Strings.KindUnchanged,
    };
}
