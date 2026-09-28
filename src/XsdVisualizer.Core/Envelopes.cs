using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace XsdVisualizer.Core;

/// <summary>Resultado da validação de um Envelope: Validation Issues e, se compactado, o Payload descompactado.</summary>
public sealed record EnvelopeValidation(IReadOnlyList<ValidationIssue> Issues, string? DecompressedPayload);

public sealed partial class Operation
{
    private readonly Dictionary<(string, GlobalElement?), XmlSchemaSet?> _combinedSchemas = new();

    /// <summary>
    /// Envelopes da Request ou Response: o Payload varia conforme <paramref name="kind"/> (gerado pelo Global Element
    /// do <paramref name="payload"/>); o envelope por fora, os headers e o Body seguem o WSDL e o <paramref name="endpoint"/>.
    /// </summary>
    public IReadOnlyList<Sample> GenerateEnvelopes(MessageDirection direction, SampleKind kind,
        Endpoint? endpoint = null, PayloadBinding? payload = null)
    {
        endpoint ??= DefaultEndpoint;
        var message = Message(direction);
        if (payload is null)
            return [Envelope(direction, kind, 0, endpoint, message, null, null, [], total: 1)];

        var payloads = kind switch
        {
            SampleKind.Minimal => [payload.Element.GenerateMinimal()],
            SampleKind.Maximal => [payload.Element.GenerateMaximal()],
            _ => payload.Element.GenerateCoverageSet(),
        };
        return payloads
            .Select(p => Envelope(direction, kind, p.Number, endpoint, message, payload, p.Xml, p.Covers, payloads.Count))
            .ToList();
    }

    private Sample Envelope(MessageDirection direction, SampleKind kind, int number, Endpoint endpoint, OperationMessage message,
        PayloadBinding? payload, string? payloadXml, IReadOnlyList<string> covers, int total)
    {
        var payloadRoot = payloadXml is null ? null : XDocument.Parse(payloadXml).Root!;
        string? readablePayload = null;

        var buffer = new MemoryStream();
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", Encoding = new UTF8Encoding(false), NewLineChars = "\n" };
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("soap", "Envelope", endpoint.EnvelopeNamespace);
            if (message.Headers.Count > 0)
            {
                writer.WriteStartElement("soap", "Header", endpoint.EnvelopeNamespace);
                foreach (var header in message.Headers) WriteGenerated(writer, header);
                writer.WriteEndElement();
            }
            writer.WriteStartElement("soap", "Body", endpoint.EnvelopeNamespace);
            if (payloadRoot is null)
                WriteGenerated(writer, message.Body);
            else if (message.BodyIsString)
            {
                var compact = payloadRoot.ToString(SaveOptions.DisableFormatting);
                writer.WriteStartElement(message.BodyElementName, message.BodyElementNamespace);
                if (payload!.Compressed)
                {
                    writer.WriteString(Compress(compact));
                    readablePayload = payloadRoot.ToString();
                }
                else writer.WriteString(compact);
                writer.WriteEndElement();
            }
            else
            {
                writer.WriteStartElement(message.BodyElementName, message.BodyElementNamespace);
                using (var reader = payloadRoot.CreateReader()) writer.WriteNode(reader, true);
                writer.WriteEndElement();
            }
            writer.WriteEndElement(); // Body
            writer.WriteEndElement(); // Envelope
            writer.WriteEndDocument();
        }

        var xml = Encoding.UTF8.GetString(buffer.ToArray());
        if (kind == SampleKind.Coverage)
            xml = SampleComments.Insert(xml, SampleComments.Coverage($"Coverage Set de {Name} ({Direction(direction)}): Envelope {number} de {total}", covers));

