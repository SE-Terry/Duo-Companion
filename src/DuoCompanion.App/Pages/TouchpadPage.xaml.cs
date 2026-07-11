using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DuoCompanion.App.Pages;

public sealed partial class TouchpadPage : Page
{
    private const double Sensitivity = 2.5;
    private readonly IMouseService _mouse;

    public TouchpadPage()
    {
        _mouse = App.Services.GetRequiredService<IMouseService>();
        InitializeComponent();
    }

    private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        var dx = (int)(e.Delta.Translation.X * Sensitivity);
        var dy = (int)(e.Delta.Translation.Y * Sensitivity);
        if (dx != 0 || dy != 0) _mouse.MoveDelta(dx, dy);

        if (e.Delta.Scale != 1.0)
            _mouse.ScrollDelta((int)((e.Delta.Scale - 1.0) * 120 * 3));
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e) =>
        _mouse.Click(MouseButton.Left);

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e) =>
        _mouse.Click(MouseButton.Right);

    private void OnPointerWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(TouchSurface).Properties.MouseWheelDelta;
        _mouse.ScrollDelta(delta);
    }

    private void OnLeftClick(object sender, RoutedEventArgs e) =>
        _mouse.Click(MouseButton.Left);

    private void OnRightClick(object sender, RoutedEventArgs e) =>
        _mouse.Click(MouseButton.Right);
}
