using DuoCompanion.Contracts.Services;

namespace DuoCompanion.Services.Media;

public sealed class MediaService : IMediaService
{
    // Virtual key codes for media keys
    private const ushort VK_MEDIA_PLAY_PAUSE = 0xB3;
    private const ushort VK_MEDIA_NEXT_TRACK = 0xB0;
    private const ushort VK_MEDIA_PREV_TRACK = 0xB1;
    private const ushort VK_VOLUME_UP        = 0xAF;
    private const ushort VK_VOLUME_DOWN      = 0xAE;
    private const ushort VK_VOLUME_MUTE      = 0xAD;

    private readonly IInputService _input;

    public MediaService(IInputService input) => _input = input;

    public void PlayPause()  => _input.SendKey(VK_MEDIA_PLAY_PAUSE);
    public void Next()       => _input.SendKey(VK_MEDIA_NEXT_TRACK);
    public void Previous()   => _input.SendKey(VK_MEDIA_PREV_TRACK);
    public void VolumeUp()   => _input.SendKey(VK_VOLUME_UP);
    public void VolumeDown() => _input.SendKey(VK_VOLUME_DOWN);
    public void Mute()       => _input.SendKey(VK_VOLUME_MUTE);
}
