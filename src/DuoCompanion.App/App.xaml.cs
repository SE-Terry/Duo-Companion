using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Automation;
using DuoCompanion.Services.Clipboard;
using DuoCompanion.Services.Display;
using DuoCompanion.Services.Input;
using DuoCompanion.Services.Media;
using DuoCompanion.Services.Settings;
using DuoCompanion.Services.Snap;
using DuoCompanion.Services.Tray;
using DuoCompanion.Services.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace DuoCompanion.App;

public partial class App : Application
{
    // -4 = DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2. Set this way instead of via
    // app.manifest: a hand-authored manifest on this unpackaged WindowsAppSDK project
    // replaces the SDK's own build-generated manifest (needed for regfree WinRT/COM
    // activation) instead of merging with it, which breaks launch entirely with a
    // "side-by-side configuration is incorrect" error.
    private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    // Held for the process lifetime — releasing it (e.g. by letting it get GC'd)
    // would let a second instance start.
    private static System.Threading.Mutex? _singleInstanceMutex;

    public static IServiceProvider Services { get; private set; } = null!;
    public static CompanionWindow? CompanionWindow { get; private set; }
    private static SpanOverlayWindow? _spanOverlayWindow;

    public App()
    {
        _singleInstanceMutex = new System.Threading.Mutex(
            initiallyOwned: true, "DuoCompanion.SingleInstance.Mutex", out var createdNew);
        if (!createdNew)
        {
            // Another instance already owns the mutex — exit immediately, before any
            // real initialization (services, windows, tray icon) has happened.
            Environment.Exit(0);
        }

        SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
        WriteStartupLog("App constructor started.");
        try
        {
            InitializeComponent();
            WriteStartupLog("WinUI application initialized.");

            Services = BuildServices();
            UnhandledException += OnUnhandledException;
            WriteStartupLog("Services initialized.");
        }
        catch (Exception ex)
        {
            WriteStartupLog($"App constructor failed:{Environment.NewLine}{ex}");
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Services.GetRequiredService<ILogger<App>>().LogCritical(e.Exception, "Unhandled exception on UI thread");
        WriteStartupLog($"Unhandled UI exception:{Environment.NewLine}{e.Exception}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        WriteStartupLog("Application launch started.");
        try
        {
            Services.GetRequiredService<IClipboardService>().Initialize();

            var settingsService = Services.GetRequiredService<ISettingsService>();
            var startup = Services.GetRequiredService<IStartupRegistrationService>();
            if (!startup.Apply(settingsService.Current.LaunchOnStartup))
                WriteStartupLog("Startup registration reconciliation failed — see application log for details.");

            var orientation = Services.GetRequiredService<IOrientationService>();
            Services.GetRequiredService<IWindowManagerService>().DisplayConfigurationChanged +=
                (_, _) => orientation.Refresh();

            CompanionWindow = new CompanionWindow(
                Services.GetRequiredService<IWindowManagerService>(),
                Services.GetRequiredService<IUiAutomationService>(),
                settingsService);
            CompanionWindow.Activate();
            WriteStartupLog("Companion window activated.");

            var tray = Services.GetRequiredService<ITrayIconService>();
            tray.ToggleVisibilityRequested += (_, _) => CompanionWindow?.ToggleVisibility();
            tray.QuitRequested += (_, _) => Quit();
            tray.Start();
            WriteStartupLog("Tray icon started.");

            _spanOverlayWindow = new SpanOverlayWindow(Services.GetRequiredService<IWindowManagerService>());
            var overlay = _spanOverlayWindow;
            var autoSpan = Services.GetRequiredService<IAutoSpanCoordinatorService>();
            var settingsMonitor = Services.GetRequiredService<IDuoSnapSettingsMonitor>();
            autoSpan.SpanCandidateEntered += (_, e) => overlay.DispatcherQueue.TryEnqueue(() =>
            {
                var settings = settingsMonitor.Current;
                if (settings.AutoSpanEnabled)
                    overlay.ShowAt(e.Target, settings.OverlayOpacity, settings.FadeDurationMilliseconds);
            });
            autoSpan.SpanCandidateExited += (_, _) => overlay.DispatcherQueue.TryEnqueue(() =>
            {
                var settings = settingsMonitor.Current;
                overlay.HideOverlay(settings.FadeDurationMilliseconds);
            });
            settingsMonitor.Changed += (_, _) => overlay.DispatcherQueue.TryEnqueue(() =>
            {
                var settings = settingsMonitor.Current;
                if (!settings.AutoSpanEnabled)
                    overlay.HideOverlay(0);
                else
                    overlay.ApplySettings(settings.OverlayOpacity, settings.FadeDurationMilliseconds);
            });
            autoSpan.Start(WindowNative.GetWindowHandle(CompanionWindow));
            WriteStartupLog("Auto-span coordinator started.");

            Services.GetRequiredService<IGlobalHotkeyService>().Start();
            WriteStartupLog("Global hotkey service started.");
        }
        catch (Exception ex)
        {
            WriteStartupLog($"Application launch failed:{Environment.NewLine}{ex}");
            throw;
        }
    }

    public static void Quit()
    {
        Services.GetRequiredService<IGlobalHotkeyService>().Stop();
        Services.GetRequiredService<ITrayIconService>().Stop();
        Services.GetRequiredService<IAutoSpanCoordinatorService>().Stop();
        var overlay = _spanOverlayWindow;
        if (overlay is null || !overlay.DispatcherQueue.TryEnqueue(() =>
            {
                overlay.HideOverlay(0);
                Application.Current.Exit();
            }))
        {
            Application.Current.Exit();
        }
    }

    private static void WriteStartupLog(string message)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log"),
                $"{DateTime.Now:O} {message}{Environment.NewLine}{new string('-', 40)}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must not prevent application startup.
        }
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<IWindowManagerService, WindowManagerService>();
        services.AddSingleton<IInputService, InputService>();
        services.AddSingleton<IUiAutomationService, UiAutomationService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IMediaService, MediaService>();
        services.AddSingleton<IOrientationService, OrientationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDuoSnapSettingsMonitor, DuoSnapSettingsMonitor>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IHingeTopologyService, HingeTopologyService>();
        services.AddSingleton<IWindowIdentityService, WindowIdentityService>();
        services.AddSingleton<IWindowTrackerService, WindowTrackerService>();
        services.AddSingleton<IWindowSpanService, WindowSpanService>();
        services.AddSingleton<IWindowsSnapIntegrationService, WindowsSnapIntegrationService>();
        services.AddSingleton<IAppLayoutProfileService, AppLayoutProfileService>();
        services.AddSingleton<ILayoutSuggestionService, LayoutSuggestionService>();
        services.AddSingleton<IAutoSpanCoordinatorService, AutoSpanCoordinatorService>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IStartupRegistrationService, StartupRegistrationService>();
        return services.BuildServiceProvider();
    }
}
