using System.Windows.Forms;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Tray;

public sealed class TrayIconService : ITrayIconService, IDisposable
{
    private readonly ILogger<TrayIconService> _logger;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _menu;
    private IntPtr _iconHandle;

    public event EventHandler? ToggleVisibilityRequested;
    public event EventHandler? QuitRequested;

    public TrayIconService(ILogger<TrayIconService> logger) => _logger = logger;

    public void Start()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Show/Hide Duo Companion", null, (_, _) => ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty));

        _iconHandle = SystemIconSource.ExtractKeyboardIconHandle(small: true);
        var icon = _iconHandle != IntPtr.Zero
            ? System.Drawing.Icon.FromHandle(_iconHandle)
            : System.Drawing.SystemIcons.Application;

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "Duo Companion",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += OnMouseClick;

        _logger.LogInformation("Tray icon started");
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_notifyIcon is null) return;
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _menu?.Dispose();
        _menu = null;
        SystemIconSource.DestroyIconHandle(_iconHandle);
        _iconHandle = IntPtr.Zero;
        _logger.LogInformation("Tray icon stopped");
    }

    public void Dispose() => Stop();
}
