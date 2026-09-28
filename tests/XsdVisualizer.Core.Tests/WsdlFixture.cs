namespace XsdVisualizer.Core.Tests;

/// <summary>Pasta com um Schema Set de pedidos (XSD) e um WSDL de serviço que o transporta.</summary>
public static class WsdlFixture
{
    public const string Tns = "urn:pedidos:wsdl";

    public static string PedidoXsd => SchemaFolder.Xsd("""
        <xs:element name="pedido">
          <xs:complexType>
            <xs:sequence>
              <xs:element name="numero" type="xs:int"/>
              <xs:choice><xs:element name="CPF" type="xs:string"/><xs:element name="CNPJ" type="xs:string"/></xs:choice>
              <xs:element name="uf"><xs:simpleType><xs:restriction base="xs:string"><xs:enumeration value="SP"/><xs:enumeration value="RJ"/></xs:restriction></xs:simpleType></xs:element>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        <xs:element name="retPedido">
          <xs:complexType><xs:sequence><xs:element name="cStat" type="xs:int"/></xs:sequence></xs:complexType>
        </xs:element>
        """, "urn:loja");

    /// <param name="bindings">"11", "12" ou "11,12": quais bindings SOAP declarar.</param>
    public static string Wsdl(string bindings = "11,12", string style = "document", string use = "literal") => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <wsdl:definitions xmlns:s="http://www.w3.org/2001/XMLSchema" xmlns:tns="{{Tns}}"
            xmlns:soap="http://schemas.xmlsoap.org/wsdl/soap/" xmlns:soap12="http://schemas.xmlsoap.org/wsdl/soap12/"
            xmlns:wsdl="http://schemas.xmlsoap.org/wsdl/" targetNamespace="{{Tns}}">
          <wsdl:types>
            <s:schema elementFormDefault="qualified" targetNamespace="{{Tns}}">
              <s:element name="dadosMsg"><s:complexType mixed="true"><s:sequence><s:any/></s:sequence></s:complexType></s:element>
              <s:element name="resultMsg"><s:complexType mixed="true"><s:sequence><s:any/></s:sequence></s:complexType></s:element>
              <s:element name="dadosMsgZip" type="s:string"/>
              <s:element name="monitoria"><s:complexType><s:sequence><s:element name="servidor" type="s:string"/></s:sequence></s:complexType></s:element>
            </s:schema>
          </wsdl:types>
          <wsdl:message name="enviarIn"><wsdl:part name="dadosMsg" element="tns:dadosMsg"/></wsdl:message>
          <wsdl:message name="enviarOut"><wsdl:part name="resultMsg" element="tns:resultMsg"/></wsdl:message>
          <wsdl:message name="enviarZipIn"><wsdl:part name="dadosMsgZip" element="tns:dadosMsgZip"/></wsdl:message>
          <wsdl:message name="monitoriaHeader"><wsdl:part name="monitoria" element="tns:monitoria"/></wsdl:message>
          <wsdl:portType name="PedidosSoap">
            <wsdl:operation name="enviar"><wsdl:input message="tns:enviarIn"/><wsdl:output message="tns:enviarOut"/></wsdl:operation>
            <wsdl:operation name="enviarZip"><wsdl:input message="tns:enviarZipIn"/><wsdl:output message="tns:enviarOut"/></wsdl:operation>
          </wsdl:portType>
          {{(bindings.Contains("11") ? Binding("soap", "PedidosSoap11", style, use) : "")}}
          {{(bindings.Contains("12") ? Binding("soap12", "PedidosSoap12", style, use) : "")}}
          <wsdl:service name="Pedidos">
            {{(bindings.Contains("11") ? """<wsdl:port name="PedidosSoap11" binding="tns:PedidosSoap11"><soap:address location="https://exemplo.com/pedidos.asmx"/></wsdl:port>""" : "")}}
            {{(bindings.Contains("12") ? """<wsdl:port name="PedidosSoap12" binding="tns:PedidosSoap12"><soap12:address location="https://exemplo.com/pedidos.asmx"/></wsdl:port>""" : "")}}
          </wsdl:service>
        </wsdl:definitions>
        """;

    private static string Binding(string prefix, string name, string style, string use) => $$"""
        <wsdl:binding name="{{name}}" type="tns:PedidosSoap">
          <{{prefix}}:binding transport="http://schemas.xmlsoap.org/soap/http" style="{{style}}"/>
          <wsdl:operation name="enviar">
            <{{prefix}}:operation soapAction="{{Tns}}/enviar" style="{{style}}"/>
            <wsdl:input><{{prefix}}:body use="{{use}}"/></wsdl:input>
            <wsdl:output><{{prefix}}:body use="{{use}}"/><{{prefix}}:header message="tns:monitoriaHeader" part="monitoria" use="{{use}}"/></wsdl:output>
          </wsdl:operation>
          <wsdl:operation name="enviarZip">
            <{{prefix}}:operation soapAction="{{Tns}}/enviarZip" style="{{style}}"/>
            <wsdl:input><{{prefix}}:body use="{{use}}"/></wsdl:input>
            <wsdl:output><{{prefix}}:body use="{{use}}"/></wsdl:output>
          </wsdl:operation>
        </wsdl:binding>
        """;

    public static SchemaFolder Folder(string bindings = "11,12", string style = "document", string use = "literal") =>
        new(("pedido.xsd", PedidoXsd), ("Pedidos.wsdl", Wsdl(bindings, style, use)));
}
