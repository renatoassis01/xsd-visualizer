namespace XsdVisualizer.Core;

/// <summary>Todos os XSDs de uma pasta, com includes/imports resolvidos.</summary>
public sealed class SchemaSet
{
    internal SchemaSet(string folder, IReadOnlyList<GlobalElement> globalElements, IReadOnlyList<ValidationIssue> loadIssues,
        IReadOnlyList<Service> services)
    {
        Folder = folder;
        GlobalElements = globalElements;
        LoadIssues = loadIssues;
        Services = services;
        foreach (var element in globalElements)
            element.SchemaSet = this;
        foreach (var service in services)
            service.SchemaSet = this;
    }

    public string Folder { get; }
    public string Name => Path.GetFileName(Folder);
    public IReadOnlyList<GlobalElement> GlobalElements { get; }
    public IReadOnlyList<ValidationIssue> LoadIssues { get; }
    /// <summary>Services dos WSDLs da pasta (ADR-0004).</summary>
    public IReadOnlyList<Service> Services { get; }
}
