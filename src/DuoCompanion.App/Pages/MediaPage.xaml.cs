using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DuoCompanion.App.Pages;

public sealed partial class MediaPage : Page
{
    private readonly IMediaService _media;

    public MediaPage()
    {
        _media = App.Services.GetRequiredService<IMediaService>();
        InitializeComponent();
    }

    private void OnPlayPause(object s, RoutedEventArgs e)  => _media.PlayPause();
    private void OnNext(object s, RoutedEventArgs e)       => _media.Next();
    private void OnPrev(object s, RoutedEventArgs e)       => _media.Previous();
    private void OnVolumeUp(object s, RoutedEventArgs e)   => _media.VolumeUp();
    private void OnVolumeDown(object s, RoutedEventArgs e) => _media.VolumeDown();
    private void OnMute(object s, RoutedEventArgs e)       => _media.Mute();
}
