using DuoCompanion.App.Pages;
using DuoCompanion.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace DuoCompanion.App;

public sealed partial class CompanionWindow : Window
{
    private readonly IWindowManagerService _windowManager;
    private readonly IUiAutomationService _automation;
    private Type _lastManualPage = typeof(KeyboardPage);

    private static readonly Dictionary<string, Type> _pageMap = new()
    {
        ["Keyboard"]    = typeof(KeyboardPage),
        ["Touchpad"]    = typeof(TouchpadPage),
        ["Clipboard"]   = typeof(ClipboardPage),
        ["Media"]       = typeof(MediaPage),
        ["Handwriting"] = typeof(HandwritingPage),
        ["Settings"]    = typeof(SettingsPage),
    };

    public CompanionWindow(IWindowManagerService windowManager, IUiAutomationService automation)
    {
        _windowManager = windowManager;
        _automation    = automation;
        InitializeComponent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption =
            Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;

        _automation.TextInputFocused += OnTextInputFocused;
        _automation.TextInputBlurred += OnTextInputBlurred;
        Activated += OnFirstActivated;
        Closed += OnClosed;
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
    }

    public void NavigateTo(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && _pageMap.TryGetValue(tag, out var pageType))
        {
            _lastManualPage = pageType;
            NavigateTo(pageType);
        }
    }

    private void OnTextInputFocused(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => NavigateTo(typeof(KeyboardPage)));
    }

    private void OnTextInputBlurred(object? sender, EventArgs e)
    {
        // Only revert if user hadn't manually switched away from keyboard
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ContentFrame.CurrentSourcePageType == typeof(KeyboardPage))
                NavigateTo(_lastManualPage == typeof(KeyboardPage) ? typeof(KeyboardPage) : _lastManualPage);
        });
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        var hwnd = WindowNative.GetWindowHandle(this);
        _windowManager.StartMonitoring(hwnd);
        _windowManager.PositionCompanionWindow(hwnd);
        _automation.Start();
        NavigateTo(typeof(KeyboardPage));
    }

    private void OnDisplayConfigurationChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            _windowManager.PositionCompanionWindow(hwnd);
        });
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _automation.Stop();
        _windowManager.DisplayConfigurationChanged -= OnDisplayConfigurationChanged;
        _windowManager.StopMonitoring();
    }
}
