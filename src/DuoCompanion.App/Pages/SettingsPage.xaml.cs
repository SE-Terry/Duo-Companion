using DuoCompanion.App;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinRT.Interop;

namespace DuoCompanion.App.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ISettingsService _settings;
    private readonly IWindowManagerService _windowManager;
    private bool _loading = true;

    public SettingsPage()
    {
        _settings = App.Services.GetRequiredService<ISettingsService>();
        _windowManager = App.Services.GetRequiredService<IWindowManagerService>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        var s = _settings.Current;

        ThemeCombo.SelectedIndex = s.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };

        DefaultModuleCombo.SelectedIndex = s.DefaultModule switch
        {
            "Touchpad"    => 1, "Clipboard" => 2,
            "Media"       => 3, "Handwriting" => 4, _ => 0
        };

        CompanionDisplayCombo.SelectedIndex = s.CompanionDisplay switch { "Left" => 1, "Auto" => 2, _ => 0 };

        OpacitySlider.Value = s.WindowOpacity;
        _loading = false;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _settings.Current.Theme = tag;
        _settings.Save();
    }

    private void OnDefaultModuleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || DefaultModuleCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _settings.Current.DefaultModule = tag;
        _settings.Save();
    }

    private void OnCompanionDisplayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CompanionDisplayCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _settings.Current.CompanionDisplay = tag;
        _settings.Save();

        if (App.CompanionWindow is not null)
            _windowManager.PositionCompanionWindow(WindowNative.GetWindowHandle(App.CompanionWindow));
    }

    private void OnOpacityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Current.WindowOpacity = OpacitySlider.Value;
        _settings.Save();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _settings.Reset();
        OnLoaded(sender, new RoutedEventArgs());
    }
}
