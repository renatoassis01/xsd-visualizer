using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

/// <summary>
/// Lê um WSDL 1.1 document/literal (ADR-0004): compila os schemas de wsdl:types e monta Services,
/// Endpoints e Operations. O que estiver fora desse recorte vira Validation Issue.
/// </summary>
internal sealed class WsdlReader(string file, XmlResolver resolver, List<ValidationIssue> issues)
{
    private static readonly XNamespace W = Soap.Wsdl11;
    private static readonly XNamespace Xs = XmlSchema.Namespace;

    public IReadOnlyList<Service> Read()
    {
        XDocument document;
        try
        {
            document = XDocument.Load(file, LoadOptions.SetLineInfo);
        }
        catch (XmlException e)
        {
            Issue(e.Message, e.LineNumber, e.LinePosition);
            return [];
        }

        var root = document.Root!;
        if (root.Name.Namespace == Soap.Wsdl20)
        {
            Issue("WSDL 2.0 não suportado (apenas WSDL 1.1).", root);
            return [];
        }
        if (root.Name != W + "definitions")
        {
            Issue("Não é um WSDL 1.1: a raiz deveria ser wsdl:definitions.", root);
            return [];
        }

        var schemas = CompileTypes(root);
        var messages = root.Elements(W + "message").ToDictionary(m => Name(root, (string?)m.Attribute("name")), m => m);
        var portTypes = root.Elements(W + "portType").ToDictionary(p => Name(root, (string?)p.Attribute("name")), p => p);
        var bindings = root.Elements(W + "binding").ToDictionary(b => Name(root, (string?)b.Attribute("name")), b => b);

        var services = new List<Service>();
        foreach (var service in root.Elements(W + "service"))
        {
            var endpoints = new List<Endpoint>();
            var operations = new Dictionary<string, Operation>(StringComparer.Ordinal);
            foreach (var port in service.Elements(W + "port"))
            {
                if (!bindings.TryGetValue(Resolve(port, (string?)port.Attribute("binding")), out var binding))
                {
                    Issue($"Endpoint {(string?)port.Attribute("name")}: binding {(string?)port.Attribute("binding")} não encontrado.", port);
                    continue;
                }
                var soap = binding.Elements().FirstOrDefault(e => e.Name.LocalName == "binding" && e.Name.Namespace.NamespaceName is Soap.WsdlSoap11 or Soap.WsdlSoap12);
                if (soap is null)
                {
                    Issue($"Binding {(string?)binding.Attribute("name")} não é SOAP (HTTP/MIME não suportados): Endpoint {(string?)port.Attribute("name")} ignorado.",
                        binding, IssueSeverity.Warning);
                    continue;
                }
                var version = soap.Name.Namespace == Soap.WsdlSoap12 ? SoapVersion.Soap12 : SoapVersion.Soap11;
                var bindingStyle = (string?)soap.Attribute("style") ?? "document";

                var actions = new Dictionary<string, string>(StringComparer.Ordinal);
                portTypes.TryGetValue(Resolve(binding, (string?)binding.Attribute("type")), out var portType);
                foreach (var op in binding.Elements(W + "operation"))
                {
                    var name = (string?)op.Attribute("name") ?? "";
                    var soapOp = op.Elements().FirstOrDefault(e => e.Name.LocalName == "operation" && e.Name.Namespace == soap.Name.Namespace);
                    var style = (string?)soapOp?.Attribute("style") ?? bindingStyle;
                    if (style == "rpc")
                    {
                        Issue($"Operation {name}: estilo rpc não suportado (apenas document/literal).", op);
                        continue;
                    }
                    if (op.Descendants().Any(e => e.Name.LocalName is "body" or "header" && (string?)e.Attribute("use") == "encoded"))
                    {
                        Issue($"Operation {name}: use=\"encoded\" não suportado (apenas document/literal).", op);
                        continue;
                    }
                    actions[name] = (string?)soapOp?.Attribute("soapAction") ?? "";
                    if (!operations.ContainsKey(name) && BuildOperation(root, op, portType, messages, schemas) is { } built)
                        operations[name] = built;
                }
                if (actions.Count > 0)
                    endpoints.Add(new Endpoint((string?)port.Attribute("name") ?? "", Address(port), version, actions));
            }
            if (operations.Count == 0)
            {
                Issue($"Service {(string?)service.Attribute("name")}: nenhuma Operation SOAP document/literal utilizável.", service, IssueSeverity.Warning);
                continue;
            }
            services.Add(new Service((string?)service.Attribute("name") ?? Path.GetFileNameWithoutExtension(file), file,
                endpoints, operations.Values.Where(o => endpoints.Any(e => e.Supports(o))).ToList()));
        }
        return services;
    }

