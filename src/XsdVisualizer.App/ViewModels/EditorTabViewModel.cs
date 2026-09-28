using System.Collections.ObjectModel;
using AvaloniaEdit.Document;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

public sealed record SampleItem(Sample Sample, string Display);

/// <summary>Uma aba do editor: um Document (XML do usuário) ou Samples gerados.</summary>
public sealed partial class EditorTabViewModel : ViewModelBase
{
    private int _validationVersion;
    private bool _loadingText;

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

    public static EditorTabViewModel ForDocument(string path, string text, IEnumerable<SchemaSet> openSets)
    {
        var tab = new EditorTabViewModel(Path.GetFileName(path), text) { FilePath = path, IsDocument = true };
        tab.RefreshBinding(openSets);
        return tab;
    }

    public static EditorTabViewModel ForSamples(string title, IReadOnlyList<Sample> samples)
    {
        var tab = new EditorTabViewModel(title, "")
        {
            SampleItems = samples.Select(s => new SampleItem(s, SampleDisplay(s, samples.Count))).ToList(),
        };
        tab.SelectedSampleItem = tab.SampleItems[0];
        return tab;
    }

    public string Title { get; }
    public TextDocument Document { get; }
    public string? FilePath { get; private set; }
    public bool IsDocument { get; private init; }

    [ObservableProperty] public partial bool IsDirty { get; set; }

    public ObservableCollection<ValidationIssue> Issues { get; } = [];
    [ObservableProperty] public partial string IssuesSummary { get; set; } = "";

    // ---- Samples ----
    public IReadOnlyList<SampleItem> SampleItems { get; private init; } = [];
    public bool HasSeveralSamples => SampleItems.Count > 1;

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
    }

    public string SuggestedFileName => SelectedSampleItem?.Sample.FileName ?? Title;

    // ---- Binding (Documents) ----
    [ObservableProperty] public partial IReadOnlyList<GlobalElementViewModel> Candidates { get; set; } = [];
    [ObservableProperty] public partial GlobalElementViewModel? BoundElement { get; set; }
    [ObservableProperty] public partial string? BindingMessage { get; set; }
    public bool HasSeveralCandidates => Candidates.Count > 1;

    partial void OnCandidatesChanged(IReadOnlyList<GlobalElementViewModel> value) => OnPropertyChanged(nameof(HasSeveralCandidates));

    partial void OnBoundElementChanged(GlobalElementViewModel? value)
    {
        BindingMessage = value is null && Candidates.Count > 1 ? Strings.ManyCandidates : BindingMessage;
        if (value is not null && Candidates.Count > 0) BindingMessage = null;
        ScheduleValidation(immediate: true);
    }

    /// <summary>Refaz o Binding contra os Schema Sets abertos, mantendo a escolha atual se ela ainda for candidata.</summary>
    public void RefreshBinding(IEnumerable<SchemaSet> openSets)
    {
        if (!IsDocument) return;
        var binding = DocumentBinding.Find(Document.Text, openSets);
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

    private GlobalElement? ValidationTarget => IsDocument ? BoundElement?.Model : SelectedSampleItem?.Sample.Element;

    private void ScheduleValidation(bool immediate = false)
    {
        var version = ++_validationVersion;
        var target = ValidationTarget;
        var text = Document.Text;
        if (target is null)
        {
            SetIssues([]);
            return;
        }
        Task.Run(async () =>
        {
            if (!immediate) await Task.Delay(400);
            if (version != _validationVersion) return;
            var issues = target.Validate(text);
            Dispatcher.UIThread.Post(() =>
            {
                if (version == _validationVersion) SetIssues(issues);
            });
        });
    }

    private void SetIssues(IReadOnlyList<ValidationIssue> issues)
    {
        Issues.Clear();
        foreach (var issue in issues) Issues.Add(issue);
        IssuesSummary = issues.Count == 0
            ? ValidationTarget is null ? "" : Strings.NoIssues
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
