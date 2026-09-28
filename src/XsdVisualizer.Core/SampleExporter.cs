namespace XsdVisualizer.Core;

public sealed record ExportResult(int Written, int Invalid);

/// <summary>"Gerar todos": grava Maximal, Minimal e Coverage Set de cada Global Element.</summary>
public static class SampleExporter
{
    /// <summary>
    /// Layout: <c>&lt;saída&gt;/&lt;Schema Set&gt;/&lt;Global Element&gt;/&lt;Global Element&gt;.{max,min,cov-NN}.xml</c>.
    /// Quando o mesmo nome é declarado em mais de um arquivo, a pasta leva o arquivo: "evento (evento-a)".
    /// </summary>
    /// <param name="payloadBindings">Payload Binding de cada Request/Response; Operations sem ele ficam de fora.
    /// Os Envelopes vão para <c>&lt;Schema Set&gt;/_servicos/&lt;Service&gt;/&lt;operation&gt;.{request,response}.{max,min,cov-NN}.xml</c>.</param>
    public static ExportResult ExportAll(SchemaSet set, string outputFolder, IProgress<GlobalElement>? progress = null,
        CancellationToken cancellation = default, Func<Operation, MessageDirection, PayloadBinding?>? payloadBindings = null,
        Func<Operation, Endpoint?>? endpoints = null)
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
        var duplicatedServices = set.Services.GroupBy(s => s.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var service in set.Services)
            foreach (var operation in service.Operations)
                foreach (var direction in new[] { MessageDirection.Request, MessageDirection.Response })
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (payloadBindings?.Invoke(operation, direction) is not { } payload) continue;
                    // Mesmo nome de Service em WSDLs diferentes (ex.: SVRS e SP): a pasta leva o arquivo.
                    var serviceFolder = duplicatedServices.Contains(service.Name)
                        ? $"{service.Name} ({Path.GetFileNameWithoutExtension(service.SourceFile)})"
                        : service.Name;
                    var folder = Path.Combine(outputFolder, set.Name, "_servicos", serviceFolder);
                    Directory.CreateDirectory(folder);
                    var endpoint = endpoints?.Invoke(operation);
                    var envelopes = operation.GenerateEnvelopes(direction, SampleKind.Maximal, endpoint, payload)
                        .Concat(operation.GenerateEnvelopes(direction, SampleKind.Minimal, endpoint, payload))
                        .Concat(operation.GenerateEnvelopes(direction, SampleKind.Coverage, endpoint, payload));
                    foreach (var envelope in envelopes)
                    {
                        File.WriteAllText(Path.Combine(folder, envelope.FileName), envelope.Xml);
                        written++;
                        if (!envelope.IsValid) invalid++;
                    }
                }
        return new ExportResult(written, invalid);
    }
}
