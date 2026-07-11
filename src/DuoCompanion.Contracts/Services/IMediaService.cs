namespace DuoCompanion.Contracts.Services;

public interface IMediaService
{
    void PlayPause();
    void Next();
    void Previous();
    void VolumeUp();
    void VolumeDown();
    void Mute();
}
