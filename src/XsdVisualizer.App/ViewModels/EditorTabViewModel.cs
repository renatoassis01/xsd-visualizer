using System.Collections.ObjectModel;
using AvaloniaEdit.Document;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

public sealed record SampleItem(Sample Sample, string Display);

/// <summary>Uma Operation e direção candidatas para um Envelope trazido pelo usuário.</summary>
public sealed record OperationChoice(OperationCandidate Candidate)
{
    public string Display =>
        $"{Candidate.Operation.Service.Name} / {Candidate.Operation.Name} ({DirectionText.Of(Candidate.Direction)})   [{Candidate.Operation.Service.FileName}]";
    public override string ToString() => Display;
}

/// <summary>
/// Uma aba do editor: um Document (XML do usuário, que pode ser um Envelope), Samples gerados
/// (Global Element ou Envelopes) ou um Payload descompactado.
/// </summary>
public sealed partial class EditorTabViewModel : ViewModelBase
{
    private int _validationVersion;
    private bool _loadingText;
    private Func<Operation, MessageDirection, PayloadBinding?> _savedPayloads = (_, _) => null;
    private Func<string, IReadOnlyList<ValidationIssue>>? _payloadValidator;

    private EditorTabViewModel(string title, string text)
    {
        Title = title;
        Document = new TextDocument(text);
        Document.TextChanged += (_, _) =>
        {
            if (_loadingText) return;
            IsDirty = true;
            ScheduleValidation();
        };
    }

    public static EditorTabViewModel ForDocument(string path, string text, IEnumerable<SchemaSet> openSets,
        Func<Operation, MessageDirection, PayloadBinding?> savedPayloads)
    {
        var tab = new EditorTabViewModel(Path.GetFileName(path), text) { FilePath = path, IsDocument = true, _savedPayloads = savedPayloads };
        tab.RefreshBinding(openSets);
        return tab;
    }

    /// <summary>Payload (descompactado) de um Envelope, validado contra o Global Element dele.</summary>
    public static EditorTabViewModel ForPayload(string title, string text, GlobalElement? element)
    {
        var tab = new EditorTabViewModel(title, text) { _payloadValidator = element is null ? null : element.Validate };
        tab.ScheduleValidation(immediate: true);
        return tab;
    }

    public static EditorTabViewModel ForSamples(string title, IReadOnlyList<Sample> samples)
    {
        var tab = new EditorTabViewModel(title, "");
        tab.ReplaceSamples(samples);
        return tab;
    }

    public string Title { get; }
    public TextDocument Document { get; }
    public string? FilePath { get; private set; }
    public bool IsDocument { get; private init; }
    public bool IsPayload => _payloadValidator is not null;

    [ObservableProperty] public partial bool IsDirty { get; set; }

    public ObservableCollection<ValidationIssue> Issues { get; } = [];
    [ObservableProperty] public partial string IssuesSummary { get; set; } = "";

    // ---- Samples ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSeveralSamples))]
    public partial IReadOnlyList<SampleItem> SampleItems { get; private set; } = [];
    public bool HasSeveralSamples => SampleItems.Count > 1;

    /// <summary>Um só Maximal Sample de <paramref name="element"/> (a aba que é regerada quando uma alternativa é fixada).</summary>
    public bool IsMaximalOf(GlobalElement element) =>
        SampleItems is [{ Sample: { Kind: SampleKind.Maximal } sample }] && sample.Element == element;

    /// <summary>Um só Envelope Maximal dessa Operation e direção (regerado quando um ramo do Payload é fixado).</summary>
    public bool IsMaximalEnvelopeOf(Operation operation, MessageDirection direction) =>
        SampleItems is [{ Sample: { Kind: SampleKind.Maximal } sample }] && sample.Operation == operation && sample.Direction == direction;

    /// <summary>Troca os Samples da aba (ex.: Maximal regerado), mantendo a aba aberta.</summary>
    public void ReplaceSamples(IReadOnlyList<Sample> samples)
    {
        SampleItems = samples.Select(s => new SampleItem(s, SampleDisplay(s, samples.Count))).ToList();
        SelectedSampleItem = SampleItems[0];
    }

