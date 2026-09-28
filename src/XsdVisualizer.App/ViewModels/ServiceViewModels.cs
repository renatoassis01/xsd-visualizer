using CommunityToolkit.Mvvm.ComponentModel;
using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

/// <summary>Grupo "Serviços" de um Schema Set na árvore da esquerda.</summary>
public sealed class ServicesGroupViewModel(IReadOnlyList<ServiceViewModel> services) : ViewModelBase
{
    public string Label => Strings.Services;
    public IReadOnlyList<ServiceViewModel> Services { get; } = services;
}

public sealed class ServiceViewModel(Service model, IPayloadBindings store) : ViewModelBase
{
    public Service Model { get; } = model;
    public string Name => Model.Name;
    public string SourceFileName => Model.FileName;
    public IReadOnlyList<OperationViewModel> Operations { get; } = model.Operations.Select(o => new OperationViewModel(o, store)).ToList();
}

/// <summary>
/// Uma Operation selecionada: Endpoint, Request/Response e o Payload Binding de cada direção,
/// com a árvore do Payload (e seus ramos fixados) para gerar Envelopes.
/// </summary>
public sealed partial class OperationViewModel : ViewModelBase
{
    private readonly IPayloadBindings _store;
    private bool _loading;

    public OperationViewModel(Operation model, IPayloadBindings store)
    {
        Model = model;
        _store = store;
        var saved = store.GetEndpoint(model);
        _loading = true; // o padrão não é uma escolha do usuário: não vai para a sessão
        SelectedEndpoint = model.Endpoints.FirstOrDefault(e => e.Name == saved) ?? model.DefaultEndpoint;
        _loading = false;
        LoadDirection();
    }

    public Operation Model { get; }
    public string Name => Model.Name;
    public string ServiceName => Model.Service.Name;
    public IReadOnlyList<Endpoint> Endpoints => Model.Endpoints;

    /// <summary>Disparado quando um ramo é fixado na árvore do Payload (regera o Envelope Maximal).</summary>
    public event Action<OperationViewModel>? PinsChanged;

    [ObservableProperty] public partial Endpoint SelectedEndpoint { get; set; }

    partial void OnSelectedEndpointChanged(Endpoint value)
    {
        if (value is null) return;
        if (!_loading) _store.SetEndpoint(Model, value);
        OnPropertyChanged(nameof(SoapAction));
        OnPropertyChanged(nameof(Address));
    }

    public string? SoapAction => SelectedEndpoint?.SoapActionOf(Model);
    public string? Address => SelectedEndpoint?.Address;

    [ObservableProperty] public partial bool IsResponse { get; set; }
    public MessageDirection Direction => IsResponse ? MessageDirection.Response : MessageDirection.Request;
    public OperationMessage Message => Model.Message(Direction);

    partial void OnIsResponseChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRequest));
        LoadDirection();
    }

    public bool IsRequest
    {
        get => !IsResponse;
        set => IsResponse = !value;
    }

    /// <summary>Um nó da árvore do Payload foi selecionado (para o painel de detalhes).</summary>
    public event Action<SchemaNodeViewModel>? NodeSelected;

    public string BodyDescription => string.Format(Message.BodyIsString ? Strings.BodyString : Strings.BodyAny, Message.BodyElementName);
    public string HeadersDescription => Message.HeaderNames.Count == 0 ? Strings.NoHeaders : string.Join(", ", Message.HeaderNames);
    public bool CanCompress => Message.BodyIsString;

    public IReadOnlyList<GlobalElementChoice> PayloadChoices => _store.PayloadChoices;
    /// <summary>Sem nenhum XSD aberto não há o que escolher: o WSDL sozinho não traz o XML de negócio.</summary>
    public bool HasPayloadChoices => PayloadChoices.Count > 0;

    [ObservableProperty] public partial GlobalElementChoice? SelectedPayload { get; set; }
    [ObservableProperty] public partial bool Compressed { get; set; }

    partial void OnSelectedPayloadChanged(GlobalElementChoice? value)
    {
        if (_loading) return;
        // Primeiro vínculo de um Body string: já vem marcado como compactado.
        if (value is not null && _store.Get(Message) is null) SetCompressedSilently(Message.BodyIsString);
        _store.Set(Message, value?.Element, Compressed);
        BuildPayloadTree();
    }

    partial void OnCompressedChanged(bool value)
    {
        if (_loading) return;
        _store.Set(Message, SelectedPayload?.Element, value);
    }

    public PayloadBinding? CurrentBinding => SelectedPayload is { } choice ? new PayloadBinding(choice.Element, Compressed) : null;

    /// <summary>Dono (pins) da árvore do Payload vinculado; null sem Payload Binding.</summary>
    [ObservableProperty] public partial GlobalElementViewModel? PayloadOwner { get; private set; }
    [ObservableProperty] public partial IReadOnlyList<SchemaNodeViewModel> PayloadTree { get; private set; } = [];
    public bool HasPayload => SelectedPayload is not null;

    /// <summary>Recarrega a lista de Global Elements (Schema Set aberto ou fechado) e o vínculo salvo.</summary>
    public void RefreshChoices()
    {
        OnPropertyChanged(nameof(PayloadChoices));
        OnPropertyChanged(nameof(HasPayloadChoices));
        LoadDirection();
    }

    private void LoadDirection()
    {
        _loading = true;
        var binding = _store.Get(Message);
        SelectedPayload = binding is null ? null : _store.PayloadChoices.FirstOrDefault(c => c.Element == binding.Element);
        Compressed = binding?.Compressed ?? Message.BodyIsString;
        _loading = false;
        BuildPayloadTree();
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(BodyDescription));
        OnPropertyChanged(nameof(HeadersDescription));
        OnPropertyChanged(nameof(CanCompress));
    }

    private void SetCompressedSilently(bool value)
    {
        _loading = true;
        Compressed = value;
        _loading = false;
    }

    private void BuildPayloadTree()
    {
        if (PayloadOwner is not null)
        {
            PayloadOwner.PinsChanged -= OnPayloadPinsChanged;
            PayloadOwner.NodeSelected -= OnPayloadNodeSelected;
        }
        PayloadOwner = SelectedPayload is { } choice ? new GlobalElementViewModel(choice.Element) : null;
        if (PayloadOwner is not null)
        {
            PayloadOwner.PinsChanged += OnPayloadPinsChanged;
            PayloadOwner.NodeSelected += OnPayloadNodeSelected;
        }
        PayloadTree = PayloadOwner is null ? [] : [new SchemaNodeViewModel(PayloadOwner.Model.Tree, PayloadOwner, null) { IsExpanded = true }];
        OnPropertyChanged(nameof(HasPayload));
    }

    private void OnPayloadPinsChanged(GlobalElementViewModel _) => PinsChanged?.Invoke(this);

    private void OnPayloadNodeSelected(SchemaNodeViewModel node) => NodeSelected?.Invoke(node);
}
