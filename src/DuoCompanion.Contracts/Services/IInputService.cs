namespace DuoCompanion.Contracts.Services;

public interface IInputService
{
    void SendKey(ushort virtualKeyCode, bool isExtendedKey = false);
    void SendKeyDown(ushort virtualKeyCode);
    void SendKeyUp(ushort virtualKeyCode);
    void SendText(string text);
}