    [ObservableProperty] public partial SampleItem? SelectedSampleItem { get; set; }
    public IReadOnlyList<string> Covers => SelectedSampleItem?.Sample.Covers ?? [];
    public bool HasCovers => Covers.Count > 0;

    partial void OnSelectedSampleItemChanged(SampleItem? value)
    {
        if (value is null) return;
        ReplaceText(value.Sample.Xml);
        SetIssues(value.Sample.Issues);
        IsDirty = false;
        OnPropertyChanged(nameof(Covers));
        OnPropertyChanged(nameof(HasCovers));
        OnPropertyChanged(nameof(HasPayloadToOpen));
    }

    // ---- Payload de Envelopes (compactado ou não) ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPayloadToOpen))]
    public partial string? DecompressedPayload { get; private set; }

    /// <summary>O Payload legível desta aba: do Envelope gerado, ou descompactado do Envelope trazido.</summary>
    public string? PayloadText => SelectedSampleItem?.Sample.Payload ?? DecompressedPayload;
    public GlobalElement? PayloadElement => IsDocument ? CurrentPayloadBinding?.Element : SelectedSampleItem?.Sample.Element;
    public bool HasPayloadToOpen => PayloadText is not null;

    public string SuggestedFileName => SelectedSampleItem?.Sample.FileName ?? Title;

    // ---- Binding (Documents) ----
    [ObservableProperty] public partial IReadOnlyList<GlobalElementViewModel> Candidates { get; set; } = [];
    [ObservableProperty] public partial GlobalElementViewModel? BoundElement { get; set; }
    [ObservableProperty] public partial string? BindingMessage { get; set; }
    public bool HasSeveralCandidates => Candidates.Count > 1;

    partial void OnCandidatesChanged(IReadOnlyList<GlobalElementViewModel> value) => OnPropertyChanged(nameof(HasSeveralCandidates));

    partial void OnBoundElementChanged(GlobalElementViewModel? value)
    {
        if (IsEnvelope)
        {
            UpdateEnvelopeMessage();
            ScheduleValidation(immediate: true);
            return;
        }
        BindingMessage = value is null && Candidates.Count > 1 ? Strings.ManyCandidates : BindingMessage;
        if (value is not null && Candidates.Count > 0) BindingMessage = null;
        ScheduleValidation(immediate: true);
    }

    // ---- Binding de Envelopes (Documents) ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BindingLabel))]
    public partial bool IsEnvelope { get; private set; }

    /// <summary>Rótulo do seletor de Global Element: num Envelope, ele escolhe o do Payload.</summary>
    public string BindingLabel => IsEnvelope ? Strings.PayloadLabel : Strings.Binding;
    [ObservableProperty] public partial IReadOnlyList<OperationChoice> OperationCandidates { get; private set; } = [];
    [ObservableProperty] public partial OperationChoice? BoundOperation { get; set; }

    partial void OnBoundOperationChanged(OperationChoice? value)
    {
        UpdateEnvelopeMessage();
        ScheduleValidation(immediate: true);
    }

    /// <summary>Payload Binding salvo da Operation vinculada ou, sem ele, o Global Element da raiz do Payload.</summary>
    private PayloadBinding? CurrentPayloadBinding =>
        BoundOperation?.Candidate is { } c
            ? _savedPayloads(c.Operation, c.Direction)
              ?? (BoundElement?.Model is { } root ? new PayloadBinding(root, c.Operation.Message(c.Direction).BodyIsString) : null)
            : null;

    private void UpdateEnvelopeMessage()
    {
        if (!IsEnvelope) return;
        BindingMessage = OperationCandidates.Count == 0 ? string.Format(Strings.NoOperation, _envelopeBody)
            : BoundOperation is null ? Strings.ManyOperations
            : CurrentPayloadBinding is not null ? null
            : Candidates.Count > 1 ? Strings.ManyPayloadCandidates
            : Strings.PayloadUnbound;
    }

    private string? _envelopeBody;

