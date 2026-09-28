namespace XsdVisualizer.Core.Tests;

public class SchemaTreeTests
{
    private static GlobalElement Open(SchemaFolder folder, string name) =>
        new SchemaSetLoader().Open(folder.Path).GlobalElements.Single(e => e.Name == name);

    [Fact]
    public void The_tree_shows_structure_cardinality_types_facets_and_documentation()
    {
        using var folder = new SchemaFolder(("pedido.xsd", SchemaFolder.Xsd("""
            <xs:simpleType name="TUf">
              <xs:restriction base="xs:string"><xs:enumeration value="SP"/><xs:enumeration value="RJ"/></xs:restriction>
            </xs:simpleType>
            <xs:element name="pedido">
              <xs:annotation><xs:documentation>Pedido de compra</xs:documentation></xs:annotation>
              <xs:complexType>
                <xs:sequence>
                  <xs:element name="uf" type="TUf"/>
                  <xs:element name="obs" minOccurs="0">
                    <xs:simpleType><xs:restriction base="xs:string"><xs:maxLength value="60"/></xs:restriction></xs:simpleType>
                  </xs:element>
                  <xs:element name="item" type="xs:string" maxOccurs="unbounded"/>
                </xs:sequence>
                <xs:attribute name="versao" type="xs:string" use="required" fixed="1.00"/>
              </xs:complexType>
            </xs:element>
            """, "urn:loja")));

        var root = Open(folder, "pedido").Tree;

        Assert.Equal(NodeKind.Element, root.Kind);
        Assert.Equal("Pedido de compra", root.Documentation);
        Assert.Equal(["@versao", "sequence"], root.Children.Select(c => c.Label));

        var versao = root.Children[0];
        Assert.Equal(NodeKind.Attribute, versao.Kind);
        Assert.Equal("1..1", versao.Cardinality);
        Assert.Equal("1.00", versao.FixedValue);

        var sequence = root.Children[1];
        Assert.Equal(NodeKind.Sequence, sequence.Kind);
        Assert.Equal(
            ["uf 1..1 TUf", "obs 0..1 xs:string", "item 1..n xs:string"],
            sequence.Children.Select(c => $"{c.Label} {c.Cardinality} {c.TypeName}"));
        Assert.Equal([new Facet("enumeration", "SP"), new Facet("enumeration", "RJ")], sequence.Children[0].Facets);
        Assert.Equal([new Facet("maxLength", "60")], sequence.Children[1].Facets);
    }

    [Fact]
    public void Recursive_nodes_are_flagged_and_still_expand_on_demand()
    {
        using var folder = new SchemaFolder(("pasta.xsd", SchemaFolder.Xsd("""
            <xs:complexType name="TPasta">
              <xs:sequence><xs:element name="pasta" type="TPasta" minOccurs="0" maxOccurs="unbounded"/></xs:sequence>
              <xs:attribute name="nome" type="xs:string"/>
            </xs:complexType>
            <xs:element name="raiz" type="TPasta"/>
            """)));

        var raiz = Open(folder, "raiz").Tree;
        var filha = raiz.Children[1].Children.Single();
        var neta = filha.Children[1].Children.Single();

        Assert.False(raiz.IsRecursive);
        Assert.True(filha.IsRecursive);
        Assert.True(neta.IsRecursive);
        Assert.Equal("raiz/sequence[1]/pasta/sequence[1]/pasta", neta.Path);
    }

    private static SchemaFolder Polimorfismo() => new(("desenho.xsd", SchemaFolder.Xsd("""
        <xs:element name="forma" type="xs:string" abstract="true"/>
        <xs:element name="circulo" type="xs:string" substitutionGroup="forma"/>
        <xs:element name="quadrado" type="xs:string" substitutionGroup="forma"/>
        <xs:complexType name="TPagamento" abstract="true"><xs:sequence><xs:element name="valor" type="xs:decimal"/></xs:sequence></xs:complexType>
        <xs:complexType name="TPix"><xs:complexContent><xs:extension base="TPagamento"><xs:sequence><xs:element name="chave" type="xs:string"/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>
        <xs:complexType name="TCartao"><xs:complexContent><xs:extension base="TPagamento"><xs:sequence><xs:element name="bandeira" type="xs:string"/></xs:sequence></xs:extension></xs:complexContent></xs:complexType>
        <xs:element name="desenho">
          <xs:complexType>
            <xs:sequence>
              <xs:element ref="forma" maxOccurs="3"/>
              <xs:element name="pagamento" type="TPagamento"/>
              <xs:any namespace="##other" processContents="lax" minOccurs="0"/>
            </xs:sequence>
            <xs:anyAttribute namespace="##any"/>
          </xs:complexType>
        </xs:element>
        """, "urn:desenho")));

    [Fact]
    public void Substitution_groups_and_abstract_types_appear_as_alternatives()
    {
        using var folder = Polimorfismo();

        var sequence = Open(folder, "desenho").Tree.Children.Single(c => c.Kind == NodeKind.Sequence);
        var forma = sequence.Children[0];
        var pagamento = sequence.Children[1];

        Assert.Equal((NodeKind.Choice, ChoiceOrigin.SubstitutionGroup, "1..3"), (forma.Kind, forma.Origin, forma.Cardinality));
        Assert.Equal(["circulo", "quadrado"], forma.Children.Select(c => c.Label));

        Assert.Equal((NodeKind.Choice, ChoiceOrigin.DerivedTypes), (pagamento.Kind, pagamento.Origin));
        Assert.Equal(["pagamento TPix", "pagamento TCartao"], pagamento.Children.Select(c => $"{c.Label} {c.XsiType}"));
        var pix = pagamento.Children[0].Children.Single(c => c.Kind == NodeKind.Sequence);
        Assert.Equal(["valor", "chave"], pix.Children.SelectMany(c => c.Kind == NodeKind.Sequence ? c.Children : [c]).Select(c => c.Label));
    }

    [Fact]
    public void Wildcards_show_which_namespaces_they_accept()
    {
        using var folder = Polimorfismo();

        var root = Open(folder, "desenho").Tree;
        var any = root.Children.Single(c => c.Kind == NodeKind.Sequence).Children[2];
        var anyAttribute = root.Children.Single(c => c.Kind == NodeKind.AnyAttribute);

        Assert.Equal(("any", "0..1", "##other"), (any.Label, any.Cardinality, any.WildcardNamespace));
        Assert.Equal(("@any", "##any"), (anyAttribute.Label, anyAttribute.WildcardNamespace));
    }
}
