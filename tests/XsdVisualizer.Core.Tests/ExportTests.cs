namespace XsdVisualizer.Core.Tests;

public class ExportTests
{
    [Fact]
    public void Exporting_writes_max_min_and_coverage_samples_per_global_element()
    {
        using var folder = new SchemaFolder(
            ("pedido.xsd", SchemaFolder.Xsd("""
                <xs:element name="pedido">
                  <xs:complexType><xs:choice><xs:element name="a" type="xs:string"/><xs:element name="b" type="xs:string"/></xs:choice></xs:complexType>
                </xs:element>
                """)),
            ("evento-a.xsd", SchemaFolder.Xsd("""<xs:element name="evento" type="xs:string"/>""")),
            ("evento-b.xsd", SchemaFolder.Xsd("""<xs:element name="evento" type="xs:int"/>""")));
        using var output = new SchemaFolder();
        var set = new SchemaSetLoader().Open(folder.Path);

        var result = SampleExporter.ExportAll(set, output.Path);

        var root = Path.Combine(output.Path, set.Name);
        Assert.Equal(
            [
                "evento (evento-a)/evento.cov-01.xml", "evento (evento-a)/evento.max.xml", "evento (evento-a)/evento.min.xml",
                "evento (evento-b)/evento.cov-01.xml", "evento (evento-b)/evento.max.xml", "evento (evento-b)/evento.min.xml",
                "pedido/pedido.cov-01.xml", "pedido/pedido.cov-02.xml", "pedido/pedido.max.xml", "pedido/pedido.min.xml",
            ],
            Directory.GetFiles(root, "*.xml", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .Order(StringComparer.Ordinal));
        Assert.Equal((10, 0), (result.Written, result.Invalid));
    }

    [Fact]
    public void Exporting_includes_envelopes_of_operations_with_a_payload_binding()
    {
        using var folder = WsdlFixture.Folder();
        using var output = new SchemaFolder();
        var set = new SchemaSetLoader().Open(folder.Path);
        var pedido = new PayloadBinding(set.GlobalElements.Single(e => e.Name == "pedido"), false);

        var result = SampleExporter.ExportAll(set, output.Path,
            payloadBindings: m => m.Operation.Name == "enviar" && m.Direction == MessageDirection.Request ? pedido : null);

        var services = Path.Combine(output.Path, set.Name, "_servicos", "Pedidos");
        Assert.Equal(
            ["enviar.request.cov-01.xml", "enviar.request.cov-02.xml", "enviar.request.max.xml", "enviar.request.min.xml"],
            Directory.GetFiles(services).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(0, result.Invalid);
    }

    [Fact]
    public void Services_with_the_same_name_in_different_wsdls_get_separate_folders_and_the_chosen_endpoint()
    {
        using var folder = new SchemaFolder(("pedido.xsd", WsdlFixture.PedidoXsd),
            ("A.wsdl", WsdlFixture.Wsdl()), ("B.wsdl", WsdlFixture.Wsdl()));
        using var output = new SchemaFolder();
        var set = new SchemaSetLoader().Open(folder.Path);
        var pedido = new PayloadBinding(set.GlobalElements.Single(e => e.Name == "pedido"), false);

        SampleExporter.ExportAll(set, output.Path,
            payloadBindings: m => m.Operation.Name == "enviar" && m.Direction == MessageDirection.Request ? pedido : null,
            endpoints: o => o.Endpoints.Single(e => e.SoapVersion == SoapVersion.Soap11));

        var services = Path.Combine(output.Path, set.Name, "_servicos");
        Assert.Equal(["Pedidos (A)", "Pedidos (B)"], Directory.GetDirectories(services).Select(Path.GetFileName).Order());
        Assert.Contains("http://schemas.xmlsoap.org/soap/envelope/",
            File.ReadAllText(Path.Combine(services, "Pedidos (A)", "enviar.request.max.xml")));
    }
}
