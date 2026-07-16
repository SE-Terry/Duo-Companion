namespace DuoCompanion.Contracts.Services;

public sealed class WindowDragEventArgs : EventArgs
{
    public IntPtr Hwnd { get; }
    public int Left { get; }
    public int Top { get; }
    public int Width { get; }
    public int Height { get; }
    public string ProcessName { get; }

    public WindowDragEventArgs(IntPtr hwnd, int left, int top, int width, int height, string processName = "")
    {
        Hwnd = hwnd;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        ProcessName = processName;
    }

    public int CenterX => Left + Width / 2;
    public int CenterY => Top + Height / 2;
}

public interface IWindowTrackerService
{
    event EventHandler<WindowDragEventArgs>? DragStarted;
    event EventHandler<WindowDragEventArgs>? DragMoved;
    event EventHandler<WindowDragEventArgs>? DragEnded;
    void Start(IntPtr hostHwnd);
    void Stop();
}
