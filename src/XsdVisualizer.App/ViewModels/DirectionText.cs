using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

/// <summary>Rótulo de Request/Response no idioma da interface.</summary>
public static class DirectionText
{
    public static string Of(MessageDirection direction) => direction == MessageDirection.Request ? Strings.Request : Strings.Response;
}