        IReadOnlyList<ValidationIssue> Validate(string text)
        {
            var issues = ValidateEnvelope(text, direction, payload).Issues.ToList();
            if (payload is null)
                issues.Insert(0, new ValidationIssue(
                    $"Defina o Payload Binding da {Direction(direction)} de {Name}: o WSDL não diz qual XML vai no Body.", null, 0, 0));
            return issues;
        }
        return new Sample(payload?.Element, kind, number, xml, covers, Validate(xml), Validate, this, direction, readablePayload);
    }

    /// <summary>
    /// Valida um Envelope desta Operation em camadas, numa passada: o envelope contra o schema do SOAP, o Body contra
    /// o schema do WSDL e o Payload contra o Global Element do <paramref name="payload"/>. Payload compactado (ou em
    /// string) é validado à parte; suas Validation Issues se referem ao Payload descompactado (InPayload).
    /// </summary>
    public EnvelopeValidation ValidateEnvelope(string xml, MessageDirection direction, PayloadBinding? payload)
    {
        var message = Message(direction);
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.SetLineInfo);
        }
        catch (XmlException e)
        {
            return new([new ValidationIssue(e.Message, null, e.LineNumber, e.LinePosition)], null);
        }

        var root = document.Root!;
        var envelopeNs = root.Name.NamespaceName;
        if (root.Name.LocalName != "Envelope" || !Soap.IsEnvelopeNamespace(envelopeNs))
            return new([Issue("A raiz deveria ser soap:Envelope (SOAP 1.1 ou 1.2).", root)], null);

        var issues = new List<ValidationIssue>();
        var schemas = CombinedSchemas(envelopeNs, message.BodyIsString ? null : payload?.Element);
        if (schemas is null)
            issues.Add(Issue("Não foi possível combinar os schemas do SOAP, do WSDL e do Payload.", root));
        else
            issues.AddRange(ValidateWith(xml, schemas));

        var body = root.Element(XName.Get("Body", envelopeNs));
        var wrapper = body?.Elements().FirstOrDefault();
        if (wrapper is null || wrapper.Name != XName.Get(message.BodyElementName, message.BodyElementNamespace))
        {
            issues.Add(Issue($"O Body deveria conter {message.BodyElementName} ({Direction(direction)} de {Name}).", (XObject?)wrapper ?? body ?? root));
            return new(issues, null);
        }

        string? decompressed = null;
        if (message.BodyIsString && payload is not null)
        {
            var text = wrapper.Value;
            if (payload.Compressed)
            {
                try { text = Decompress(text); }
                catch (Exception e) when (e is FormatException or InvalidDataException)
                {
                    issues.Add(Issue($"O conteúdo de {message.BodyElementName} não é gzip+base64 válido: {e.Message}", wrapper));
                    return new(issues, null);
                }
            }
            // Formatado, para as posições baterem com o Payload exibido.
            try { text = XDocument.Parse(text).ToString(); } catch (XmlException) { }
            decompressed = payload.Compressed ? text : null;
            issues.AddRange(payload.Element.Validate(text).Select(i => i with { InPayload = true }));
        }
        return new(issues, decompressed);
    }

    /// <summary>SOAP + WSDL + (Payload) num só conjunto, para validar o Envelope numa passada com posições reais.</summary>
    private XmlSchemaSet? CombinedSchemas(string envelopeNs, GlobalElement? payload)
    {
        if (_combinedSchemas.TryGetValue((envelopeNs, payload), out var cached)) return cached;
        XmlSchemaSet? set = new() { XmlResolver = SchemaResolver.EmbeddedOnly() };
        try
        {
            var location = envelopeNs == Soap.Envelope12 ? "http://www.w3.org/2003/05/soap-envelope" : Soap.Envelope11;
            set.Add(envelopeNs, location);
            set.Add(WsdlSchemas);
            if (payload is not null) set.Add(payload.Unit.Schemas);
            set.Compile();
        }
        catch (Exception e) when (e is XmlSchemaException or ArgumentException or XmlException)
        {
            set = null;
        }
        return _combinedSchemas[(envelopeNs, payload)] = set;
    }

    private static List<ValidationIssue> ValidateWith(string xml, XmlSchemaSet schemas)
    {
        var issues = new List<ValidationIssue>();
        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemas,
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
                | XmlSchemaValidationFlags.ProcessIdentityConstraints
                | XmlSchemaValidationFlags.AllowXmlAttributes,
        };
        settings.ValidationEventHandler += (_, e) => issues.Add(new ValidationIssue(e.Message, null, e.Exception.LineNumber,
            e.Exception.LinePosition, e.Severity == XmlSeverityType.Warning ? IssueSeverity.Warning : IssueSeverity.Error));
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read()) { }
        }
        catch (XmlException e)
        {
            issues.Add(new ValidationIssue(e.Message, null, e.LineNumber, e.LinePosition));
        }
        return issues;
    }

    /// <summary>Escreve um elemento do WSDL (header, ou Body sem Payload Binding) gerado como Maximal.</summary>
    private void WriteGenerated(XmlWriter writer, XmlSchemaElement element)
    {
        var tree = new SchemaTreeBuilder(WsdlSchemas).Build(element);
        var xml = new SampleGenerator(GenerationMode.Maximal, new PinnedChoices(null)).Generate(tree);
        using var reader = XDocument.Parse(xml).Root!.CreateReader();
        writer.WriteNode(reader, true);
    }

    internal static string Compress(string text)
    {
        var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(buffer.ToArray());
    }

    internal static string Decompress(string base64)
    {
        using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(base64.Trim())), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Direction(MessageDirection direction) => direction == MessageDirection.Request ? "Request" : "Response";

    private static ValidationIssue Issue(string message, XObject at) =>
        at is IXmlLineInfo line && line.HasLineInfo()
            ? new ValidationIssue(message, null, line.LineNumber, line.LinePosition)
            : new ValidationIssue(message, null, 0, 0);
}
