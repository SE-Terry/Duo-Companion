using DuoCompanion.App;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DuoCompanion.App.Pages;

public sealed partial class KeyboardPage : Page
{
    private readonly IInputService _input;
    private bool _shiftActive;
    private bool _ctrlActive;
    private bool _altActive;

    public KeyboardPage()
    {
        _input = App.Services.GetRequiredService<IInputService>();
        InitializeComponent();
    }

    private void OnKeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        var vk = ushort.Parse(tag);

        if (_shiftActive) _input.SendKeyDown(0x10);
        if (_ctrlActive)  _input.SendKeyDown(0x11);
        if (_altActive)   _input.SendKeyDown(0x12);

        _input.SendKey(vk, isExtendedKey: IsExtendedKey(vk));

        if (_altActive)   _input.SendKeyUp(0x12);
        if (_ctrlActive)  _input.SendKeyUp(0x11);
        if (_shiftActive) _input.SendKeyUp(0x10);

        // Auto-release one-shot modifiers
        if (_shiftActive) SetShift(false);
        if (_ctrlActive)  SetModifier(ref _ctrlActive, BtnCtrl);
        if (_altActive)   SetModifier(ref _altActive, BtnAlt);
    }

    private void OnModifierClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        switch (tag)
        {
            case "16": SetShift(!_shiftActive); break;
            case "17": SetModifier(ref _ctrlActive, BtnCtrl); break;
            case "18": SetModifier(ref _altActive,  BtnAlt);  break;
        }
    }

    private void SetShift(bool active)
    {
        _shiftActive = active;
        BtnShift.Background = active
            ? new SolidColorBrush(Colors.SteelBlue)
            : null;
    }

    private static void SetModifier(ref bool state, Button btn)
    {
        state = !state;
        btn.Background = state ? new SolidColorBrush(Colors.SteelBlue) : null;
    }

    private static bool IsExtendedKey(ushort vk) =>
        vk is 0x25 or 0x26 or 0x27 or 0x28 or // arrows
              0x2D or 0x2E or                   // Insert/Delete
              0x91 or 0x5B;                     // ScrollLock, Win
}
