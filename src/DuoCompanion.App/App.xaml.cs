using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Automation;
using DuoCompanion.Services.Clipboard;
using DuoCompanion.Services.Display;
using DuoCompanion.Services.Input;
using DuoCompanion.Services.Media;
using DuoCompanion.Services.Settings;
using DuoCompanion.Services.Tray;
using DuoCompanion.Services.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace DuoCompanion.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static CompanionWindow? CompanionWindow { get; private set; }

    public App()
    {
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

            var orientation = Services.GetRequiredService<IOrientationService>();
            Services.GetRequiredService<IWindowManagerService>().DisplayConfigurationChanged +=
                (_, _) => orientation.Refresh();

            CompanionWindow = new CompanionWindow(
                Services.GetRequiredService<IWindowManagerService>(),
                Services.GetRequiredService<IUiAutomationService>(),
                Services.GetRequiredService<ISettingsService>());
            CompanionWindow.Activate();
            WriteStartupLog("Companion window activated.");

            var tray = Services.GetRequiredService<ITrayIconService>();
            tray.ToggleVisibilityRequested += (_, _) => CompanionWindow?.ToggleVisibility();
            tray.QuitRequested += (_, _) => Quit();
            tray.Start();
            WriteStartupLog("Tray icon started.");
        }
        catch (Exception ex)
        {
            WriteStartupLog($"Application launch failed:{Environment.NewLine}{ex}");
            throw;
        }
    }

    public static void Quit()
    {
        Services.GetRequiredService<ITrayIconService>().Stop();
        Application.Current.Exit();
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
        services.AddSingleton<ITrayIconService, TrayIconService>();
        return services.BuildServiceProvider();
    }
}
