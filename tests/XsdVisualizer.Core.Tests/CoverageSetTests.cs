using System.Xml.Linq;

namespace XsdVisualizer.Core.Tests;

public class CoverageSetTests
{
    private static GlobalElement Pedido(SchemaFolder folder) =>
        new SchemaSetLoader().Open(folder.Path).GlobalElements.Single(e => e.Name == "pedido");

    private static SchemaFolder Folder() => new(("pedido.xsd", SchemaFolder.Xsd("""
        <xs:element name="pedido">
          <xs:complexType>
            <xs:sequence>
              <xs:choice>
                <xs:element name="pf">
                  <xs:complexType><xs:choice><xs:element name="CPF" type="xs:string"/><xs:element name="RG" type="xs:string"/></xs:choice></xs:complexType>
                </xs:element>
                <xs:element name="pj"><xs:complexType><xs:sequence><xs:element name="CNPJ" type="xs:string"/></xs:sequence></xs:complexType></xs:element>
              </xs:choice>
              <xs:element name="uf">
                <xs:simpleType><xs:restriction base="xs:string"><xs:enumeration value="SP"/><xs:enumeration value="RJ"/><xs:enumeration value="MG"/></xs:restriction></xs:simpleType>
              </xs:element>
              <xs:element name="obs" type="xs:string" minOccurs="0"/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        """)));

    [Fact]
    public void Every_branch_optional_and_enumeration_value_appears_in_some_valid_sample()
    {
        using var folder = Folder();

        var samples = Pedido(folder).GenerateCoverageSet();

        Assert.All(samples, s => Assert.True(s.IsValid, string.Join("\n", s.Issues)));
        var documents = samples.Select(s => XDocument.Parse(s.Xml)).ToList();
        var names = documents.SelectMany(d => d.Descendants()).Select(e => e.Name.LocalName).ToHashSet();
        Assert.Superset(new HashSet<string> { "pf", "pj", "CPF", "RG", "CNPJ", "obs" }, names);
        Assert.Equal(["MG", "RJ", "SP"], documents.Select(d => d.Root!.Element("uf")!.Value).Distinct().Order());
    }

    [Fact]
    public void The_set_stays_close_to_the_largest_choice_or_enumeration_instead_of_every_combination()
    {
        using var folder = Folder();

        var samples = Pedido(folder).GenerateCoverageSet();

        // pf/CPF, pf/RG e pj formam 3 caminhos e uf tem 3 valores: 3 Samples bastam (o produto seria 9).
        Assert.Equal(3, samples.Count);
        Assert.Equal(["pedido.cov-01.xml", "pedido.cov-02.xml", "pedido.cov-03.xml"], samples.Select(s => s.FileName));
    }

    [Fact]
    public void Each_sample_says_what_it_covers_in_a_comment_at_the_top()
    {
        using var folder = Folder();

        var first = Pedido(folder).GenerateCoverageSet()[0];

        Assert.Equal(["pedido: pf", "pedido/pf: CPF", "pedido/uf = SP"], first.Covers);
        var comment = XDocument.Parse(first.Xml).Nodes().OfType<XComment>().Single();
        Assert.All(first.Covers, c => Assert.Contains(c, comment.Value));
    }
}
