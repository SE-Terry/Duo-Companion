namespace DuoCompanion.Contracts.Services;

public enum MouseButton { Left, Right }

public interface IMouseService
{
    void MoveDelta(int dx, int dy);
    void Click(MouseButton button);
    void ScrollDelta(int wheelDelta);
}
