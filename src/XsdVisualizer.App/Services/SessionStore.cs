using System.Text.Json;

namespace XsdVisualizer.App.Services;

public enum ThemeChoice { System, Light, Dark }

/// <summary>"System" segue o idioma do sistema operacional; os demais são culturas (pt-BR, en).</summary>
public static class LanguageChoice
{
    public const string System = "system";
    public const string Portuguese = "pt-BR";
    public const string English = "en";
}

/// <summary>Schema Sets abertos, recentes e preferências, persistidos por usuário.</summary>
public sealed class Session
{
    public List<string> OpenSchemaSets { get; set; } = [];
    public List<string> Recent { get; set; } = [];
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
    public string Language { get; set; } = LanguageChoice.System;
    /// <summary>Payload Bindings por "&lt;wsdl&gt;|&lt;service&gt;|&lt;operation&gt;|&lt;Request/Response&gt;".</summary>
    public Dictionary<string, SavedPayloadBinding> PayloadBindings { get; set; } = new();
    /// <summary>Endpoint escolhido por "&lt;wsdl&gt;|&lt;service&gt;|&lt;operation&gt;".</summary>
    public Dictionary<string, string> Endpoints { get; set; } = new();
}

/// <summary>Global Element de um Payload Binding, identificado pelo arquivo que o declara.</summary>
public sealed class SavedPayloadBinding
{
    public string ElementName { get; set; } = "";
    public string ElementNamespace { get; set; } = "";
    public string ElementSourceFile { get; set; } = "";
    public bool Compressed { get; set; }
}

public sealed class SessionStore
{
    private const int MaxRecent = 10;
    /// <summary>XSDVISUALIZER_SESSION troca o arquivo (útil para testes não mexerem na sessão real).</summary>
    private readonly string _file = Environment.GetEnvironmentVariable("XSDVISUALIZER_SESSION") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XsdVisualizer", "session.json");

    public SessionStore() => Current = Load();

    public Session Current { get; }

    private Session Load()
    {
        try
        {
            return File.Exists(_file) ? JsonSerializer.Deserialize<Session>(File.ReadAllText(_file)) ?? new() : new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void SaveSchemaSets(IEnumerable<string> openSchemaSets, IEnumerable<string> recent)
    {
        Current.OpenSchemaSets = openSchemaSets.ToList();
        Current.Recent = recent.Take(MaxRecent).ToList();
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Sessão é conveniência: falhar em gravar não deve derrubar o app.
        }
    }

    /// <summary>Coloca a pasta no topo dos recentes, sem duplicar ("pasta" e "pasta/" são a mesma).</summary>
    public static List<string> Touch(IEnumerable<string> recent, string folder) =>
        Normalize(recent.Prepend(folder)).Take(MaxRecent).ToList();

    /// <summary>Remove separador final e duplicatas, mantendo a ordem.</summary>
    public static IEnumerable<string> Normalize(IEnumerable<string> folders) =>
        folders.Select(Path.TrimEndingDirectorySeparator).Distinct(StringComparer.Ordinal);
}
