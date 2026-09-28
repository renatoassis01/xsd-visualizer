using System.Xml.Linq;

namespace XsdVisualizer.Core.Tests;

public class SampleGenerationTests
{
    private static readonly XNamespace Loja = "urn:loja";

    private static GlobalElement Open(SchemaFolder folder, string name) =>
        new SchemaSetLoader().Open(folder.Path).GlobalElements.Single(e => e.Name == name);

    private static SchemaFolder Pedido() => new(("pedido.xsd", SchemaFolder.Xsd("""
        <xs:element name="pedido">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="numero" type="xs:int"/>
              <xs:element name="obs" type="xs:string" minOccurs="0"/>
              <xs:element name="item" type="xs:string" maxOccurs="unbounded"/>
              <xs:element name="tag" type="xs:string" minOccurs="0" maxOccurs="5"/>
            </xs:sequence>
            <xs:attribute name="versao" type="xs:string" use="required" fixed="1.00"/>
            <xs:attribute name="canal" type="xs:string"/>
          </xs:complexType>
        </xs:element>
        """, "urn:loja")));

    [Fact]
    public void The_minimal_sample_has_only_what_the_schema_requires()
    {
        using var folder = Pedido();

        var sample = Open(folder, "pedido").GenerateMinimal();

        Assert.True(sample.IsValid, string.Join("\n", sample.Issues));
        var root = XDocument.Parse(sample.Xml).Root!;
        Assert.Equal(Loja + "pedido", root.Name);
        Assert.Equal(["numero", "item"], root.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("1.00", (string?)root.Attribute("versao"));
        Assert.Null(root.Attribute("canal"));
    }

    [Fact]
    public void The_maximal_sample_has_every_optional_and_repeats_up_to_two_times()
    {
        using var folder = Pedido();

        var sample = Open(folder, "pedido").GenerateMaximal();

        Assert.True(sample.IsValid, string.Join("\n", sample.Issues));
        var root = XDocument.Parse(sample.Xml).Root!;
        Assert.Equal(["numero", "obs", "item", "item", "tag", "tag"], root.Elements().Select(e => e.Name.LocalName));
        Assert.NotNull(root.Attribute("canal"));
    }

    private static string Restricted(string name, string restriction) =>
        $"""<xs:element name="{name}"><xs:simpleType><xs:restriction {restriction}</xs:restriction></xs:simpleType></xs:element>""";

    [Fact]
    public void Generated_values_satisfy_facets_and_built_in_types()
    {
        using var folder = new SchemaFolder(("valores.xsd", SchemaFolder.Xsd($"""
            <xs:element name="valores">
              <xs:complexType>
                <xs:sequence>
                  {Restricted("uf", """base="xs:string"><xs:enumeration value="SP"/><xs:enumeration value="RJ"/>""")}
                  {Restricted("cep", """base="xs:string"><xs:pattern value="[0-9]{8}"/>""")}
                  {Restricted("nome", """base="xs:string"><xs:minLength value="10"/><xs:maxLength value="20"/>""")}
                  {Restricted("sigla", """base="xs:string"><xs:maxLength value="2"/>""")}
                  {Restricted("chave", """base="xs:string"><xs:length value="44"/><xs:pattern value="[0-9]{44}"/>""")}
                  {Restricted("valor", """base="xs:decimal"><xs:totalDigits value="5"/><xs:fractionDigits value="2"/><xs:minInclusive value="100"/>""")}
                  {Restricted("saldo", """base="xs:int"><xs:maxExclusive value="-10"/>""")}
                  {Restricted("vProd", """base="xs:string"><xs:whiteSpace value="preserve"/><xs:pattern value="0|0\.[0-9]{2}|[1-9]{1}[0-9]{0,12}(\.[0-9]{2})?"/>""")}
                  {Restricted("xNome", """base="xs:string"><xs:whiteSpace value="preserve"/><xs:pattern value="[!-ÿ]{1}[ -ÿ]{0,}[!-ÿ]{1}|[!-ÿ]{1}"/><xs:minLength value="2"/><xs:maxLength value="60"/>""")}
                  {Restricted("email", """base="xs:string"><xs:pattern value="[^@]+@[^\.]+\..+"/>""")}
                  {Restricted("dhEmi", """base="xs:string"><xs:pattern value="(((20(([02468][048])|([13579][26]))-02-29))|(20[0-9][0-9])-((((0[1-9])|(1[0-2]))-((0[1-9])|(1\d)|(2[0-8])))|((((0[13578])|(1[02]))-31)|(((0[1,3-9])|(1[0-2]))-(29|30)))))T(20|21|22|23|[0-1]\d):[0-5]\d:[0-5]\d([\-,\+](0[0-9]|10|11):00|([\+](12):00))"/>""")}
                  {Restricted("letra", """base="xs:string"><xs:pattern value="[\i-[:]][\c-[:]]*"/>""")}
                  {Restricted("arquivo", """base="xs:base64Binary"><xs:length value="3"/>""")}
                  {Restricted("hash", """base="xs:hexBinary"><xs:length value="4"/>""")}
                  <xs:element name="data" type="xs:date"/>
                  <xs:element name="dataHora" type="xs:dateTime"/>
                  <xs:element name="hora" type="xs:time"/>
                  <xs:element name="ano" type="xs:gYear"/>
                  <xs:element name="flag" type="xs:boolean"/>
                  <xs:element name="site" type="xs:anyURI"/>
                  <xs:element name="idioma" type="xs:language"/>
                  <xs:element name="prazo" type="xs:duration"/>
                  <xs:element name="positivo" type="xs:positiveInteger"/>
                  <xs:element name="negativo" type="xs:negativeInteger"/>
                  <xs:element name="taxa" type="xs:double"/>
                  <xs:element name="codigo" type="xs:NCName"/>
                  <xs:element name="lista"><xs:simpleType><xs:list itemType="xs:int"/></xs:simpleType></xs:element>
                  <xs:element name="uniao"><xs:simpleType><xs:union memberTypes="xs:date xs:int"/></xs:simpleType></xs:element>
                </xs:sequence>
              </xs:complexType>
            </xs:element>
            """)));

        var sample = Open(folder, "valores").GenerateMaximal();

        Assert.True(sample.IsValid, string.Join("\n", sample.Issues) + "\n" + sample.Xml);
        var root = XDocument.Parse(sample.Xml).Root!;
        Assert.Equal("SP", root.Element("uf")!.Value);
    }

    [Fact]
    public void Generation_is_deterministic()
    {
        using var folder = Pedido();
        var pedido = Open(folder, "pedido");

        Assert.Equal(pedido.GenerateMaximal().Xml, pedido.GenerateMaximal().Xml);
        Assert.Equal(pedido.GenerateMaximal().Xml, Open(folder, "pedido").GenerateMaximal().Xml);
    }
}
