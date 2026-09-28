using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

public sealed class SchemaSetViewModel : ViewModelBase
{
    public SchemaSetViewModel(SchemaSet model, IPayloadBindings store)
    {
        Model = model;
        GlobalElements = model.GlobalElements.Select(e => new GlobalElementViewModel(e)).ToList();
        Services = model.Services.Select(s => new ServiceViewModel(s, store)).ToList();
        Children = Services.Count == 0
            ? GlobalElements.Cast<object>().ToList()
            : GlobalElements.Cast<object>().Append(new ServicesGroupViewModel(Services)).ToList();
    }

    public IReadOnlyList<ServiceViewModel> Services { get; }
    public IEnumerable<OperationViewModel> Operations => Services.SelectMany(s => s.Operations);
    /// <summary>Filhos na árvore: os Global Elements e, se houver WSDL, o grupo "Serviços".</summary>
    public IReadOnlyList<object> Children { get; }

    public SchemaSet Model { get; }
    public string Name => Model.Name;
    public string Folder => Model.Folder;
    public IReadOnlyList<GlobalElementViewModel> GlobalElements { get; }
    public IReadOnlyList<ValidationIssue> LoadIssues => Model.LoadIssues;
    public bool HasLoadIssues => LoadIssues.Count > 0;
    public string LoadIssuesSummary => string.Format(Strings.LoadIssuesCount, LoadIssues.Count);
    public string LoadIssuesTitle => string.Format(Strings.LoadIssuesTitle, Name);
    public bool HasMissingFiles => Model.MissingFiles.Count > 0;
    public string MissingFilesSummary => string.Format(Strings.MissingFiles, string.Join(", ", Model.MissingFiles));

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(LoadIssuesSummary));
        OnPropertyChanged(nameof(LoadIssuesTitle));
        OnPropertyChanged(nameof(MissingFilesSummary));
    }
}
