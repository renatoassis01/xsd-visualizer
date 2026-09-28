namespace XsdVisualizer.Core;

public sealed record ExportResult(int Written, int Invalid);

/// <summary>"Gerar todos": grava Maximal, Minimal e Coverage Set de cada Global Element.</summary>
public static class SampleExporter
{
    /// <summary>
    /// Layout: <c>&lt;saída&gt;/&lt;Schema Set&gt;/&lt;Global Element&gt;/&lt;Global Element&gt;.{max,min,cov-NN}.xml</c>.
    /// Quando o mesmo nome é declarado em mais de um arquivo, a pasta leva o arquivo: "evento (evento-a)".
    /// </summary>
    public static ExportResult ExportAll(SchemaSet set, string outputFolder, IProgress<GlobalElement>? progress = null,
        CancellationToken cancellation = default)
    {
        var duplicated = set.GlobalElements.GroupBy(e => e.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        int written = 0, invalid = 0;
        foreach (var element in set.GlobalElements)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report(element);
            var folderName = duplicated.Contains(element.Name)
                ? $"{element.Name} ({Path.GetFileNameWithoutExtension(element.SourceFile)})"
                : element.Name;
            var folder = Path.Combine(outputFolder, set.Name, folderName);
            Directory.CreateDirectory(folder);
            foreach (var sample in element.GenerateCoverageSet().Prepend(element.GenerateMinimal()).Prepend(element.GenerateMaximal()))
            {
                File.WriteAllText(Path.Combine(folder, sample.FileName), sample.Xml);
                written++;
                if (!sample.IsValid) invalid++;
            }
        }
        return new ExportResult(written, invalid);
    }
}
