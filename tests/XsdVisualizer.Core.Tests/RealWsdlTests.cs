namespace XsdVisualizer.Core.Tests;

/// <summary>Integração com WSDLs reais da SEFAZ (NF-e 4.00 e NFCom) e o pacote de schemas da NF-e.</summary>
[Trait("Category", "Integration")]
public class RealWsdlTests
{
    private static readonly SchemaSet Nfe = new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("PL_010_V1.30"));

    private static GlobalElement Element(string name, string file) =>
        Nfe.GlobalElements.Single(e => e.Name == name && Path.GetFileName(e.SourceFile) == file);

    [Fact]
    public void Nfe_authorization_wsdls_load_with_their_endpoints()
    {
        var set = new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("WSDL_NFe4"));

        Assert.Empty(set.LoadIssues);
        Assert.Equal(2, set.Services.Count);
        Assert.All(set.Services, s => Assert.Equal(["nfeAutorizacaoLote", "nfeAutorizacaoLoteZip"], s.Operations.Select(o => o.Name)));
        var svrs = set.Services.Single(s => s.SourceFile.EndsWith("SVRS.wsdl"));
        Assert.Equal([SoapVersion.Soap12, SoapVersion.Soap11], svrs.Operations[0].Endpoints.Select(e => e.SoapVersion));
    }

    [Theory]
    [InlineData("SVRS")]
    [InlineData("SP")]
    public void Nfe_envelopes_with_real_payloads_are_valid(string authority)
    {
        var service = new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("WSDL_NFe4")).Services
            .Single(s => s.SourceFile.EndsWith($"{authority}.wsdl"));
        var lote = service.Operations.Single(o => o.Name == "nfeAutorizacaoLote");
        var zip = service.Operations.Single(o => o.Name == "nfeAutorizacaoLoteZip");
        var enviNFe = new PayloadBinding(Element("enviNFe", "enviNFe_v4.00.xsd"), false);
        var retEnviNFe = new PayloadBinding(Element("retEnviNFe", "retEnviNFe_v4.00.xsd"), false);

        var envelopes = lote.GenerateEnvelopes(MessageDirection.Request, SampleKind.Maximal, payload: enviNFe)
            .Concat(lote.GenerateEnvelopes(MessageDirection.Response, SampleKind.Maximal, payload: retEnviNFe))
            .Concat(zip.GenerateEnvelopes(MessageDirection.Request, SampleKind.Minimal, payload: enviNFe with { Compressed = true }))
            .ToList();

        Assert.All(envelopes, e => Assert.True(e.IsValid, $"{e.FileName}: {string.Join("\n", e.Issues.Take(5))}"));
        Assert.NotNull(envelopes[2].Payload);
    }

    [Fact]
    public void Nfcom_wsdls_load_and_the_reception_body_is_a_string()
    {
        var set = new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("WSDL_NFCom"));

        Assert.Empty(set.LoadIssues);
        Assert.Equal(4, set.Services.Count);
        var recepcao = set.Services.SelectMany(s => s.Operations).Single(o => o.Name == "nfcomRecepcao");
        Assert.True(recepcao.Request.BodyIsString);
        Assert.False(recepcao.Response.BodyIsString);
    }
}
