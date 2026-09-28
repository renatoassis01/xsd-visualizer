namespace XsdVisualizer.Core.Tests;

public class OpeningSchemaSetsTests
{
    [Fact]
    public void Opening_a_folder_lists_its_global_elements()
    {
        using var folder = new SchemaFolder(("pedido.xsd", SchemaFolder.Xsd("""
            <xs:element name="pedido" type="xs:string"/>
            <xs:element name="cliente" type="xs:string"/>
            """, "urn:loja")));

        var set = new SchemaSetLoader().Open(folder.Path);

        Assert.Empty(set.LoadIssues);
        Assert.Equal(["cliente", "pedido"], set.GlobalElements.Select(e => e.Name).Order());
        Assert.All(set.GlobalElements, e => Assert.Equal("urn:loja", e.Namespace));
    }

    [Fact]
    public void Included_files_are_resolved_and_each_element_is_listed_once_per_declaring_file()
    {
        using var folder = new SchemaFolder(
            ("tipos.xsd", SchemaFolder.Xsd("""
                <xs:simpleType name="TCodigo"><xs:restriction base="xs:string"><xs:length value="3"/></xs:restriction></xs:simpleType>
                <xs:element name="assinatura" type="xs:string"/>
                """, "urn:loja")),
            ("pedido.xsd", SchemaFolder.Xsd("""
                <xs:include schemaLocation="tipos.xsd"/>
                <xs:element name="pedido" type="TCodigo"/>
                """, "urn:loja")),
            ("evento-a.xsd", SchemaFolder.Xsd("""
                <xs:include schemaLocation="tipos.xsd"/>
                <xs:element name="evento" type="TCodigo"/>
                """, "urn:loja")),
            ("evento-b.xsd", SchemaFolder.Xsd("""
                <xs:include schemaLocation="tipos.xsd"/>
                <xs:element name="evento" type="xs:int"/>
                """, "urn:loja")));

        var set = new SchemaSetLoader().Open(folder.Path);

        Assert.Empty(set.LoadIssues);
        Assert.Equal(
            ["assinatura@tipos.xsd", "evento@evento-a.xsd", "evento@evento-b.xsd", "pedido@pedido.xsd"],
            set.GlobalElements.Select(e => $"{e.Name}@{Path.GetFileName(e.SourceFile)}").Order());
    }

    [Fact]
    public void Broken_files_become_load_issues_without_hiding_the_healthy_ones()
    {
        using var folder = new SchemaFolder(
            ("ok.xsd", SchemaFolder.Xsd("""<xs:element name="ok" type="xs:string"/>""")),
            ("malformado.xsd", "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\">\n<xs:element name=\"x\">\n</xs:schema>"),
            ("tipo-inexistente.xsd", SchemaFolder.Xsd("""

                <xs:element name="quebrado" type="TNaoExiste"/>
                """)));

        var set = new SchemaSetLoader().Open(folder.Path);

        Assert.Equal(["ok"], set.GlobalElements.Select(e => e.Name));
        Assert.Collection(set.LoadIssues.OrderBy(i => i.File),
            malformado =>
            {
                Assert.Equal(folder.PathOf("malformado.xsd"), malformado.File);
                Assert.Equal(3, malformado.Line);
            },
            tipo =>
            {
                Assert.Equal(folder.PathOf("tipo-inexistente.xsd"), tipo.File);
                Assert.Contains("TNaoExiste", tipo.Message);
                Assert.Equal(4, tipo.Line);
            });
    }

    [Fact]
    public void Opening_a_loose_xsd_opens_its_whole_folder()
    {
        using var folder = new SchemaFolder(
            ("a.xsd", SchemaFolder.Xsd("""<xs:element name="a" type="xs:string"/>""")),
            ("b.xsd", SchemaFolder.Xsd("""<xs:element name="b" type="xs:string"/>""")));

        var set = new SchemaSetLoader().Open(folder.PathOf("a.xsd"));

        Assert.Equal(folder.Path, set.Folder);
        Assert.Equal(["a", "b"], set.GlobalElements.Select(e => e.Name));
    }

    [Fact]
    public void Xsd_1_1_constructs_are_reported_as_load_issues()
    {
        using var folder = new SchemaFolder(("v11.xsd", SchemaFolder.Xsd("""
            <xs:element name="faixa">
              <xs:complexType>
                <xs:sequence><xs:element name="min" type="xs:int"/><xs:element name="max" type="xs:int"/></xs:sequence>
                <xs:assert test="min le max"/>
              </xs:complexType>
            </xs:element>
            """)));

        var set = new SchemaSetLoader().Open(folder.Path);

        var issue = Assert.Single(set.LoadIssues, i => i.Message.Contains("XSD 1.1"));
        Assert.Contains("assert", issue.Message);
        Assert.Equal(folder.PathOf("v11.xsd"), issue.File);
        Assert.Equal(6, issue.Line);
    }
}
