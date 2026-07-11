using DuoCompanion.App;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DuoCompanion.App.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ISettingsService _settings;
    private bool _loading = true;

    public SettingsPage()
    {
        _settings = App.Services.GetRequiredService<ISettingsService>();
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
