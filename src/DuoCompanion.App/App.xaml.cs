using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Automation;
using DuoCompanion.Services.Clipboard;
using DuoCompanion.Services.Display;
using DuoCompanion.Services.Input;
using DuoCompanion.Services.Media;
using DuoCompanion.Services.Settings;
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
        InitializeComponent();
        Services = BuildServices();
        UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Services.GetRequiredService<ILogger<App>>().LogCritical(e.Exception, "Unhandled exception on UI thread");
        System.IO.File.AppendAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"),
            $"{DateTime.Now:O}{Environment.NewLine}{e.Exception}{Environment.NewLine}{new string('-', 40)}{Environment.NewLine}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services.GetRequiredService<IClipboardService>().Initialize();

        var orientation = Services.GetRequiredService<IOrientationService>();
        Services.GetRequiredService<IWindowManagerService>().DisplayConfigurationChanged +=
            (_, _) => orientation.Refresh();

        CompanionWindow = new CompanionWindow(
            Services.GetRequiredService<IWindowManagerService>(),
            Services.GetRequiredService<IUiAutomationService>());
        CompanionWindow.Activate();
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
        services.AddSingleton<IMouseService, MouseService>();
        services.AddSingleton<IMediaService, MediaService>();
        services.AddSingleton<IOrientationService, OrientationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        return services.BuildServiceProvider();
    }
}
