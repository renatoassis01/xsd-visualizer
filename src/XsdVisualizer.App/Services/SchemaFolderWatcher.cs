namespace XsdVisualizer.App.Services;

/// <summary>Avisa (com debounce) quando algum .xsd de uma pasta muda em disco.</summary>
public sealed class SchemaFolderWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounce;

    public SchemaFolderWatcher(string folder, Action changed)
    {
        _debounce = new Timer(_ => changed(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(folder, "*.xsd")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = false,
        };
        _watcher.Changed += OnChange;
        _watcher.Created += OnChange;
        _watcher.Deleted += OnChange;
        _watcher.Renamed += OnChange;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnChange(object sender, FileSystemEventArgs e) => _debounce.Change(700, Timeout.Infinite);

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
