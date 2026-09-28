using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace XsdVisualizer.Core;

/// <summary>Baixa schemas remotos; devolve null quando não consegue.</summary>
public interface ISchemaDownloader
{
    byte[]? Download(Uri uri);
}

public sealed class HttpSchemaDownloader : ISchemaDownloader
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15) };

    public byte[]? Download(Uri uri)
    {
        try
        {
            return Client.GetByteArrayAsync(uri).GetAwaiter().GetResult();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}

/// <summary>
/// Resolve includes/imports: arquivos locais normalmente; remotos na ordem
/// schemas W3C embutidos → cache local → download (gravando no cache).
/// </summary>
internal sealed class SchemaResolver(ISchemaDownloader downloader, string cacheDirectory, Action<Uri> onUnresolved) : XmlUrlResolver
{
    private static readonly Dictionary<string, string> Embedded = new(StringComparer.OrdinalIgnoreCase)
    {
        ["www.w3.org/TR/xmldsig-core/xmldsig-core-schema.xsd"] = "W3C.xmldsig-core-schema.xsd",
        ["www.w3.org/TR/2002/REC-xmldsig-core-20020212/xmldsig-core-schema.xsd"] = "W3C.xmldsig-core-schema.xsd",
        ["www.w3.org/TR/xmldsig-core1/xmldsig-core-schema.xsd"] = "W3C.xmldsig-core-schema.xsd",
        ["www.w3.org/2001/xml.xsd"] = "W3C.xml.xsd",
        ["www.w3.org/2009/01/xml.xsd"] = "W3C.xml.xsd",
    };

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        if (absoluteUri.IsFile) return base.GetEntity(absoluteUri, role, ofObjectToReturn);

        var key = absoluteUri.Host + absoluteUri.AbsolutePath;
        if (Embedded.TryGetValue(key, out var resource))
            return typeof(SchemaResolver).Assembly.GetManifestResourceStream(resource);

        var cached = Path.Combine(cacheDirectory, Hash(absoluteUri) + ".xsd");
        if (File.Exists(cached)) return File.OpenRead(cached);

        if (downloader.Download(absoluteUri) is { } content)
        {
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllBytes(cached, content);
            return new MemoryStream(content);
        }

        onUnresolved(absoluteUri);
        throw new WebException($"Não foi possível obter o schema remoto {absoluteUri} (sem conexão e sem cópia em cache).");
    }

    private static string Hash(Uri uri) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(uri.ToString())));
}
