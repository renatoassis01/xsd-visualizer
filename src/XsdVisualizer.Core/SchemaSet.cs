namespace XsdVisualizer.Core;

/// <summary>Todos os XSDs de uma pasta, com includes/imports resolvidos.</summary>
public sealed class SchemaSet
{
    internal SchemaSet(string folder, IReadOnlyList<GlobalElement> globalElements, IReadOnlyList<ValidationIssue> loadIssues)
    {
        Folder = folder;
        GlobalElements = globalElements;
        LoadIssues = loadIssues;
        foreach (var element in globalElements)
            element.SchemaSet = this;
    }

    public string Folder { get; }
    public string Name => Path.GetFileName(Folder);
    public IReadOnlyList<GlobalElement> GlobalElements { get; }
    public IReadOnlyList<ValidationIssue> LoadIssues { get; }
}