    private Operation? BuildOperation(XElement root, XElement bindingOperation, XElement? portType,
        Dictionary<XName, XElement> messages, XmlSchemaSet schemas)
    {
        var name = (string?)bindingOperation.Attribute("name") ?? "";
        var abstractOp = portType?.Elements(W + "operation").FirstOrDefault(o => (string?)o.Attribute("name") == name);
        if (abstractOp is null)
        {
            Issue($"Operation {name}: não encontrada no portType.", bindingOperation);
            return null;
        }
        var request = Message(root, abstractOp.Element(W + "input"), bindingOperation.Element(W + "input"), MessageDirection.Request, messages, schemas, name);
        var response = Message(root, abstractOp.Element(W + "output"), bindingOperation.Element(W + "output"), MessageDirection.Response, messages, schemas, name);
        return request is null || response is null ? null : new Operation(name, request, response, schemas);
    }

    private OperationMessage? Message(XElement root, XElement? abstractMessage, XElement? bindingMessage, MessageDirection direction,
        Dictionary<XName, XElement> messages, XmlSchemaSet schemas, string operation)
    {
        if (abstractMessage is null || !messages.TryGetValue(Resolve(abstractMessage, (string?)abstractMessage.Attribute("message")), out var message))
        {
            Issue($"Operation {operation}: mensagem de {direction.Label()} não encontrada.", abstractMessage ?? root);
            return null;
        }
        var parts = message.Elements(W + "part").ToList();
        if (parts.Count != 1 || parts[0].Attribute("element") is null)
        {
            Issue($"Operation {operation}: a mensagem {(string?)message.Attribute("name")} deve ter uma única part com element= (document/literal).", message);
            return null;
        }
        var body = Element(parts[0], (string?)parts[0].Attribute("element"), schemas);
        if (body is null) return null;

        var headers = new List<XmlSchemaElement>();
        foreach (var header in bindingMessage?.Elements().Where(e => e.Name.LocalName == "header") ?? [])
        {
            if (!messages.TryGetValue(Resolve(header, (string?)header.Attribute("message")), out var headerMessage))
            {
                Issue($"Operation {operation}: mensagem de header {(string?)header.Attribute("message")} não encontrada.", header);
                continue;
            }
            var part = headerMessage.Elements(W + "part").FirstOrDefault(p => (string?)p.Attribute("name") == (string?)header.Attribute("part"));
            if (part?.Attribute("element") is null)
            {
                Issue($"Operation {operation}: part {(string?)header.Attribute("part")} do header não encontrada ou sem element=.", header);
                continue;
            }
            if (Element(part, (string?)part.Attribute("element"), schemas) is { } element)
                headers.Add(element);
        }
        return new OperationMessage(direction, body, headers);
    }

    private XmlSchemaElement? Element(XElement context, string? qname, XmlSchemaSet schemas)
    {
        var name = Resolve(context, qname);
        if (schemas.GlobalElements[new XmlQualifiedName(name.LocalName, name.NamespaceName)] is XmlSchemaElement element) return element;
        Issue($"Elemento {qname} não declarado nos schemas do WSDL.", context);
        return null;
    }

    /// <summary>Compila os xs:schema de wsdl:types, que herdam prefixos declarados no wsdl:definitions.</summary>
    private XmlSchemaSet CompileTypes(XElement root)
    {
        var set = new XmlSchemaSet { XmlResolver = resolver };
        set.ValidationEventHandler += (_, e) => issues.Add(SchemaValidation.Issue(e.Exception, file, e.Severity));
        foreach (var schema in root.Element(W + "types")?.Elements(Xs + "schema") ?? [])
        {
            var copy = new XElement(schema);
            foreach (var ns in schema.AncestorsAndSelf().SelectMany(a => a.Attributes()).Where(a => a.IsNamespaceDeclaration))
                if (copy.Attribute(ns.Name) is null) copy.SetAttributeValue(ns.Name, ns.Value);
            try
            {
                using var reader = XmlReader.Create(new StringReader(copy.ToString()), new XmlReaderSettings(), new Uri(Path.GetFullPath(file)).AbsoluteUri);
                set.Add(XmlSchema.Read(reader, null)!);
            }
            catch (Exception e) when (e is XmlException or XmlSchemaException)
            {
                Issue(e.Message, schema);
            }
        }
        try { set.Compile(); }
        catch (XmlSchemaException e) { Issue(e.Message, e.LineNumber, e.LinePosition); }
        return set;
    }

    private static string? Address(XElement port) =>
        (string?)port.Elements().FirstOrDefault(e => e.Name.LocalName == "address")?.Attribute("location");

    private static XName Name(XElement root, string? localName) => XName.Get(localName ?? "", (string?)root.Attribute("targetNamespace") ?? "");

    private static XName Resolve(XElement context, string? qname)
    {
        if (string.IsNullOrEmpty(qname)) return XName.Get("_");
        var colon = qname.IndexOf(':');
        var ns = colon < 0 ? context.GetDefaultNamespace() : context.GetNamespaceOfPrefix(qname[..colon]) ?? XNamespace.None;
        return ns + qname[(colon + 1)..];
    }

    private void Issue(string message, XElement at, IssueSeverity severity = IssueSeverity.Error)
    {
        var line = (IXmlLineInfo)at;
        issues.Add(new ValidationIssue(message, file, line.LineNumber, line.LinePosition, severity));
    }

    private void Issue(string message, int line, int column) => issues.Add(new ValidationIssue(message, file, line, column));
}
