using XsdVisualizer.App.Services;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

/// <summary>
/// Payload Bindings e Endpoint escolhido de cada Operation, persistidos na sessão, e a lista de Global Elements
/// (dos Schema Sets abertos) oferecidos como Payload.
/// </summary>
public sealed class PayloadBindingStore(SessionStore session, Func<IEnumerable<SchemaSet>> openSchemaSets) : IPayloadBindings
{
    public IReadOnlyList<GlobalElementChoice> PayloadChoices { get; private set; } = [];

    /// <summary>Um Payload Binding mudou: Documents abertos refazem o Binding.</summary>
    public event Action? BindingsChanged;

    /// <summary>Schema Sets abertos mudaram: refaz a lista de Payloads e os vínculos salvos das Operations.</summary>
    public void Refresh(IEnumerable<OperationViewModel> operations)
    {
        PayloadChoices = openSchemaSets().SelectMany(s => s.GlobalElements).Select(e => new GlobalElementChoice(e)).ToList();
        foreach (var operation in operations) operation.RefreshChoices();
    }

    public PayloadBinding? Get(OperationMessage message)
    {
        if (!session.Current.PayloadBindings.TryGetValue(SessionKeys.Of(message), out var saved)) return null;
        var element = openSchemaSets().SelectMany(s => s.GlobalElements).FirstOrDefault(e =>
            e.Name == saved.ElementName && e.Namespace == saved.ElementNamespace && e.SourceFile == saved.ElementSourceFile);
        return element is null ? null : new PayloadBinding(element, saved.Compressed);
    }

    public void Set(OperationMessage message, GlobalElement? element, bool compressed)
    {
        var key = SessionKeys.Of(message);
        if (element is null) session.Current.PayloadBindings.Remove(key);
        else session.Current.PayloadBindings[key] = new SavedPayloadBinding
        {
            ElementName = element.Name,
            ElementNamespace = element.Namespace,
            ElementSourceFile = element.SourceFile,
            Compressed = compressed,
        };
        session.Save();
        BindingsChanged?.Invoke();
    }

    public string? GetEndpoint(Operation operation) =>
        session.Current.Endpoints.GetValueOrDefault(SessionKeys.Of(operation));

    public void SetEndpoint(Operation operation, Endpoint endpoint)
    {
        session.Current.Endpoints[SessionKeys.Of(operation)] = endpoint.Name;
        session.Save();
    }

    /// <summary>O Endpoint que o usuário escolheu para a Operation, se escolheu.</summary>
    public Endpoint? ChosenEndpoint(Operation operation) =>
        GetEndpoint(operation) is { } name ? operation.Endpoints.FirstOrDefault(e => e.Name == name) : null;
}
