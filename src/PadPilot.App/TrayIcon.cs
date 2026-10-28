using System.ComponentModel;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PadPilot.App;

/// <summary>PadPilot's icon in the Windows notification area (tray), with an Open / Exit menu.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _openItem;
    private readonly Forms.ToolStripMenuItem _exitItem;
    private bool _balloonShown;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico")).Stream;

        _openItem = new Forms.ToolStripMenuItem();
        _openItem.Font = new System.Drawing.Font(_openItem.Font, System.Drawing.FontStyle.Bold);
        _openItem.Click += (_, _) => OpenRequested?.Invoke();
        _exitItem = new Forms.ToolStripMenuItem();
        _exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_openItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_exitItem);

        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream),
            Text = "PadPilot",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();

        Loc.Instance.PropertyChanged += OnLanguageChanged;
        UpdateMenuTexts();
    }

    /// <summary>Hover text, e.g. "PadPilot · DualSense verbunden · 45 %". Windows allows up to 127 characters.</summary>
    public void SetToolTip(string text) => _icon.Text = text.Length > 127 ? text[..127] : text;

    /// <summary>Tells the user once per session where the window went.</summary>
    public void ShowHiddenHint()
    {
        if (_balloonShown)
            return;
        _balloonShown = true;
        _icon.ShowBalloonTip(3000, "PadPilot", Loc.Get("Tray_Balloon"), Forms.ToolTipIcon.None);
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => UpdateMenuTexts();

    private void UpdateMenuTexts()
    {
        _openItem.Text = Loc.Get("Tray_Open");
        _exitItem.Text = Loc.Get("Tray_Exit");
    }

    public void Dispose()
    {
        Loc.Instance.PropertyChanged -= OnLanguageChanged;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
