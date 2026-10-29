namespace PadPilot.Core.Mods;

/// <summary>
/// Text-file method for mods that can't use UDP: the mod writes a command (same JSON as over UDP)
/// into a file, and PadPilot applies it whenever the file changes.
/// </summary>
public sealed class ModFileWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private string? _path;

    public event Action<ModCommand?, string?>? Received;

    public string? Path => _path;

    public void Start(string path)
    {
        Stop();
        _path = path;
        string dir = System.IO.Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);

        // Writers often trigger several change events in a row; read once things have settled.
        _debounce = new Timer(_ => ReadFile(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(dir, System.IO.Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => _debounce.Change(50, Timeout.Infinite);
        _watcher.Created += (_, _) => _debounce.Change(50, Timeout.Infinite);
        _watcher.Renamed += (_, _) => _debounce.Change(50, Timeout.Infinite);
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _debounce?.Dispose();
        _watcher = null;
        _debounce = null;
    }

    private void ReadFile()
    {
        if (_path == null)
            return;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                string text = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(text))
                    return;
                ModCommandParser.TryParse(text, out var command, out var error);
                Received?.Invoke(command, error);
                return;
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(20); // the mod is still writing
            }
        }
    }

    public void Dispose() => Stop();
}
