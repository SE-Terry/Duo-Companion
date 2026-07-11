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
    private readonly ISettingsService _settings;
    private readonly Frame _contentFrame = new();
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private Type _lastManualPage = typeof(KeyboardPage);
    private bool _isHidden;

    private static readonly Dictionary<string, Type> _pageMap = new()
    {
        ["Keyboard"]    = typeof(KeyboardPage),
        ["Touchpad"]    = typeof(TouchpadPage),
        ["Clipboard"]   = typeof(ClipboardPage),
        ["Media"]       = typeof(MediaPage),
        ["Handwriting"] = typeof(HandwritingPage),
        ["Settings"]    = typeof(SettingsPage),
    };

    public CompanionWindow(IWindowManagerService windowManager, IUiAutomationService automation, ISettingsService settings)
    {
        _windowManager = windowManager;
        _automation    = automation;
        _settings      = settings;
        InitializeComponent();
        Content = CreateContent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption =
            Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;

        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            HideCompanionWindowNow();
        };

        _automation.TextInputFocused += OnTextInputFocused;
        _automation.TextInputBlurred += OnTextInputBlurred;
        Activated += OnFirstActivated;
        Closed += OnClosed;
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
    }

    public void NavigateTo(Type pageType)
    {
        if (_contentFrame.CurrentSourcePageType != pageType)
            _contentFrame.Navigate(pageType);
    }

    public void ToggleVisibility()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isHidden)
            {
                NavigateTo(_lastManualPage);
                ShowCompanionWindowNow();
            }
            else
            {
                HideCompanionWindowNow();
            }
        });
    }

    private void ShowCompanionWindowNow()
    {
        _hideTimer.Stop();
        if (!_isHidden) return;
        _windowManager.ShowCompanionWindow(WindowNative.GetWindowHandle(this));
        _isHidden = false;
    }

    private void HideCompanionWindowNow()
    {
        if (_isHidden) return;
        _windowManager.HideCompanionWindow(WindowNative.GetWindowHandle(this));
        _isHidden = true;
    }

    private void HideCompanionWindowDebounced()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
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
        DispatcherQueue.TryEnqueue(() =>
        {
            NavigateTo(typeof(KeyboardPage));
            ShowCompanionWindowNow();
        });
    }

    private void OnTextInputBlurred(object? sender, EventArgs e)
    {
        // Only act if the window is currently on the auto-triggered keyboard page —
        // if the user manually pinned a different page, a background focus change
        // elsewhere on the system shouldn't touch this window at all.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_contentFrame.CurrentSourcePageType != typeof(KeyboardPage)) return;

            var mode = _settings.Current.AutoHideMode;
            var hasManualPin = _lastManualPage != typeof(KeyboardPage);

            if (mode == "Always" || (mode == "Smart" && !hasManualPin))
            {
                HideCompanionWindowDebounced();
                return;
            }

            NavigateTo(_lastManualPage);
        });
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        var hwnd = WindowNative.GetWindowHandle(this);
        _windowManager.StartMonitoring(hwnd);
        _windowManager.PositionCompanionWindow(hwnd);
        _windowManager.MakeWindowNonActivating(hwnd);
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

    private Grid CreateContent()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var navigationBar = new Grid();
        navigationBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        navigationBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var navigationButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Padding = new Thickness(8, 0, 8, 0)
        };
        navigationButtons.Children.Add(CreateNavigationButton("Keyboard", "Keyboard", "\uE765"));
        navigationButtons.Children.Add(CreateNavigationButton("Touchpad", "Touchpad", "\uE7C5"));
        navigationButtons.Children.Add(CreateNavigationButton("Clipboard", "Clipboard", "\uE77F"));
        navigationButtons.Children.Add(CreateNavigationButton("Media", "Media", "\uE768"));
        navigationButtons.Children.Add(CreateNavigationButton("Handwriting", "Handwriting", "\uED63"));

        var settingsButton = CreateNavigationButton("Settings", "Settings", "\uE713");
        settingsButton.Margin = new Thickness(0, 0, 8, 0);

        navigationBar.Children.Add(navigationButtons);
        Grid.SetColumn(settingsButton, 1);
        navigationBar.Children.Add(settingsButton);

        root.Children.Add(navigationBar);
        Grid.SetRow(_contentFrame, 1);
        root.Children.Add(_contentFrame);
        return root;
    }

    private Button CreateNavigationButton(string tag, string toolTip, string glyph)
    {
        var button = new Button
        {
            Tag = tag,
            Content = new FontIcon { Glyph = glyph }
        };
        ToolTipService.SetToolTip(button, toolTip);
        button.Click += OnNavClick;
        return button;
    }
}
