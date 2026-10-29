using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PadPilot.Core.Mods;

namespace PadPilot.App;

/// <summary>The "Mods" tab: UDP server, text-file method, last command, reset and test.</summary>
public sealed class ModsViewModel : ObservableObject
{
    public const string Example = """
        {
          "lightbar": "#FF0000",
          "playerLeds": [0, 0, 1, 0, 0],
          "rumble": { "left": 0, "right": 0 },
          "leftTrigger": {
            "mode": "resistance", "start": 2, "strength": 4 },
          "rightTrigger": {
            "mode": "weapon", "start": 2, "end": 6, "strength": 8 }
        }
        """;

    private readonly UdpModServer _udp;
    private readonly ModFileWatcher _file;
    private readonly ModOverrides _overrides;
    private readonly AppSettings _settings;

    private string? _udpError;
    private string _udpStatusText = "";
    private string _lastCommandText = "";
    private int _commandCount;
    private bool _overridesActive;

    /// <summary>Raised when the overrides were cleared from the UI, so the controller gets updated.</summary>
    public event Action? OverridesCleared;

    public ModsViewModel(UdpModServer udp, ModFileWatcher file, ModOverrides overrides, AppSettings settings)
    {
        _udp = udp;
        _file = file;
        _overrides = overrides;
        _settings = settings;
        Loc.Instance.PropertyChanged += (_, _) => UpdateUdpText();
        RestartUdp();
        RestartFile();
    }

    public bool UdpEnabled
    {
        get => _settings.ModUdpEnabled;
        set { _settings.ModUdpEnabled = value; _settings.Save(); OnPropertyChanged(nameof(UdpEnabled)); RestartUdp(); }
    }

    public int Port
    {
        get => _settings.ModUdpPort;
        set
        {
            int port = Math.Clamp(value, 1024, 65535);
            if (port == _settings.ModUdpPort)
                return;
            _settings.ModUdpPort = port;
            _settings.Save();
            OnPropertyChanged(nameof(Port));
            RestartUdp();
        }
    }

    public bool FileEnabled
    {
        get => _settings.ModFileEnabled;
        set { _settings.ModFileEnabled = value; _settings.Save(); OnPropertyChanged(nameof(FileEnabled)); RestartFile(); }
    }

    public string FilePath => _settings.ModFilePath ?? AppSettings.DefaultModFilePath;
    public string ExampleJson => Example;

    public string UdpStatusText { get => _udpStatusText; private set => Set(ref _udpStatusText, value); }
    public bool UdpOk => _udp.IsRunning;
    public bool UdpFailed => _settings.ModUdpEnabled && _udpError != null;
    public string LastCommandText { get => _lastCommandText; private set => Set(ref _lastCommandText, value); }
    public int CommandCount { get => _commandCount; private set => Set(ref _commandCount, value); }
    public bool OverridesActive { get => _overridesActive; private set => Set(ref _overridesActive, value); }

    /// <summary>Called on the UI thread for every command received.</summary>
    public void Report(string source, ModCommand? cmd, string? error)
    {
        CommandCount++;
        string time = DateTime.Now.ToString("HH:mm:ss");
        LastCommandText = error != null
            ? $"{time} · {source} · {Loc.Format("Mods_Invalid", error)}"
            : $"{time} · {source} · {Describe(cmd!)}";
        OverridesActive = _overrides.IsActive;
    }

    public void ResetOverrides()
    {
        _overrides.Clear();
        OverridesActive = false;
        OverridesCleared?.Invoke();
    }

    /// <summary>Sends the example command to PadPilot's own UDP port, exactly like a mod would.</summary>
    public void SendTest()
    {
        using var client = new UdpClient(AddressFamily.InterNetwork);
        byte[] data = Encoding.UTF8.GetBytes(Example);
        client.Send(data, data.Length, new IPEndPoint(IPAddress.Loopback, _settings.ModUdpPort));
    }

    public void OpenFileFolder()
    {
        string dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void RestartUdp()
    {
        _udp.Stop();
        _udpError = _settings.ModUdpEnabled ? _udp.Start(_settings.ModUdpPort) : null;
        UpdateUdpText();
    }

    private void RestartFile()
    {
        _file.Stop();
        if (_settings.ModFileEnabled)
            _file.Start(FilePath);
    }

    private void UpdateUdpText()
    {
        UdpStatusText = !_settings.ModUdpEnabled ? Loc.Get("Mods_UdpOff")
            : _udpError != null ? Loc.Format("Mods_UdpError", _udpError)
            : Loc.Format("Mods_UdpListening", _settings.ModUdpPort);
        OnPropertyChanged(nameof(UdpOk));
        OnPropertyChanged(nameof(UdpFailed));
    }

    private static string Describe(ModCommand c)
    {
        if (c.IsStatusRequest)
            return Loc.Get("Mods_StatusRequest");
        var parts = new List<string>();
        if (c.Reset) parts.Add("reset");
        if (c.Lightbar is { } lb) parts.Add($"lightbar #{lb.R:X2}{lb.G:X2}{lb.B:X2}");
        if (c.PlayerLeds != null) parts.Add("playerLeds");
        if (c.MicLed != null) parts.Add($"micLed {c.MicLed.ToString()!.ToLowerInvariant()}");
        if (c.Rumble is { } rumble) parts.Add($"rumble {rumble.Left}/{rumble.Right}");
        if (c.LeftTrigger != null) parts.Add("leftTrigger");
        if (c.RightTrigger != null) parts.Add("rightTrigger");
        return string.Join(", ", parts);
    }
}
