namespace XsdVisualizer.Core.Tests;

/// <summary>Dois Schema Sets de pedidos: o "antes" e o "depois" de uma Nota Técnica fictícia.</summary>
public sealed class ComparisonFixture : IDisposable
{
    public SchemaFolder BeforeFolder { get; } = new(
        ("pedido_v1.00.xsd", SchemaFolder.Xsd("""
            <xs:element name="pedido">
              <xs:annotation><xs:documentation>Pedido de compra</xs:documentation></xs:annotation>
              <xs:complexType>
                <xs:sequence>
                  <xs:element name="numero" type="xs:int"/>
                  <xs:element name="xPed" minOccurs="0">
                    <xs:simpleType><xs:restriction base="xs:string"><xs:maxLength value="15"/></xs:restriction></xs:simpleType>
                  </xs:element>
                  <xs:element name="cStat">
                    <xs:simpleType><xs:restriction base="xs:string"><xs:enumeration value="100"/><xs:enumeration value="999"/></xs:restriction></xs:simpleType>
                  </xs:element>
                  <xs:element name="obs" type="xs:string" minOccurs="0">
                    <xs:annotation><xs:documentation>Observacao</xs:documentation></xs:annotation>
                  </xs:element>
                  <xs:element name="fax" type="xs:string" minOccurs="0"/>
                </xs:sequence>
                <xs:attribute name="versao" type="xs:string" fixed="1.00"/>
              </xs:complexType>
            </xs:element>
            <xs:element name="legado" type="xs:string"/>
            <xs:element name="igual" type="xs:string"/>
            """, "urn:loja")));

    public SchemaFolder AfterFolder { get; } = new(
        ("pedido_v1.00.xsd", SchemaFolder.Xsd("""
            <xs:element name="pedido">
              <xs:annotation><xs:documentation>Pedido de compra</xs:documentation></xs:annotation>
              <xs:complexType>
                <xs:sequence>
                  <xs:element name="numero" type="xs:int"/>
                  <xs:element name="xPed">
                    <xs:simpleType><xs:restriction base="xs:string"><xs:maxLength value="60"/></xs:restriction></xs:simpleType>
                  </xs:element>
                  <xs:element name="cStat">
                    <xs:simpleType><xs:restriction base="xs:string"><xs:enumeration value="100"/><xs:enumeration value="150"/><xs:enumeration value="151"/></xs:restriction></xs:simpleType>
                  </xs:element>
                  <xs:element name="obs" type="xs:string" minOccurs="0">
                    <xs:annotation><xs:documentation>Observação</xs:documentation></xs:annotation>
                  </xs:element>
                  <xs:element name="IBSCBS" type="xs:string" minOccurs="0"/>
                </xs:sequence>
                <xs:attribute name="versao" type="xs:string" fixed="1.00"/>
              </xs:complexType>
            </xs:element>
            <xs:element name="novo" type="xs:string"/>
            <xs:element name="igual" type="xs:string"/>
            """, "urn:loja")));

    public SchemaSet Before { get; }
    public SchemaSet After { get; }

    public ComparisonFixture()
    {
        Before = new SchemaSetLoader().Open(BeforeFolder.Path);
        After = new SchemaSetLoader().Open(AfterFolder.Path);
    }

    public void Dispose()
    {
        BeforeFolder.Dispose();
        AfterFolder.Dispose();
    }
}
