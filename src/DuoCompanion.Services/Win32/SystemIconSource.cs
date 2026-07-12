namespace DuoCompanion.Services.Win32;

public static class SystemIconSource
{
    // TabTip.exe is Windows' own Touch Keyboard/On-Screen Keyboard executable —
    // ships on every Windows install, and its icon (index 0) is the standard
    // Windows keyboard icon, which fits a keyboard companion app.
    private static readonly string KeyboardIconSourcePath =
        Path.Combine(Environment.SystemDirectory, "TabTip.exe");

    public static IntPtr ExtractKeyboardIconHandle(bool small)
    {
        var large = new IntPtr[1];
        var smallIcons = new IntPtr[1];
        var extracted = NativeMethods.ExtractIconEx(KeyboardIconSourcePath, 0, large, smallIcons, 1);
        if (extracted == 0) return IntPtr.Zero;

        return small ? smallIcons[0] : large[0];
    }

    public static void DestroyIconHandle(IntPtr hIcon)
    {
        if (hIcon != IntPtr.Zero) NativeMethods.DestroyIcon(hIcon);
    }
}
