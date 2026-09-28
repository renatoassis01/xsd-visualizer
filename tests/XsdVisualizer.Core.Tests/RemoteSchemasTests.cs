namespace XsdVisualizer.Core.Tests;

public class RemoteSchemasTests
{
    private sealed class FakeDownloader(Dictionary<string, string>? responses = null) : ISchemaDownloader
    {
        public List<Uri> Requests { get; } = [];

        public byte[]? Download(Uri uri)
        {
            Requests.Add(uri);
            return responses is not null && responses.TryGetValue(uri.ToString(), out var content)
                ? System.Text.Encoding.UTF8.GetBytes(content)
                : null;
        }
    }

    private static SchemaFolder WithImport(string location, string ns) => new(("pedido.xsd", $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:r="{ns}" targetNamespace="urn:loja" elementFormDefault="qualified">
          <xs:import namespace="{ns}" schemaLocation="{location}"/>
          <xs:element name="pedido"><xs:complexType><xs:sequence><xs:element ref="r:{(ns.Contains("xmldsig") ? "Signature" : "remoto")}"/></xs:sequence></xs:complexType></xs:element>
        </xs:schema>
        """));

    [Fact]
    public void Common_w3c_schemas_are_embedded_and_work_offline()
    {
        using var folder = WithImport("http://www.w3.org/TR/xmldsig-core/xmldsig-core-schema.xsd", "http://www.w3.org/2000/09/xmldsig#");
        using var cache = new SchemaFolder();
        var downloader = new FakeDownloader();

        var set = new SchemaSetLoader(downloader, cache.Path).Open(folder.Path);

        Assert.Empty(set.LoadIssues);
        Assert.Empty(downloader.Requests);
        Assert.True(set.GlobalElements.Single(e => e.Name == "pedido").GenerateMaximal().IsValid);
    }

    [Fact]
    public void Other_remote_schemas_are_downloaded_once_and_then_served_from_the_cache()
    {
        const string url = "https://exemplo.com/schemas/remoto.xsd";
        using var folder = WithImport(url, "urn:remoto");
        using var cache = new SchemaFolder();
        var online = new FakeDownloader(new()
        {
            [url] = """
                <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" targetNamespace="urn:remoto">
                  <xs:element name="remoto" type="xs:string"/>
                </xs:schema>
                """,
        });

        var first = new SchemaSetLoader(online, cache.Path).Open(folder.Path);
        var offline = new FakeDownloader();
        var second = new SchemaSetLoader(offline, cache.Path).Open(folder.Path);

        Assert.Empty(first.LoadIssues);
        Assert.Equal([new Uri(url)], online.Requests);
        Assert.Empty(second.LoadIssues);
        Assert.Empty(offline.Requests);
    }

    [Fact]
    public void An_unreachable_remote_schema_without_cache_is_a_load_issue()
    {
        const string url = "https://exemplo.com/schemas/sumiu.xsd";
        using var folder = WithImport(url, "urn:remoto");
        using var cache = new SchemaFolder();

        var set = new SchemaSetLoader(new FakeDownloader(), cache.Path).Open(folder.Path);

        Assert.Contains(set.LoadIssues, i => i.Message.Contains(url) && i.File == folder.PathOf("pedido.xsd"));
    }
}
