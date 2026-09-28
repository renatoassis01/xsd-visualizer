using System.Xml;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

public enum SoapVersion { Soap11, Soap12 }

public enum MessageDirection { Request, Response }

/// <summary>Um wsdl:service de um WSDL do Schema Set.</summary>
public sealed class Service
{
    internal Service(string name, string sourceFile, IReadOnlyList<Endpoint> endpoints, IReadOnlyList<Operation> operations)
    {
        Name = name;
        SourceFile = sourceFile;
        Endpoints = endpoints;
        Operations = operations;
        foreach (var operation in operations) operation.Service = this;
    }

    public string Name { get; }
    public string SourceFile { get; }
    public SchemaSet SchemaSet { get; internal set; } = null!;
    public IReadOnlyList<Endpoint> Endpoints { get; }
    public IReadOnlyList<Operation> Operations { get; }
}

/// <summary>Um wsdl:port: endereço, versão do SOAP e soapAction de cada Operation.</summary>
public sealed class Endpoint
{
    private readonly IReadOnlyDictionary<string, string> _soapActions;

    internal Endpoint(string name, string? address, SoapVersion soapVersion, IReadOnlyDictionary<string, string> soapActions)
    {
        Name = name;
        Address = address;
        SoapVersion = soapVersion;
        _soapActions = soapActions;
    }

    public string Name { get; }
    public string? Address { get; }
    public SoapVersion SoapVersion { get; }

    public bool Supports(Operation operation) => _soapActions.ContainsKey(operation.Name);

    public string? SoapActionOf(Operation operation) =>
        _soapActions.TryGetValue(operation.Name, out var action) && action.Length > 0 ? action : null;

    public string EnvelopeNamespace => SoapVersion == SoapVersion.Soap12 ? Soap.Envelope12 : Soap.Envelope11;

    public override string ToString() => $"{Name} ({(SoapVersion == SoapVersion.Soap12 ? "SOAP 1.2" : "SOAP 1.1")})";
}

/// <summary>Uma operação de um Service, com Request e Response.</summary>
public sealed partial class Operation
{
    internal Operation(string name, OperationMessage request, OperationMessage response, XmlSchemaSet wsdlSchemas)
    {
        Name = name;
        Request = request;
        Response = response;
        WsdlSchemas = wsdlSchemas;
    }

    public string Name { get; }
    public Service Service { get; internal set; } = null!;
    public OperationMessage Request { get; }
    public OperationMessage Response { get; }
    internal XmlSchemaSet WsdlSchemas { get; }

    public OperationMessage Message(MessageDirection direction) => direction == MessageDirection.Request ? Request : Response;

    /// <summary>Endpoints que oferecem esta Operation, SOAP 1.2 primeiro.</summary>
    public IReadOnlyList<Endpoint> Endpoints =>
        Service.Endpoints.Where(e => e.Supports(this)).OrderByDescending(e => e.SoapVersion).ToList();

    /// <summary>SOAP 1.2 quando existir.</summary>
    public Endpoint DefaultEndpoint => Endpoints.First();

    public override string ToString() => $"{Service?.Name}/{Name}";
}

/// <summary>A Request ou a Response de uma Operation: elemento do Body e headers declarados.</summary>
public sealed class OperationMessage
{
    internal OperationMessage(MessageDirection direction, XmlSchemaElement body, IReadOnlyList<XmlSchemaElement> headers)
    {
        Direction = direction;
        Body = body;
        Headers = headers;
    }

    public MessageDirection Direction { get; }
    internal XmlSchemaElement Body { get; }
    internal IReadOnlyList<XmlSchemaElement> Headers { get; }

    public string BodyElementName => Body.QualifiedName.Name;
    public string BodyElementNamespace => Body.QualifiedName.Namespace;
    public IReadOnlyList<string> HeaderNames => Headers.Select(h => h.QualifiedName.Name).ToList();

    /// <summary>O Body é uma string (Payload como texto, em geral gzip+base64) em vez de conteúdo XML livre.</summary>
    public bool BodyIsString => Body.ElementSchemaType is XmlSchemaSimpleType;
}

/// <summary>Ligação da Request/Response de uma Operation ao Global Element do seu Payload.</summary>
public sealed record PayloadBinding(GlobalElement Element, bool Compressed);

internal static class Soap
{
    public const string Envelope11 = "http://schemas.xmlsoap.org/soap/envelope/";
    public const string Envelope12 = "http://www.w3.org/2003/05/soap-envelope";
    public const string Wsdl11 = "http://schemas.xmlsoap.org/wsdl/";
    public const string Wsdl20 = "http://www.w3.org/ns/wsdl";
    public const string WsdlSoap11 = "http://schemas.xmlsoap.org/wsdl/soap/";
    public const string WsdlSoap12 = "http://schemas.xmlsoap.org/wsdl/soap12/";

    public static bool IsEnvelopeNamespace(string ns) => ns is Envelope11 or Envelope12;
}