    /// <summary>Refaz o Binding contra os Schema Sets abertos, mantendo a escolha atual se ela ainda for candidata.</summary>
    public void RefreshBinding(IEnumerable<SchemaSet> openSets)
    {
        if (!IsDocument) return;
        var binding = DocumentBinding.Find(Document.Text, openSets);
        IsEnvelope = binding.IsEnvelope;
        if (binding.IsEnvelope)
        {
            var previousOperation = BoundOperation?.Candidate;
            var operations = binding.OperationCandidates.Select(c => new OperationChoice(c)).ToList();
            _envelopeBody = operations.Count == 0 ? BodyElementName(Document.Text) : null;
            Candidates = binding.Candidates.Select(c => new GlobalElementViewModel(c, showSourceFile: true)).ToList();
            BoundElement = binding.Bound is { } payloadRoot ? Candidates.Single(c => c.Model == payloadRoot) : null;
            OperationCandidates = operations;
            BoundOperation = operations.FirstOrDefault(o => o.Candidate == previousOperation)
                ?? (binding.BoundOperation is { } only ? operations.Single(o => o.Candidate == only) : null);
            UpdateEnvelopeMessage();
            ScheduleValidation(immediate: true);
            return;
        }
        var candidates = binding.Candidates
            .Select(c => new GlobalElementViewModel(c, showSourceFile: true))
            .ToList();
        var previous = BoundElement?.Model;
        Candidates = candidates;
        BindingMessage = binding.RootName is null ? Strings.Malformed
            : candidates.Count == 0 ? string.Format(Strings.NoCandidate, binding.RootName)
            : candidates.Count > 1 ? Strings.ManyCandidates
            : null;
        BoundElement = candidates.FirstOrDefault(c => c.Model == previous)
            ?? (binding.Bound is { } bound ? candidates.Single(c => c.Model == bound) : null);
        ScheduleValidation(immediate: true);
    }

    public void MarkSaved(string path)
    {
        FilePath = path;
        IsDirty = false;
    }

    /// <summary>Como validar o texto desta aba agora; null quando não há contra o que validar.</summary>
    private Func<string, (IReadOnlyList<ValidationIssue> Issues, string? Payload)>? Validator
    {
        get
        {
            if (_payloadValidator is { } payload) return text => (payload(text), null);
            if (!IsDocument)
                return SelectedSampleItem?.Sample is { } sample ? text => (sample.Validate(text), null) : null;
            if (IsEnvelope)
            {
                if (BoundOperation?.Candidate is not { } c) return null;
                var binding = CurrentPayloadBinding;
                return text =>
                {
                    var result = c.Operation.ValidateEnvelope(text, c.Direction, binding);
                    return (result.Issues, result.DecompressedPayload);
                };
            }
            return BoundElement?.Model is { } element ? text => (element.Validate(text), null) : null;
        }
    }

    private void ScheduleValidation(bool immediate = false)
    {
        var version = ++_validationVersion;
        var validate = Validator;
        var text = Document.Text;
        if (validate is null)
        {
            SetIssues([]);
            return;
        }
        Task.Run(async () =>
        {
            if (!immediate) await Task.Delay(400);
            if (version != _validationVersion) return;
            var (issues, payload) = validate(text);
            Dispatcher.UIThread.Post(() =>
            {
                if (version != _validationVersion) return;
                SetIssues(issues);
                if (IsDocument) DecompressedPayload = payload;
            });
        });
    }

    private static string? BodyElementName(string xml)
    {
        try
        {
            var root = System.Xml.Linq.XDocument.Parse(xml).Root!;
            return root.Elements().FirstOrDefault(e => e.Name.LocalName == "Body")?.Elements().FirstOrDefault()?.Name.LocalName;
        }
        catch (System.Xml.XmlException) { return null; }
    }

    private void SetIssues(IReadOnlyList<ValidationIssue> issues)
    {
        Issues.Clear();
        foreach (var issue in issues) Issues.Add(issue);
        IssuesSummary = issues.Count == 0
            ? Validator is null ? "" : Strings.NoIssues
            : $"{Strings.Issues}: {issues.Count}";
    }

    private void ReplaceText(string text)
    {
        _loadingText = true;
        Document.Text = text;
        _loadingText = false;
    }

    private static string SampleDisplay(Sample sample, int total)
    {
        var name = sample.Kind == SampleKind.Coverage ? string.Format(Strings.SampleOf, sample.Number, total) : sample.FileName;
        return sample.IsValid ? name : $"{name}  ⚠ {Strings.Invalid}";
    }
}
