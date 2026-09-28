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
}

public sealed class SessionStore
{
    private const int MaxRecent = 10;
    private readonly string _file = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XsdVisualizer", "session.json");

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

    public static List<string> Touch(IEnumerable<string> recent, string folder) =>
        recent.Where(r => !string.Equals(r, folder, StringComparison.Ordinal)).Prepend(folder).Take(MaxRecent).ToList();
}
