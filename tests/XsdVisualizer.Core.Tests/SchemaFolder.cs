namespace XsdVisualizer.Core.Tests;

/// <summary>Pasta temporária com XSDs de fixture, apagada no Dispose.</summary>
public sealed class SchemaFolder : IDisposable
{
    public string Path { get; }

    public SchemaFolder(params (string File, string Content)[] files)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xsdvis-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        foreach (var (file, content) in files)
            Write(file, content);
    }

    public string Write(string file, string content)
    {
        var full = System.IO.Path.Combine(Path, file);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return full;
    }

    public string PathOf(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }

    public static string Xsd(string body, string? targetNamespace = null) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"{(targetNamespace is null ? "" : $" targetNamespace=\"{targetNamespace}\" xmlns=\"{targetNamespace}\"")} elementFormDefault="qualified">
        {body}
        </xs:schema>
        """;
}
