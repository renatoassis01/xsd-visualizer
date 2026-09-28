namespace XsdVisualizer.Core.Tests;

public class ValidationTests
{
    private static SchemaFolder Pedido() => new(("pedido.xsd", SchemaFolder.Xsd("""
        <xs:element name="pedido">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="numero" type="xs:int"/>
              <xs:element name="cliente" type="xs:string"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        """, "urn:loja")));

    [Fact]
    public void A_valid_xml_has_no_issues()
    {
        using var folder = Pedido();
        var pedido = new SchemaSetLoader().Open(folder.Path).GlobalElements.Single();

        var issues = pedido.Validate("""<pedido xmlns="urn:loja"><numero>1</numero><cliente>Ana</cliente></pedido>""");

        Assert.Empty(issues);
    }

    [Fact]
    public void Invalid_content_is_reported_with_its_position()
    {
        using var folder = Pedido();
        var pedido = new SchemaSetLoader().Open(folder.Path).GlobalElements.Single();

        var issues = pedido.Validate("""
            <pedido xmlns="urn:loja">
              <numero>um</numero>
              <cliente>Ana</cliente>
            </pedido>
            """);

        var issue = Assert.Single(issues);
        Assert.Equal(2, issue.Line);
        Assert.True(issue.Column > 0);
        Assert.Contains("numero", issue.Message);
    }

    [Fact]
    public void Malformed_xml_is_reported_as_an_issue()
    {
        using var folder = Pedido();
        var pedido = new SchemaSetLoader().Open(folder.Path).GlobalElements.Single();

        var issues = pedido.Validate("<pedido xmlns=\"urn:loja\">\n<numero>1</pedido>");

        var issue = Assert.Single(issues);
        Assert.Equal(2, issue.Line);
    }
}
