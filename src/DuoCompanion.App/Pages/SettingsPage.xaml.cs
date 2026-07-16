using System.Collections.Generic;
using System.Linq;
using DuoCompanion.App;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
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
    private readonly IStartupRegistrationService _startup;
    private readonly IGlobalHotkeyService _hotkeys;
    private bool _loading = true;

    public SettingsPage()
    {
        _settings = App.Services.GetRequiredService<ISettingsService>();
        _windowManager = App.Services.GetRequiredService<IWindowManagerService>();
        _startup = App.Services.GetRequiredService<IStartupRegistrationService>();
        _hotkeys = App.Services.GetRequiredService<IGlobalHotkeyService>();
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
            "Clipboard" => 1, "Media" => 2, "Handwriting" => 3, _ => 0
        };

        CompanionDisplayCombo.SelectedIndex = s.CompanionDisplay switch { "Left" => 1, "Auto" => 2, _ => 0 };

        AutoHideModeCombo.SelectedIndex = s.AutoHideMode switch { "Off" => 0, "Always" => 2, _ => 1 };

        LaunchOnStartupToggle.IsOn = s.LaunchOnStartup;

        AutoSpanToggle.IsOn = s.DuoSnap.AutoSpanEnabled;

        ActivationHalfWidthBox.Value = s.DuoSnap.ActivationHalfWidth;
        OverlayOpacityBox.Value = s.DuoSnap.OverlayOpacity;
        FadeDurationBox.Value = s.DuoSnap.FadeDurationMilliseconds;
        DwellDurationBox.Value = s.DuoSnap.DwellDurationMilliseconds;

        RestoreBehaviorCombo.SelectedIndex = s.DuoSnap.RestoreBehavior == RestoreBehavior.Never ? 1 : 0;

        SnapIntegrationModeCombo.SelectedIndex = IndexForSnapIntegrationMode(s.DuoSnap.SnapIntegrationMode);

        RefreshIgnoredExecutablesList();
        RefreshProfilesList();

        HotkeyLeftBox.Text = HotkeyTextFor(WindowLayoutKind.Left);
        HotkeyRightBox.Text = HotkeyTextFor(WindowLayoutKind.Right);
        HotkeySpanBox.Text = HotkeyTextFor(WindowLayoutKind.Span);
        HotkeyLeft70Right30Box.Text = HotkeyTextFor(WindowLayoutKind.Left70Right30);
        HotkeyLeft30Right70Box.Text = HotkeyTextFor(WindowLayoutKind.Left30Right70);
        RefreshHotkeyStatus();

        OpacitySlider.Value = s.WindowOpacity;
        _loading = false;
    }

    private string HotkeyTextFor(WindowLayoutKind action) =>
        _settings.Current.DuoSnap.HotkeyBindings.TryGetValue(action, out var text) ? text : string.Empty;

    private static int IndexForSnapIntegrationMode(SnapIntegrationMode mode) => mode switch
    {
        SnapIntegrationMode.ReplaceWindowsSnap => 1,
        SnapIntegrationMode.WindowsSnapDisabledManually => 2,
        _ => 0
    };

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

    private void OnAutoHideModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AutoHideModeCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _settings.Current.AutoHideMode = tag;
        _settings.Save();
    }

    private void OnLaunchOnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Current.LaunchOnStartup = LaunchOnStartupToggle.IsOn;
        _settings.Save();
        _startup.Apply(LaunchOnStartupToggle.IsOn);
    }

    private void OnAutoSpanToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Current.DuoSnap.AutoSpanEnabled = AutoSpanToggle.IsOn;
        _settings.Save();
    }

    private void OnActivationHalfWidthChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(sender.Value)) return;
        _settings.Current.DuoSnap.ActivationHalfWidth = (int)sender.Value;
        _settings.Save();
        ReflectClampedValue(sender, _settings.Current.DuoSnap.ActivationHalfWidth);
    }

    private void OnOverlayOpacityChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(sender.Value)) return;
        _settings.Current.DuoSnap.OverlayOpacity = sender.Value;
        _settings.Save();
        ReflectClampedValue(sender, _settings.Current.DuoSnap.OverlayOpacity);
    }

    private void OnFadeDurationChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(sender.Value)) return;
        _settings.Current.DuoSnap.FadeDurationMilliseconds = (int)sender.Value;
        _settings.Save();
        ReflectClampedValue(sender, _settings.Current.DuoSnap.FadeDurationMilliseconds);
    }

    private void OnDwellDurationChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(sender.Value)) return;
        _settings.Current.DuoSnap.DwellDurationMilliseconds = (int)sender.Value;
        _settings.Save();
        ReflectClampedValue(sender, _settings.Current.DuoSnap.DwellDurationMilliseconds);
    }

    // Normalize() (invoked by Save()) may clamp an out-of-range entry — reflect the
    // clamped value back into the control so invalid input visibly snaps back
    // instead of silently diverging from what is actually persisted.
    private void ReflectClampedValue(NumberBox box, double clampedValue)
    {
        if (box.Value == clampedValue) return;
        _loading = true;
        box.Value = clampedValue;
        _loading = false;
    }

    private void OnRestoreBehaviorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || RestoreBehaviorCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        if (!Enum.TryParse<RestoreBehavior>(tag, out var behavior)) return;

        _settings.Current.DuoSnap.RestoreBehavior = behavior;
        _settings.Save();
    }

    private async void OnSnapIntegrationModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SnapIntegrationModeCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        if (!Enum.TryParse<SnapIntegrationMode>(tag, out var mode)) return;

        if (mode == SnapIntegrationMode.WindowsSnapDisabledManually)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Disable Windows Snap integration?",
                Content = "DuoCompanion will stop offering automatic hinge-drag spanning and will require every " +
                          "layout to be an explicit selection. This does not change any Windows setting itself — " +
                          "it only records that you have already turned Windows Snap off yourself.",
                PrimaryButtonText = "Disable",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                _loading = true;
                SnapIntegrationModeCombo.SelectedIndex = IndexForSnapIntegrationMode(_settings.Current.DuoSnap.SnapIntegrationMode);
                _loading = false;
                return;
            }
        }

        _settings.Current.DuoSnap.SnapIntegrationMode = mode;
        _settings.Save();
    }

    private void RefreshIgnoredExecutablesList()
    {
        IgnoredExeList.ItemsSource = _settings.Current.DuoSnap.IgnoredExecutableNames.ToList();
    }

    private void OnAddIgnoredExecutable(object sender, RoutedEventArgs e)
    {
        var name = IgnoredExeInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        var names = _settings.Current.DuoSnap.IgnoredExecutableNames;
        if (!names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            names.Add(name);

        IgnoredExeInput.Text = string.Empty;
        _settings.Save();
        RefreshIgnoredExecutablesList();
    }

    private void OnRemoveIgnoredExecutable(object sender, RoutedEventArgs e)
    {
        var index = IgnoredExeList.SelectedIndex;
        var names = _settings.Current.DuoSnap.IgnoredExecutableNames;
        if (index < 0 || index >= names.Count) return;

        names.RemoveAt(index);
        _settings.Save();
        RefreshIgnoredExecutablesList();
    }

    private void RefreshProfilesList()
    {
        ProfilesList.ItemsSource = _settings.Current.DuoSnap.Profiles
            .Select(p => $"{p.ExecutableName} → {(p.IsIgnored ? "Ignored" : p.Layout.ToString())}")
            .ToList();
    }

    private void OnAddOrUpdateProfile(object sender, RoutedEventArgs e)
    {
        var name = ProfileExeInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;
        if (ProfileLayoutCombo.SelectedItem is not ComboBoxItem { Tag: string layoutTag } ||
            !Enum.TryParse<WindowLayoutKind>(layoutTag, out var layout))
            return;

        var profiles = _settings.Current.DuoSnap.Profiles;
        profiles.RemoveAll(p => string.Equals(p.ExecutableName, name, StringComparison.OrdinalIgnoreCase));
        profiles.Add(new AppLayoutProfile
        {
            ExecutableName = name,
            Layout = layout,
            IsIgnored = ProfileIgnoredCheck.IsChecked == true,
        });

        ProfileExeInput.Text = string.Empty;
        ProfileIgnoredCheck.IsChecked = false;
        _settings.Save();
        RefreshProfilesList();
    }

    private void OnRemoveProfile(object sender, RoutedEventArgs e)
    {
        var index = ProfilesList.SelectedIndex;
        var profiles = _settings.Current.DuoSnap.Profiles;
        if (index < 0 || index >= profiles.Count) return;

        profiles.RemoveAt(index);
        _settings.Save();
        RefreshProfilesList();
    }

    private void OnHotkeyBindingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not TextBox { Tag: string tag } box) return;
        if (!Enum.TryParse<WindowLayoutKind>(tag, out var action)) return;

        var text = box.Text?.Trim() ?? string.Empty;
        var bindings = _settings.Current.DuoSnap.HotkeyBindings;
        if (string.IsNullOrWhiteSpace(text))
            bindings.Remove(action);
        else
            bindings[action] = text;

        _settings.Save();
        RefreshHotkeyStatus();
    }

    // Recomputes conflict status two ways: a live, pure re-resolution of the
    // currently configured bindings (parse errors and cross-action duplicates,
    // via the same HotkeyBindingResolver the hotkey service itself uses), plus
    // the OS-level registration outcome as of the last time the hotkey service
    // started (it does not re-register live, so that half is informational only).
    private void RefreshHotkeyStatus()
    {
        var resolution = HotkeyBindingResolver.Resolve(_settings.Current.DuoSnap.HotkeyBindings);
        var lines = new List<string>();

        foreach (var error in resolution.Errors)
            lines.Add($"{error.Action}: {error.Reason}");

        foreach (var result in _hotkeys.RegistrationResults.Where(r => !r.Succeeded))
            lines.Add($"{result.Action}: {result.Error} (as of last launch)");

        HotkeyStatusText.Text = lines.Count == 0 ? "No conflicts." : string.Join(Environment.NewLine, lines);
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

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        App.Quit();
    }
}
