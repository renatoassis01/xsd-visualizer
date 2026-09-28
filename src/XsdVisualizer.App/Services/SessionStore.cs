using System.Text.Json;

namespace XsdVisualizer.App.Services;

/// <summary>Schema Sets abertos e recentes, persistidos por usuário.</summary>
public sealed class Session
{
    public List<string> OpenSchemaSets { get; set; } = [];
    public List<string> Recent { get; set; } = [];
}

public sealed class SessionStore
{
    private const int MaxRecent = 10;
    private readonly string _file = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XsdVisualizer", "session.json");

    public Session Load()
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

    public void Save(IEnumerable<string> openSchemaSets, IEnumerable<string> recent)
    {
        var session = new Session { OpenSchemaSets = openSchemaSets.ToList(), Recent = recent.Take(MaxRecent).ToList() };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Sessão é conveniência: falhar em gravar não deve derrubar o app.
        }
    }

    public static List<string> Touch(IEnumerable<string> recent, string folder) =>
        recent.Where(r => !string.Equals(r, folder, StringComparison.Ordinal)).Prepend(folder).Take(MaxRecent).ToList();
}
