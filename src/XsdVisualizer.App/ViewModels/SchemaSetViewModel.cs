using XsdVisualizer.App.Resources;
using XsdVisualizer.Core;

namespace XsdVisualizer.App.ViewModels;

public sealed class SchemaSetViewModel : ViewModelBase
{
    public SchemaSetViewModel(SchemaSet model)
    {
        Model = model;
        var duplicated = model.GlobalElements.GroupBy(e => e.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        GlobalElements = model.GlobalElements.Select(e => new GlobalElementViewModel(e, duplicated.Contains(e.Name))).ToList();
    }

    public SchemaSet Model { get; }
    public string Name => Model.Name;
    public string Folder => Model.Folder;
    public IReadOnlyList<GlobalElementViewModel> GlobalElements { get; }
    public IReadOnlyList<ValidationIssue> LoadIssues => Model.LoadIssues;
    public bool HasLoadIssues => LoadIssues.Count > 0;
    public string LoadIssuesSummary => string.Format(Strings.LoadIssuesCount, LoadIssues.Count);
}
