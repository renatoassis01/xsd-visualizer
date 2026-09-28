namespace XsdVisualizer.Core.Tests;

public class EnvelopeValidationTests
{
    private const string Soap12 = "http://www.w3.org/2003/05/soap-envelope";

    private static (SchemaSet Set, Operation Enviar, Operation EnviarZip, PayloadBinding Pedido) Open(SchemaFolder folder)
    {
        var set = new SchemaSetLoader().Open(folder.Path);
        var service = set.Services.Single();
        return (set, service.Operations.Single(o => o.Name == "enviar"), service.Operations.Single(o => o.Name == "enviarZip"),
            new PayloadBinding(set.GlobalElements.Single(e => e.Name == "pedido"), false));
    }

    [Fact]
    public void An_invalid_value_inside_the_payload_is_reported_at_its_line_in_the_envelope()
    {
        using var folder = WsdlFixture.Folder();
        var (_, enviar, _, pedido) = Open(folder);

        var result = enviar.ValidateEnvelope($"""
            <soap:Envelope xmlns:soap="{Soap12}">
              <soap:Body>
                <dadosMsg xmlns="{WsdlFixture.Tns}">
                  <pedido xmlns="urn:loja">
                    <numero>1</numero>
                    <CPF>123</CPF>
                    <uf>MG</uf>
                  </pedido>
                </dadosMsg>
              </soap:Body>
            </soap:Envelope>
            """, MessageDirection.Request, pedido);

        var issue = Assert.Single(result.Issues);
        Assert.Equal(7, issue.Line);
        Assert.False(issue.InPayload);
    }

    [Fact]
    public void A_body_with_the_wrong_element_is_reported()
    {
        using var folder = WsdlFixture.Folder();
        var (_, enviar, _, pedido) = Open(folder);

        var result = enviar.ValidateEnvelope($"""
            <soap:Envelope xmlns:soap="{Soap12}"><soap:Body><resultMsg xmlns="{WsdlFixture.Tns}"/></soap:Body></soap:Envelope>
            """, MessageDirection.Request, pedido);

        Assert.Contains(result.Issues, i => i.Message.Contains("dadosMsg"));
    }

    [Fact]
    public void A_compressed_payload_is_decompressed_and_its_issues_point_into_the_decompressed_text()
    {
        using var folder = WsdlFixture.Folder();
        var (_, _, enviarZip, pedido) = Open(folder);
        var compressed = pedido with { Compressed = true };
        var payload = """<pedido xmlns="urn:loja"><numero>um</numero><CPF>1</CPF><uf>SP</uf></pedido>""";
        var base64 = Convert.ToBase64String(Gzip(payload));

        var result = enviarZip.ValidateEnvelope($"""
            <soap:Envelope xmlns:soap="{Soap12}"><soap:Body><dadosMsgZip xmlns="{WsdlFixture.Tns}">{base64}</dadosMsgZip></soap:Body></soap:Envelope>
            """, MessageDirection.Request, compressed);

        var issue = Assert.Single(result.Issues);
        Assert.True(issue.InPayload);
        Assert.Contains("numero", issue.Message);
        Assert.NotNull(result.DecompressedPayload);
        Assert.Contains("<numero>um</numero>", result.DecompressedPayload!.Split('\n')[issue.Line - 1]);
    }

    [Fact]
    public void A_body_that_is_not_gzip_base64_is_reported()
    {
        using var folder = WsdlFixture.Folder();
        var (_, _, enviarZip, pedido) = Open(folder);

        var result = enviarZip.ValidateEnvelope($"""
            <soap:Envelope xmlns:soap="{Soap12}"><soap:Body><dadosMsgZip xmlns="{WsdlFixture.Tns}">não é base64</dadosMsgZip></soap:Body></soap:Envelope>
            """, MessageDirection.Request, pedido with { Compressed = true });

        Assert.Contains(result.Issues, i => i.Message.Contains("gzip+base64"));
    }

    private static byte[] Gzip(string text)
    {
        var buffer = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(buffer, System.IO.Compression.CompressionMode.Compress, true))
            gzip.Write(System.Text.Encoding.UTF8.GetBytes(text));
        return buffer.ToArray();
    }

    [Fact]
    public void A_payload_whose_root_is_not_the_bound_global_element_is_reported()
    {
        using var folder = WsdlFixture.Folder();
        var (_, enviar, _, pedido) = Open(folder);

        var result = enviar.ValidateEnvelope($"""
            <soap:Envelope xmlns:soap="{Soap12}"><soap:Body>
              <dadosMsg xmlns="{WsdlFixture.Tns}"><retPedido xmlns="urn:loja"><cStat>1</cStat></retPedido></dadosMsg>
            </soap:Body></soap:Envelope>
            """, MessageDirection.Request, pedido);

        Assert.Contains(result.Issues, i => i.Message.Contains("pedido") && i.Message.Contains("retPedido"));
    }
}
