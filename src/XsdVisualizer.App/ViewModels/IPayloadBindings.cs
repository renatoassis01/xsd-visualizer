using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

/// <summary>Onde ficam os Payload Bindings e o Endpoint escolhido de cada Operation (persistidos na sessão).</summary>
public interface IPayloadBindings
{
    PayloadBinding? Get(Operation operation, MessageDirection direction);
    void Set(Operation operation, MessageDirection direction, GlobalElement? element, bool compressed);
    string? GetEndpoint(Operation operation);
    void SetEndpoint(Operation operation, Endpoint endpoint);
    IReadOnlyList<GlobalElementChoice> PayloadChoices { get; }
}

/// <summary>Um Global Element oferecido como Payload: nome, Schema Set e arquivo.</summary>
public sealed record GlobalElementChoice(GlobalElement Element)
{
    public string Display => $"{Element.Name}   [{Element.SchemaSet.Name} / {Element.FileName}]";
    public override string ToString() => Display;
}
