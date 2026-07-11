namespace DuoCompanion.Contracts.Services;

public enum ScreenLayout { SinglePortrait, SingleLandscape, DualPortrait, DualLandscape }

public interface IOrientationService
{
    ScreenLayout Current { get; }
    event EventHandler<ScreenLayout> LayoutChanged;
    void Refresh();
}
