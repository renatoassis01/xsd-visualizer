namespace XsdVisualizer.Core.Tests;

public class WsdlLoadingTests
{
    [Fact]
    public void Wsdl_files_in_the_folder_become_services_with_endpoints_and_operations()
    {
        using var folder = WsdlFixture.Folder();

        var set = new SchemaSetLoader().Open(folder.Path);

        Assert.Empty(set.LoadIssues);
        var service = Assert.Single(set.Services);
        Assert.Equal("Pedidos", service.Name);
        Assert.Equal(folder.PathOf("Pedidos.wsdl"), service.SourceFile);
        Assert.Equal(
            ["PedidosSoap11 Soap11 https://exemplo.com/pedidos.asmx", "PedidosSoap12 Soap12 https://exemplo.com/pedidos.asmx"],
            service.Endpoints.Select(e => $"{e.Name} {e.SoapVersion} {e.Address}"));
        Assert.Equal(["enviar", "enviarZip"], service.Operations.Select(o => o.Name));

        var enviar = service.Operations[0];
        Assert.Equal(SoapVersion.Soap12, enviar.DefaultEndpoint.SoapVersion);
        Assert.Equal($"{WsdlFixture.Tns}/enviar", enviar.DefaultEndpoint.SoapActionOf(enviar));
        Assert.Equal(("dadosMsg", false), (enviar.Request.BodyElementName, enviar.Request.BodyIsString));
        Assert.Equal("resultMsg", enviar.Response.BodyElementName);
        Assert.Equal(["monitoria"], enviar.Response.HeaderNames);
        Assert.True(service.Operations[1].Request.BodyIsString);
    }

    [Fact]
    public void Wrapper_elements_of_a_wsdl_are_not_listed_as_global_elements()
    {
        using var folder = WsdlFixture.Folder();

        var set = new SchemaSetLoader().Open(folder.Path);

        Assert.Equal(["pedido", "retPedido"], set.GlobalElements.Select(e => e.Name));
    }

    [Theory]
    [InlineData("rpc", "literal", "rpc")]
    [InlineData("document", "encoded", "encoded")]
    public void Rpc_and_encoded_operations_are_load_issues(string style, string use, string expected)
    {
        using var folder = WsdlFixture.Folder("12", style, use);

        var set = new SchemaSetLoader().Open(folder.Path);

        Assert.Empty(set.Services);
        Assert.Contains(set.LoadIssues, i => i.Message.Contains(expected) && i.File == folder.PathOf("Pedidos.wsdl") && i.Line > 0);
    }

    [Fact]
    public void Wsdl_2_0_and_malformed_files_are_load_issues()
    {
        using var folder = new SchemaFolder(
            ("v2.wsdl", """<description xmlns="http://www.w3.org/ns/wsdl" targetNamespace="urn:x"/>"""),
            ("quebrado.wsdl", "<wsdl:definitions xmlns:wsdl=\"http://schemas.xmlsoap.org/wsdl/\">"));

        var set = new SchemaSetLoader().Open(folder.Path);

        Assert.Empty(set.Services);
        Assert.Contains(set.LoadIssues, i => i.Message.Contains("WSDL 2.0") && i.File == folder.PathOf("v2.wsdl"));
        Assert.Contains(set.LoadIssues, i => i.File == folder.PathOf("quebrado.wsdl"));
    }

    [Fact]
    public void Opening_a_loose_wsdl_opens_its_folder()
    {
        using var folder = WsdlFixture.Folder();

        var set = new SchemaSetLoader().Open(folder.PathOf("Pedidos.wsdl"));

        Assert.Equal(folder.Path, set.Folder);
        Assert.Single(set.Services);
    }
}
