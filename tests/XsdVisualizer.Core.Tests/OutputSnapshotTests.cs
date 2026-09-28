using System.Security.Cryptography;
using System.Text;

namespace XsdVisualizer.Core.Tests;

/// <summary>
/// Rede de segurança de refatoração: a saída do Core para as fixtures reais tem de continuar idêntica, byte a byte.
/// Para regravar de propósito (mudança de comportamento intencional): XSDVIS_UPDATE_SNAPSHOTS=1 dotnet test.
/// </summary>
[Trait("Category", "Integration")]
public class OutputSnapshotTests
{
    private static readonly string SnapshotFile = Path.Combine(
        Path.GetDirectoryName(RealSchemaSetsTests.Fixture("PL_010_V1.30"))!, "..", "XsdVisualizer.Core.Tests", "Snapshots", "outputs.sha256");

    [Fact]
    public void Core_outputs_for_the_real_fixtures_are_unchanged()
    {
        var actual = Outputs().ToDictionary(o => o.Name, o => Hash(o.Content));
        if (Environment.GetEnvironmentVariable("XSDVIS_UPDATE_SNAPSHOTS") == "1")
            File.WriteAllLines(SnapshotFile, actual.Select(kv => $"{kv.Value} {kv.Key}"));

        var expected = File.ReadAllLines(SnapshotFile).Select(l => l.Split(' ', 2)).ToDictionary(p => p[1], p => p[0]);
        var differences = expected.Keys.Union(actual.Keys)
            .Where(k => expected.GetValueOrDefault(k) != actual.GetValueOrDefault(k))
            .ToList();
        Assert.True(differences.Count == 0, $"{differences.Count} saídas mudaram: " + string.Join(", ", differences.Take(20)));
    }

    private static IEnumerable<(string Name, string Content)> Outputs()
    {
        var nfgas = new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("PL_NFGas_NT2026.002_RTC_1.01"));
        foreach (var e in nfgas.GlobalElements)
        {
            var id = $"nfgas/{e.Name}@{Path.GetFileName(e.SourceFile)}";
            yield return ($"{id}/min", e.GenerateMinimal().Xml);
            yield return ($"{id}/max", e.GenerateMaximal().Xml);
            foreach (var s in e.GenerateCoverageSet()) yield return ($"{id}/{s.FileName}", s.Xml + string.Join("|", s.Covers));
        }

        var nfe = new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("PL_010_V1.30"));
        GlobalElement Nfe(string name, string file) => nfe.GlobalElements.Single(x => x.Name == name && Path.GetFileName(x.SourceFile) == file);
        var enviNFe = new PayloadBinding(Nfe("enviNFe", "enviNFe_v4.00.xsd"), false);
        var retEnviNFe = new PayloadBinding(Nfe("retEnviNFe", "retEnviNFe_v4.00.xsd"), false);
        var wsdl = new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("WSDL_NFe4"));
        foreach (var service in wsdl.Services.OrderBy(s => s.SourceFile, StringComparer.Ordinal))
        {
            var id = $"wsdl/{Path.GetFileName(service.SourceFile)}";
            var lote = service.Operations.Single(o => o.Name == "nfeAutorizacaoLote");
            var zip = service.Operations.Single(o => o.Name == "nfeAutorizacaoLoteZip");
            foreach (var endpoint in lote.Endpoints)
            {
                var request = lote.Request.GenerateEnvelopes(SampleKind.Maximal, endpoint, enviNFe)[0];
                yield return ($"{id}/{endpoint.Name}/lote.request.max", request.Xml + Issues(request.Issues));
                var response = lote.Response.GenerateEnvelopes(SampleKind.Minimal, endpoint, retEnviNFe)[0];
                yield return ($"{id}/{endpoint.Name}/lote.response.min", response.Xml + Issues(response.Issues));
            }
            var compressed = zip.Request.GenerateEnvelopes(SampleKind.Minimal, payload: enviNFe with { Compressed = true })[0];
            yield return ($"{id}/zip.request.min", compressed.Xml + compressed.Payload);
            var unbound = lote.Request.GenerateEnvelopes(SampleKind.Maximal)[0];
            yield return ($"{id}/lote.request.unbound", unbound.Xml + Issues(unbound.Issues));
            var invalid = lote.Request.ValidateEnvelope(unbound.Xml.Replace("<nfeDadosMsg", "<nfeResultMsg").Replace("</nfeDadosMsg>", "</nfeResultMsg>"),
                enviNFe);
            yield return ($"{id}/lote.invalid", Issues(invalid.Issues));
        }

        var comparison = Comparison.Compare(new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("PL_009_V4")), nfe);
        yield return ("comparison/markdown", comparison.ToMarkdown());
        yield return ("comparison/markdown+docs", comparison.ToMarkdown(includeDocumentation: true));
        var nfePair = comparison.Pairs.Single(p => p.Name == "NFe" && p.After?.SourceFile.EndsWith("nfe_v4.00.xsd") == true);
        var diff = nfePair.DiffSamples(SampleKind.Maximal);
        yield return ("comparison/nfe.diff", string.Join("\n", diff.Before.Concat(diff.After).Select(l => $"{l.Number}|{l.Kind}|{l.Path}|{l.Text}")));
    }

    private static string Issues(IEnumerable<ValidationIssue> issues) =>
        string.Join("\n", issues.Select(i => $"{i.Line}:{i.Column}:{i.Severity}:{i.InPayload}:{i.Message}"));

    private static string Hash(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
