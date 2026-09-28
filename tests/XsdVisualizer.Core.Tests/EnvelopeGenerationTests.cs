using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace XsdVisualizer.Core.Tests;

public class EnvelopeGenerationTests
{
    private static readonly XNamespace Soap12 = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace Soap11 = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Tns = WsdlFixture.Tns;
    private static readonly XNamespace Loja = "urn:loja";

    private static (SchemaSet Set, Operation Enviar, Operation EnviarZip) Open(SchemaFolder folder)
    {
        var set = new SchemaSetLoader().Open(folder.Path);
        var service = set.Services.Single();
        return (set, service.Operations.Single(o => o.Name == "enviar"), service.Operations.Single(o => o.Name == "enviarZip"));
    }

    private static PayloadBinding Payload(SchemaSet set, string element, bool compressed = false) =>
        new(set.GlobalElements.Single(e => e.Name == element), compressed);

    [Fact]
    public void A_request_envelope_wraps_the_payload_in_the_body_of_the_default_soap_1_2_endpoint()
    {
        using var folder = WsdlFixture.Folder();
        var (set, enviar, _) = Open(folder);

        var envelope = Assert.Single(enviar.Request.GenerateEnvelopes(SampleKind.Maximal, payload: Payload(set, "pedido")));

        Assert.True(envelope.IsValid, string.Join("\n", envelope.Issues) + "\n" + envelope.Xml);
        var root = XDocument.Parse(envelope.Xml).Root!;
        Assert.Equal(Soap12 + "Envelope", root.Name);
        Assert.Null(root.Element(Soap12 + "Header"));
        var pedido = root.Element(Soap12 + "Body")!.Element(Tns + "dadosMsg")!.Element(Loja + "pedido");
        Assert.NotNull(pedido);
        Assert.Equal("enviar.request.max.xml", envelope.FileName);
        Assert.Same(enviar, envelope.Operation);
    }

    [Fact]
    public void Without_a_payload_binding_the_envelope_is_marked_invalid_and_says_why()
    {
        using var folder = WsdlFixture.Folder();
        var (_, enviar, _) = Open(folder);

        var envelope = Assert.Single(enviar.Request.GenerateEnvelopes(SampleKind.Maximal));

        Assert.False(envelope.IsValid);
        Assert.Contains(envelope.Issues, i => i.Message.Contains("Payload Binding"));
    }

    [Fact]
    public void A_compressed_payload_travels_as_gzip_base64_and_stays_readable_on_the_sample()
    {
        using var folder = WsdlFixture.Folder();
        var (set, _, enviarZip) = Open(folder);

        var envelope = Assert.Single(enviarZip.Request.GenerateEnvelopes(SampleKind.Minimal,
            payload: Payload(set, "pedido", compressed: true)));

        Assert.True(envelope.IsValid, string.Join("\n", envelope.Issues));
        var text = XDocument.Parse(envelope.Xml).Root!.Element(Soap12 + "Body")!.Element(Tns + "dadosMsgZip")!.Value;
        using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(text)), CompressionMode.Decompress);
        var decompressed = XDocument.Parse(new StreamReader(gzip, Encoding.UTF8).ReadToEnd()).Root!;
        Assert.Equal(Loja + "pedido", decompressed.Name);
        Assert.Equal(decompressed.ToString(), XDocument.Parse(envelope.Payload!).Root!.ToString());
    }

    [Fact]
    public void Declared_headers_go_into_the_header_and_the_soap_version_follows_the_endpoint()
    {
        using var folder = WsdlFixture.Folder();
        var (set, enviar, _) = Open(folder);
        var soap11 = enviar.Endpoints.Single(e => e.SoapVersion == SoapVersion.Soap11);

        var envelope = Assert.Single(enviar.Response.GenerateEnvelopes(SampleKind.Maximal, soap11, Payload(set, "retPedido")));

        Assert.True(envelope.IsValid, string.Join("\n", envelope.Issues) + "\n" + envelope.Xml);
        var root = XDocument.Parse(envelope.Xml).Root!;
        Assert.Equal(Soap11 + "Envelope", root.Name);
        Assert.NotNull(root.Element(Soap11 + "Header")!.Element(Tns + "monitoria"));
        Assert.NotNull(root.Element(Soap11 + "Body")!.Element(Tns + "resultMsg")!.Element(Loja + "retPedido"));
        Assert.Equal("enviar.response.max.xml", envelope.FileName);
    }

    [Fact]
    public void The_coverage_set_of_envelopes_follows_the_coverage_set_of_the_payload()
    {
        using var folder = WsdlFixture.Folder();
        var (set, enviar, _) = Open(folder);
        var pedido = Payload(set, "pedido");

        var envelopes = enviar.Request.GenerateEnvelopes(SampleKind.Coverage, payload: pedido);

        Assert.Equal(pedido.Element.GenerateCoverageSet().Count, envelopes.Count);
        Assert.All(envelopes, e => Assert.True(e.IsValid, string.Join("\n", e.Issues)));
        Assert.Equal(["enviar.request.cov-01.xml", "enviar.request.cov-02.xml"], envelopes.Select(e => e.FileName));
        var comment = XDocument.Parse(envelopes[0].Xml).Nodes().OfType<XComment>().Single().Value;
        Assert.All(envelopes[0].Covers, c => Assert.Contains(c, comment));
    }

    [Fact]
    public void Envelopes_are_deterministic()
    {
        using var folder = WsdlFixture.Folder();
        var (set, enviar, _) = Open(folder);

        string Generate() => enviar.Request.GenerateEnvelopes(SampleKind.Maximal, payload: Payload(set, "pedido"))[0].Xml;

        Assert.Equal(Generate(), Generate());
    }

    [Fact]
    public void The_maximal_envelope_honors_branches_pinned_in_the_payload_tree()
    {
        using var folder = WsdlFixture.Folder();
        var (set, enviar, _) = Open(folder);
        var pedido = Payload(set, "pedido");
        var choice = pedido.Element.Tree.Children.Single().Children.Single(c => c.Kind == NodeKind.Choice);

        var envelope = enviar.Request.GenerateEnvelopes(SampleKind.Maximal, payload: pedido,
            pins: new Dictionary<string, int> { [choice.Path] = 1 })[0];

        Assert.Contains("<CNPJ>", envelope.Xml);
        Assert.DoesNotContain("<CPF>", envelope.Xml);
    }
}
