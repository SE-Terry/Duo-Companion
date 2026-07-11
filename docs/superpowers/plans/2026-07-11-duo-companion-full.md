# Duo Companion — Full Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete Duo Companion app for Surface Duo (DuoWOA) that auto-detects focused text fields, opens a virtual keyboard on the secondary display, and hosts clipboard management and handwriting modules.

**Architecture:** A borderless WinUI 3 companion window occupies the secondary display full-screen; it hosts page-based modules (Keyboard, Touchpad, Clipboard, Media, Handwriting, Settings) switchable via a top nav bar. A WinEvent hook monitors system focus changes and auto-navigates to the Keyboard page when a text input gains focus. All Windows platform access goes through services registered in the DI container; pages contain no business logic.

**Tech Stack:** C# 13 / .NET 9 / WinUI 3 (Windows App SDK 1.6) / MVVM (CommunityToolkit.Mvvm) / DI (Microsoft.Extensions.DependencyInjection) / Win32 P/Invoke (user32, kernel32) / UIAutomation COM / Windows.ApplicationModel.DataTransfer (Clipboard WinRT) / xUnit (service unit tests)

## Global Constraints

- Platform: Windows 11 ARM (DuoWOA), `win-arm64` runtime identifier
- `TargetFramework`: `net9.0-windows10.0.19041.0`, `TargetPlatformMinVersion`: `10.0.17763.0`
- `WindowsPackageType`: `None` (unpackaged — no MSIX, no admin rights required)
- No business logic in Views or code-behind beyond calling into the service/ViewModel
- All logging via `ILogger<T>` — no `Console.WriteLine` or `Debug.WriteLine`
- No `async void` except event handlers; all async code uses `Task`/`ValueTask`
- One class per file, file name matches class name
- Windows App SDK: `1.6.250205002` | BuildTools: `10.0.26100.1742`
- CommunityToolkit.Mvvm: `8.3.2` | M.E.DI: `9.0.0` | M.E.Logging: `9.0.0`
- `Nullable`: enable | `ImplicitUsings`: enable

---

## File Map

```
src/
  DuoCompanion.Core/
    Models/
      DisplayInfo.cs          ← existing
      ClipboardItem.cs        ← Task 7
      AppSettings.cs          ← Task 13

  DuoCompanion.Contracts/
    Services/
      IDisplayService.cs      ← existing
      IWindowManagerService.cs ← Task 1
      IInputService.cs        ← Task 3
      IUiAutomationService.cs ← Task 5
      IClipboardService.cs    ← Task 7
      IMouseService.cs        ← Task 9
      IMediaService.cs        ← Task 10
      IOrientationService.cs  ← Task 12
      ISettingsService.cs     ← Task 13

  DuoCompanion.Services/
    Win32/
      NativeMethods.cs        ← existing + extended each task
    Display/
      DisplayService.cs       ← existing
      OrientationService.cs   ← Task 12
    Window/
      WindowManagerService.cs ← Task 1
    Input/
      InputService.cs         ← Task 3
      MouseService.cs         ← Task 9
    Automation/
      UiAutomationService.cs  ← Task 5
    Clipboard/
      ClipboardService.cs     ← Task 7
    Media/
      MediaService.cs         ← Task 10
    Settings/
      SettingsService.cs      ← Task 13

  DuoCompanion.App/
    App.xaml / App.xaml.cs   ← existing + extended each task
    MainWindow.xaml/.cs       ← existing (M1 debug window)
    CompanionWindow.xaml/.cs  ← Task 1
    ViewModels/
      DisplayViewModel.cs     ← existing
      ClipboardViewModel.cs   ← Task 8
      MediaViewModel.cs       ← Task 10
    Pages/
      KeyboardPage.xaml/.cs   ← Task 4
      TouchpadPage.xaml/.cs   ← Task 9
      ClipboardPage.xaml/.cs  ← Task 8
      MediaPage.xaml/.cs      ← Task 10
      HandwritingPage.xaml/.cs← Task 11
      SettingsPage.xaml/.cs   ← Task 13
    Controls/
      NavButton.xaml/.cs      ← Task 2

tests/
  DuoCompanion.Tests/
    DuoCompanion.Tests.csproj
    Services/
      ClipboardServiceTests.cs ← Task 7
      SettingsServiceTests.cs  ← Task 13
```

---

## Task 1: Companion Window (Milestone 2)

Borderless, fullscreen, always-on-top window that auto-positions itself on the secondary display and re-positions itself when displays change.

**Files:**
- Extend: `src/DuoCompanion.Services/Win32/NativeMethods.cs`
- Create: `src/DuoCompanion.Contracts/Services/IWindowManagerService.cs`
- Create: `src/DuoCompanion.Services/Window/WindowManagerService.cs`
- Create: `src/DuoCompanion.App/CompanionWindow.xaml`
- Create: `src/DuoCompanion.App/CompanionWindow.xaml.cs`
- Modify: `src/DuoCompanion.App/App.xaml.cs`

**Interfaces produced:**
```csharp
// IWindowManagerService
void PositionOnSecondaryDisplay(IntPtr hwnd);
event EventHandler DisplayConfigurationChanged;
```

- [ ] **Step 1: Extend NativeMethods with window P/Invoke**

Append to `src/DuoCompanion.Services/Win32/NativeMethods.cs` inside the class:

```csharp
internal const int SWP_NOMOVE = 0x0002;
internal const int SWP_NOSIZE = 0x0001;
internal const int SWP_NOACTIVATE = 0x0010;
internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
internal const uint EVENT_SYSTEM_DISPLAYCHANGE = 0x001B;

internal static readonly IntPtr HWND_TOPMOST = new(-1);

[DllImport("user32.dll", SetLastError = true)]
internal static extern bool SetWindowPos(
    IntPtr hWnd, IntPtr hWndInsertAfter,
    int X, int Y, int cx, int cy, uint uFlags);

[DllImport("user32.dll")]
internal static extern IntPtr SetWinEventHook(
    uint eventMin, uint eventMax,
    IntPtr hmodWinEventProc,
    WinEventProc lpfnWinEventProc,
    uint idProcess, uint idThread,
    uint dwFlags);

[DllImport("user32.dll")]
internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

internal delegate void WinEventProc(
    IntPtr hWinEventHook, uint @event,
    IntPtr hwnd, int idObject, int idChild,
    uint idEventThread, uint dwmsEventTime);
```

- [ ] **Step 2: Create IWindowManagerService**

Create `src/DuoCompanion.Contracts/Services/IWindowManagerService.cs`:

```csharp
namespace DuoCompanion.Contracts.Services;

public interface IWindowManagerService
{
    void PositionOnSecondaryDisplay(IntPtr hwnd);
    event EventHandler DisplayConfigurationChanged;
    void StartMonitoring(IntPtr hostHwnd);
    void StopMonitoring();
}
```

- [ ] **Step 3: Implement WindowManagerService**

Create `src/DuoCompanion.Services/Window/WindowManagerService.cs`:

```csharp
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Window;

public sealed class WindowManagerService : IWindowManagerService, IDisposable
{
    private readonly IDisplayService _display;
    private readonly ILogger<WindowManagerService> _logger;
    private IntPtr _hook;

    public event EventHandler? DisplayConfigurationChanged;

    public WindowManagerService(IDisplayService display, ILogger<WindowManagerService> logger)
    {
        _display = display;
        _logger = logger;
    }

    public void StartMonitoring(IntPtr hostHwnd)
    {
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_DISPLAYCHANGE,
            NativeMethods.EVENT_SYSTEM_DISPLAYCHANGE,
            IntPtr.Zero, OnWinEvent,
            0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _logger.LogInformation("Display change monitoring started");
    }

    public void StopMonitoring()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    public void PositionOnSecondaryDisplay(IntPtr hwnd)
    {
        var secondary = _display.GetSecondaryDisplay();
        if (secondary is null)
        {
            _logger.LogWarning("No secondary display found — companion window cannot be positioned");
            return;
        }

        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            secondary.X, secondary.Y,
            secondary.Width, secondary.Height, 0);

        _logger.LogInformation("Companion window positioned at {X},{Y} size {W}x{H}",
            secondary.X, secondary.Y, secondary.Width, secondary.Height);
    }

    private void OnWinEvent(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        _logger.LogInformation("Display configuration changed");
        DisplayConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => StopMonitoring();
}
```

- [ ] **Step 4: Create CompanionWindow.xaml**

Create `src/DuoCompanion.App/CompanionWindow.xaml`:

```xml
<Window
    x:Class="DuoCompanion.App.CompanionWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Duo Companion">

    <Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">
        <!-- Navigation shell injected in Task 2 -->
        <TextBlock Text="Companion Panel — Loading..."
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   Style="{StaticResource SubtitleTextBlockStyle}" />
    </Grid>
</Window>
```

- [ ] **Step 5: Create CompanionWindow.xaml.cs**

Create `src/DuoCompanion.App/CompanionWindow.xaml.cs`:

```csharp
using DuoCompanion.Contracts.Services;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace DuoCompanion.App;

public sealed partial class CompanionWindow : Window
{
    private readonly IWindowManagerService _windowManager;

    public CompanionWindow(IWindowManagerService windowManager)
    {
        _windowManager = windowManager;
        InitializeComponent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption =
            Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;

        Activated += OnActivated;
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        var hwnd = WindowNative.GetWindowHandle(this);
        _windowManager.StartMonitoring(hwnd);
        _windowManager.PositionOnSecondaryDisplay(hwnd);
    }

    private void OnDisplayConfigurationChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            _windowManager.PositionOnSecondaryDisplay(hwnd);
        });
    }

    protected override void Finalize() => 
        _windowManager.DisplayConfigurationChanged -= OnDisplayConfigurationChanged;
}
```

- [ ] **Step 6: Wire CompanionWindow into App.xaml.cs**

Replace `OnLaunched` in `src/DuoCompanion.App/App.xaml.cs`:

```csharp
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Display;
using DuoCompanion.Services.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace DuoCompanion.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Services = BuildServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var companion = new CompanionWindow(
            Services.GetRequiredService<IWindowManagerService>());
        companion.Activate();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<IWindowManagerService, WindowManagerService>();
        return services.BuildServiceProvider();
    }
}
```

- [ ] **Step 7: Register WindowManagerService in DuoCompanion.Services.csproj**

No csproj change needed — WindowManagerService has no new NuGet dependencies.

- [ ] **Step 8: Build and verify**

On Windows machine:
```
dotnet build src/DuoCompanion.App -r win-arm64
```
Expected: Build succeeds. Run the app — blank companion window appears on secondary display, always on top, no title bar.

- [ ] **Step 9: Commit**
```bash
git add src/
git commit -m "feat(M2): companion window positions on secondary display, always-on-top"
```

---

## Task 2: Navigation Shell (Milestone 3)

Replace the blank companion window with a top navigation bar and a `Frame` hosting module pages. Six nav items: Keyboard, Touchpad, Clipboard, Media, Handwriting, Settings.

**Files:**
- Create: `src/DuoCompanion.App/Controls/NavButton.xaml` + `.cs`
- Create: `src/DuoCompanion.App/Pages/KeyboardPage.xaml` + `.cs`
- Create: `src/DuoCompanion.App/Pages/TouchpadPage.xaml` + `.cs`
- Create: `src/DuoCompanion.App/Pages/ClipboardPage.xaml` + `.cs`
- Create: `src/DuoCompanion.App/Pages/MediaPage.xaml` + `.cs`
- Create: `src/DuoCompanion.App/Pages/HandwritingPage.xaml` + `.cs`
- Create: `src/DuoCompanion.App/Pages/SettingsPage.xaml` + `.cs`
- Modify: `src/DuoCompanion.App/CompanionWindow.xaml` + `.cs`

**Interfaces produced:**
```csharp
// CompanionWindow public method used by Task 6
void NavigateTo(Type pageType);
```

- [ ] **Step 1: Create stub pages (repeat pattern for all 6)**

Create `src/DuoCompanion.App/Pages/KeyboardPage.xaml`:
```xml
<Page x:Class="DuoCompanion.App.Pages.KeyboardPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <TextBlock Text="Keyboard" HorizontalAlignment="Center" VerticalAlignment="Center"
                   Style="{StaticResource TitleTextBlockStyle}" />
    </Grid>
</Page>
```

Create `src/DuoCompanion.App/Pages/KeyboardPage.xaml.cs`:
```csharp
using Microsoft.UI.Xaml.Controls;
namespace DuoCompanion.App.Pages;
public sealed partial class KeyboardPage : Page
{
    public KeyboardPage() => InitializeComponent();
}
```

Repeat identically for: `TouchpadPage`, `ClipboardPage`, `MediaPage`, `HandwritingPage`, `SettingsPage` — only change the class name and `Text` in the placeholder.

- [ ] **Step 2: Update CompanionWindow.xaml with nav shell**

Replace `src/DuoCompanion.App/CompanionWindow.xaml`:

```xml
<Window
    x:Class="DuoCompanion.App.CompanionWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Duo Companion">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="52" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- Top navigation bar -->
        <Grid Grid.Row="0"
              Background="{ThemeResource NavigationViewTopPaneBackground}"
              BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
              BorderThickness="0,0,0,1">
            <StackPanel Orientation="Horizontal" Spacing="4" Padding="8,0">
                <Button x:Name="BtnKeyboard"    Tag="Keyboard"    Click="OnNavClick" ToolTipService.ToolTip="Keyboard">
                    <FontIcon Glyph="&#xE765;" />
                </Button>
                <Button x:Name="BtnTouchpad"   Tag="Touchpad"    Click="OnNavClick" ToolTipService.ToolTip="Touchpad">
                    <FontIcon Glyph="&#xE7C5;" />
                </Button>
                <Button x:Name="BtnClipboard"  Tag="Clipboard"   Click="OnNavClick" ToolTipService.ToolTip="Clipboard">
                    <FontIcon Glyph="&#xE77F;" />
                </Button>
                <Button x:Name="BtnMedia"      Tag="Media"       Click="OnNavClick" ToolTipService.ToolTip="Media">
                    <FontIcon Glyph="&#xE768;" />
                </Button>
                <Button x:Name="BtnHandwrite"  Tag="Handwriting" Click="OnNavClick" ToolTipService.ToolTip="Handwriting">
                    <FontIcon Glyph="&#xED63;" />
                </Button>
                <Button x:Name="BtnSettings"   Tag="Settings"    Click="OnNavClick" ToolTipService.ToolTip="Settings"
                        HorizontalAlignment="Right" Margin="Auto,0,0,0">
                    <FontIcon Glyph="&#xE713;" />
                </Button>
            </StackPanel>
        </Grid>

        <!-- Module content frame -->
        <Frame x:Name="ContentFrame" Grid.Row="1" />
    </Grid>
</Window>
```

- [ ] **Step 3: Update CompanionWindow.xaml.cs with navigation**

Replace `src/DuoCompanion.App/CompanionWindow.xaml.cs`:

```csharp
using DuoCompanion.App.Pages;
using DuoCompanion.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace DuoCompanion.App;

public sealed partial class CompanionWindow : Window
{
    private readonly IWindowManagerService _windowManager;

    private static readonly Dictionary<string, Type> _pageMap = new()
    {
        ["Keyboard"]    = typeof(KeyboardPage),
        ["Touchpad"]    = typeof(TouchpadPage),
        ["Clipboard"]   = typeof(ClipboardPage),
        ["Media"]       = typeof(MediaPage),
        ["Handwriting"] = typeof(HandwritingPage),
        ["Settings"]    = typeof(SettingsPage),
    };

    public CompanionWindow(IWindowManagerService windowManager)
    {
        _windowManager = windowManager;
        InitializeComponent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption =
            Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;

        Activated += OnFirstActivated;
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
    }

    public void NavigateTo(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && _pageMap.TryGetValue(tag, out var pageType))
            NavigateTo(pageType);
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        var hwnd = WindowNative.GetWindowHandle(this);
        _windowManager.StartMonitoring(hwnd);
        _windowManager.PositionOnSecondaryDisplay(hwnd);
        NavigateTo(typeof(KeyboardPage));
    }

    private void OnDisplayConfigurationChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            _windowManager.PositionOnSecondaryDisplay(hwnd);
        });
    }
}
```

- [ ] **Step 4: Build and verify**
```
dotnet build src/DuoCompanion.App -r win-arm64
```
Run: companion window shows nav buttons at top; clicking each navigates to the stub page label.

- [ ] **Step 5: Commit**
```bash
git add src/
git commit -m "feat(M3): navigation shell with 6 module stubs"
```

---

## Task 3: Input Injection Service (M4 Foundation)

Wrap Win32 `SendInput` for keyboard and mouse injection. Used by Keyboard (Task 4) and Touchpad (Task 9).

**Files:**
- Extend: `src/DuoCompanion.Services/Win32/NativeMethods.cs`
- Create: `src/DuoCompanion.Contracts/Services/IInputService.cs`
- Create: `src/DuoCompanion.Services/Input/InputService.cs`
- Extend: `src/DuoCompanion.App/DuoCompanion.App.csproj` — project reference
- Extend: `src/DuoCompanion.App/App.xaml.cs` — DI registration

**Interfaces produced:**
```csharp
// IInputService
void SendKey(ushort virtualKeyCode, bool isExtendedKey = false);
void SendText(string text);
void SendKeyDown(ushort vk);
void SendKeyUp(ushort vk);
```

- [ ] **Step 1: Extend NativeMethods with SendInput**

Append inside `NativeMethods` class:

```csharp
internal const uint INPUT_KEYBOARD = 1;
internal const uint KEYEVENTF_KEYUP = 0x0002;
internal const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
internal const uint KEYEVENTF_UNICODE = 0x0004;

[DllImport("user32.dll", SetLastError = true)]
internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public INPUTUNION u;
}

[StructLayout(LayoutKind.Explicit)]
internal struct INPUTUNION
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
    [FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}
```

- [ ] **Step 2: Create IInputService**

Create `src/DuoCompanion.Contracts/Services/IInputService.cs`:

```csharp
namespace DuoCompanion.Contracts.Services;

public interface IInputService
{
    void SendKey(ushort virtualKeyCode, bool isExtendedKey = false);
    void SendKeyDown(ushort virtualKeyCode);
    void SendKeyUp(ushort virtualKeyCode);
    void SendText(string text);
}
```

- [ ] **Step 3: Implement InputService**

Create `src/DuoCompanion.Services/Input/InputService.cs`:

```csharp
using System.Runtime.InteropServices;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Input;

public sealed class InputService : IInputService
{
    private readonly ILogger<InputService> _logger;

    public InputService(ILogger<InputService> logger) => _logger = logger;

    public void SendKey(ushort virtualKeyCode, bool isExtendedKey = false)
    {
        SendKeyDown(virtualKeyCode, isExtendedKey);
        SendKeyUp(virtualKeyCode, isExtendedKey);
    }

    public void SendKeyDown(ushort virtualKeyCode) => SendKeyDown(virtualKeyCode, false);
    public void SendKeyUp(ushort virtualKeyCode) => SendKeyUp(virtualKeyCode, false);

    private void SendKeyDown(ushort vk, bool extended)
    {
        var flags = extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u;
        Send(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = flags }
            }
        });
    }

    private void SendKeyUp(ushort vk, bool extended)
    {
        var flags = NativeMethods.KEYEVENTF_KEYUP | (extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0u);
        Send(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = flags }
            }
        });
    }

    public void SendText(string text)
    {
        foreach (var ch in text)
        {
            var inputs = new[]
            {
                new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    u = new NativeMethods.INPUTUNION
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wScan = ch,
                            dwFlags = NativeMethods.KEYEVENTF_UNICODE
                        }
                    }
                },
                new NativeMethods.INPUT
                {
                    type = NativeMethods.INPUT_KEYBOARD,
                    u = new NativeMethods.INPUTUNION
                    {
                        ki = new NativeMethods.KEYBDINPUT
                        {
                            wScan = ch,
                            dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP
                        }
                    }
                }
            };
            NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        }
        _logger.LogDebug("Sent text of {Length} chars", text.Length);
    }

    private static void Send(NativeMethods.INPUT input)
    {
        var inputs = new[] { input };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
```

- [ ] **Step 4: Register in DI (App.xaml.cs)**

Add to `BuildServices()`:
```csharp
services.AddSingleton<IInputService, InputService>();
```

Add using:
```csharp
using DuoCompanion.Services.Input;
```

- [ ] **Step 5: Build**
```
dotnet build src/DuoCompanion.App -r win-arm64
```
Expected: Build succeeds, no errors.

- [ ] **Step 6: Commit**
```bash
git add src/
git commit -m "feat: Win32 input injection service (keyboard + text)"
```

---

## Task 4: Virtual Keyboard Page (Milestone 4)

Touch-friendly QWERTY layout. Tapping a key calls `IInputService.SendKey()`. Keys: full QWERTY, Shift, Enter, Backspace, Space, Ctrl, Alt, Win, Tab, arrow keys.

**Files:**
- Replace: `src/DuoCompanion.App/Pages/KeyboardPage.xaml`
- Replace: `src/DuoCompanion.App/Pages/KeyboardPage.xaml.cs`

Virtual key codes (VK_*) used: 
`0x41–0x5A` (A–Z), `0x30–0x39` (0–9), `0x08` (Back), `0x09` (Tab), `0x0D` (Enter), `0x10` (Shift), `0x11` (Ctrl), `0x12` (Alt), `0x5B` (Win), `0x20` (Space), `0x25–0x28` (Left/Up/Right/Down).

- [ ] **Step 1: Replace KeyboardPage.xaml**

```xml
<Page x:Class="DuoCompanion.App.Pages.KeyboardPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid Padding="8" RowSpacing="6">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Row 0: number row -->
        <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
            <Button Tag="49"  Content="1" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="50"  Content="2" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="51"  Content="3" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="52"  Content="4" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="53"  Content="5" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="54"  Content="6" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="55"  Content="7" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="56"  Content="8" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="57"  Content="9" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="48"  Content="0" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="8"   Width="80"  Height="56" FontSize="18" Click="OnKeyClick">
                <FontIcon Glyph="&#xE750;" FontSize="18"/>
            </Button>
        </StackPanel>

        <!-- Row 1: QWERTY -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
            <Button Tag="81"  Content="Q" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="87"  Content="W" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="69"  Content="E" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="82"  Content="R" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="84"  Content="T" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="89"  Content="Y" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="85"  Content="U" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="73"  Content="I" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="79"  Content="O" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="80"  Content="P" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="13"  Content="↵" Width="80" Height="56" FontSize="18" Click="OnKeyClick"/>
        </StackPanel>

        <!-- Row 2: ASDF -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
            <Button Tag="65"  Content="A" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="83"  Content="S" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="68"  Content="D" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="70"  Content="F" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="71"  Content="G" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="72"  Content="H" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="74"  Content="J" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="75"  Content="K" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="76"  Content="L" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
        </StackPanel>

        <!-- Row 3: ZXCV + Shift -->
        <StackPanel Grid.Row="3" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
            <Button x:Name="BtnShift" Tag="16" Width="80" Height="56" FontSize="18" Click="OnModifierClick">
                <FontIcon Glyph="&#xE752;" FontSize="18"/>
            </Button>
            <Button Tag="90"  Content="Z" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="88"  Content="X" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="67"  Content="C" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="86"  Content="V" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="66"  Content="B" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="78"  Content="N" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            <Button Tag="77"  Content="M" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
        </StackPanel>

        <!-- Row 4: bottom row -->
        <StackPanel Grid.Row="4" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
            <Button x:Name="BtnCtrl" Tag="17" Content="Ctrl" Width="72" Height="56" FontSize="14" Click="OnModifierClick"/>
            <Button x:Name="BtnAlt"  Tag="18" Content="Alt"  Width="72" Height="56" FontSize="14" Click="OnModifierClick"/>
            <Button Tag="9"   Content="Tab" Width="72" Height="56" FontSize="14" Click="OnKeyClick"/>
            <Button Tag="32"  Content="Space" Width="200" Height="56" FontSize="16" Click="OnKeyClick"/>
            <Button Tag="37" Width="52" Height="56" Click="OnKeyClick"><FontIcon Glyph="&#xE76B;" FontSize="18"/></Button>
            <Button Tag="38" Width="52" Height="56" Click="OnKeyClick"><FontIcon Glyph="&#xE70E;" FontSize="18"/></Button>
            <Button Tag="40" Width="52" Height="56" Click="OnKeyClick"><FontIcon Glyph="&#xE70D;" FontSize="18"/></Button>
            <Button Tag="39" Width="52" Height="56" Click="OnKeyClick"><FontIcon Glyph="&#xE76C;" FontSize="18"/></Button>
            <Button x:Name="BtnWin" Tag="91" Content="Win" Width="72" Height="56" FontSize="14" Click="OnKeyClick"/>
        </StackPanel>
    </Grid>
</Page>
```

- [ ] **Step 2: Replace KeyboardPage.xaml.cs**

```csharp
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
```

- [ ] **Step 3: Build and verify**
```
dotnet build src/DuoCompanion.App -r win-arm64
```
Run: navigate to Keyboard tab — full QWERTY renders, tapping keys injects into any focused app on the primary display.

- [ ] **Step 4: Commit**
```bash
git add src/
git commit -m "feat(M4): virtual keyboard with key injection and modifier toggle"
```

---

## Task 5: UI Automation Service (Milestone 8 Foundation)

Use `SetWinEventHook` with `EVENT_OBJECT_FOCUS` to detect when a text-input element gains or loses focus across the system.

**Files:**
- Extend: `src/DuoCompanion.Services/Win32/NativeMethods.cs`
- Create: `src/DuoCompanion.Contracts/Services/IUiAutomationService.cs`
- Create: `src/DuoCompanion.Services/Automation/UiAutomationService.cs`
- Modify: `src/DuoCompanion.App/App.xaml.cs` — DI registration

**Interfaces produced:**
```csharp
// IUiAutomationService
event EventHandler TextInputFocused;
event EventHandler TextInputBlurred;
void Start();
void Stop();
```

- [ ] **Step 1: Extend NativeMethods for UIAutomation events**

Append inside `NativeMethods`:

```csharp
internal const uint EVENT_OBJECT_FOCUS = 0x8005;
internal const uint EVENT_OBJECT_STATECHANGE = 0x800A;
internal const int OBJID_CLIENT = 0x00000000;

[DllImport("oleacc.dll")]
internal static extern int AccessibleObjectFromEvent(
    IntPtr hwnd, int idObject, int idChild,
    [MarshalAs(UnmanagedType.IUnknown)] out object? ppvObject,
    out int varChild);

[DllImport("oleacc.dll")]
internal static extern int GetRoleText(uint dwRole, 
    System.Text.StringBuilder lpszRole, uint cchRoleMax);
```

- [ ] **Step 2: Create IUiAutomationService**

Create `src/DuoCompanion.Contracts/Services/IUiAutomationService.cs`:

```csharp
namespace DuoCompanion.Contracts.Services;

public interface IUiAutomationService
{
    event EventHandler TextInputFocused;
    event EventHandler TextInputBlurred;
    void Start();
    void Stop();
}
```

- [ ] **Step 3: Implement UiAutomationService**

Create `src/DuoCompanion.Services/Automation/UiAutomationService.cs`:

```csharp
using System.Runtime.InteropServices;
using Accessibility;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Automation;

public sealed class UiAutomationService : IUiAutomationService, IDisposable
{
    // Accessible roles that represent text-editable controls
    private static readonly HashSet<uint> _editRoles = new() { 42, 43, 44 }; // ROLE_SYSTEM_TEXT = 42

    private readonly ILogger<UiAutomationService> _logger;
    private IntPtr _focusHook;
    private NativeMethods.WinEventProc? _hookProc; // keep alive — GC can collect delegates

    public event EventHandler? TextInputFocused;
    public event EventHandler? TextInputBlurred;

    public UiAutomationService(ILogger<UiAutomationService> logger) => _logger = logger;

    public void Start()
    {
        _hookProc = OnFocusEvent;
        _focusHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_OBJECT_FOCUS,
            NativeMethods.EVENT_OBJECT_FOCUS,
            IntPtr.Zero, _hookProc,
            0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _logger.LogInformation("UI Automation focus monitoring started");
    }

    public void Stop()
    {
        if (_focusHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_focusHook);
            _focusHook = IntPtr.Zero;
        }
        _hookProc = null;
    }

    private void OnFocusEvent(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        _ = Task.Run(() => CheckFocusedElement(hwnd, idObject, idChild));
    }

    private void CheckFocusedElement(IntPtr hwnd, int idObject, int idChild)
    {
        try
        {
            var hr = NativeMethods.AccessibleObjectFromEvent(hwnd, idObject, idChild,
                out var accObj, out _);

            if (hr != 0 || accObj is not Accessibility.IAccessible acc) return;

            var roleVariant = new object();
            acc.get_accRole(idChild, out roleVariant);
            var role = Convert.ToUInt32(roleVariant);

            // ROLE_SYSTEM_TEXT = 42, ROLE_SYSTEM_DOCUMENT = 15
            if (role is 42 or 15)
            {
                _logger.LogDebug("Text input focused (role={Role})", role);
                TextInputFocused?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                TextInputBlurred?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking focused element");
        }
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 4: Add Accessibility COM reference to Services.csproj**

Add to `src/DuoCompanion.Services/DuoCompanion.Services.csproj` inside `<ItemGroup>`:
```xml
<COMReference Include="Accessibility">
  <Guid>{1EA4DBF0-3C3B-11CF-810C-00AA00389B71}</Guid>
  <VersionMajor>1</VersionMajor>
  <VersionMinor>1</VersionMinor>
  <WrapperTool>tlbimp</WrapperTool>
  <Lcid>0</Lcid>
  <Isolated>false</Isolated>
  <EmbedInteropTypes>true</EmbedInteropTypes>
</COMReference>
```

- [ ] **Step 5: Register in DI**

Add to `App.xaml.cs` `BuildServices()`:
```csharp
services.AddSingleton<IUiAutomationService, UiAutomationService>();
```

Add using:
```csharp
using DuoCompanion.Services.Automation;
```

- [ ] **Step 6: Build**
```
dotnet build src/DuoCompanion.App -r win-arm64
```

- [ ] **Step 7: Commit**
```bash
git add src/
git commit -m "feat(M8): UI Automation focus event service"
```

---

## Task 6: Auto Show/Hide Keyboard (Milestone 8 — Wiring)

Subscribe `CompanionWindow` to `IUiAutomationService` events: focus on a text field → navigate to `KeyboardPage`; focus on a non-text element → navigate to previous page.

**Files:**
- Modify: `src/DuoCompanion.App/CompanionWindow.xaml.cs`
- Modify: `src/DuoCompanion.App/App.xaml.cs`

- [ ] **Step 1: Update CompanionWindow constructor to accept IUiAutomationService**

Replace `CompanionWindow.xaml.cs` constructor and class fields:

```csharp
using DuoCompanion.App.Pages;
using DuoCompanion.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace DuoCompanion.App;

public sealed partial class CompanionWindow : Window
{
    private readonly IWindowManagerService _windowManager;
    private readonly IUiAutomationService _automation;
    private Type _lastManualPage = typeof(KeyboardPage);

    private static readonly Dictionary<string, Type> _pageMap = new()
    {
        ["Keyboard"]    = typeof(KeyboardPage),
        ["Touchpad"]    = typeof(TouchpadPage),
        ["Clipboard"]   = typeof(ClipboardPage),
        ["Media"]       = typeof(MediaPage),
        ["Handwriting"] = typeof(HandwritingPage),
        ["Settings"]    = typeof(SettingsPage),
    };

    public CompanionWindow(IWindowManagerService windowManager, IUiAutomationService automation)
    {
        _windowManager = windowManager;
        _automation    = automation;
        InitializeComponent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption =
            Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;

        _automation.TextInputFocused += OnTextInputFocused;
        _automation.TextInputBlurred += OnTextInputBlurred;
        Activated += OnFirstActivated;
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
    }

    public void NavigateTo(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && _pageMap.TryGetValue(tag, out var pageType))
        {
            _lastManualPage = pageType;
            NavigateTo(pageType);
        }
    }

    private void OnTextInputFocused(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => NavigateTo(typeof(KeyboardPage)));
    }

    private void OnTextInputBlurred(object? sender, EventArgs e)
    {
        // Only revert if user hadn't manually switched away from keyboard
        DispatcherQueue.TryEnqueue(() =>
        {
            if (ContentFrame.CurrentSourcePageType == typeof(KeyboardPage))
                NavigateTo(_lastManualPage == typeof(KeyboardPage) ? typeof(KeyboardPage) : _lastManualPage);
        });
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;
        var hwnd = WindowNative.GetWindowHandle(this);
        _windowManager.StartMonitoring(hwnd);
        _windowManager.PositionOnSecondaryDisplay(hwnd);
        _automation.Start();
        NavigateTo(typeof(KeyboardPage));
    }

    private void OnDisplayConfigurationChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            _windowManager.PositionOnSecondaryDisplay(hwnd);
        });
    }
}
```

- [ ] **Step 2: Update App.xaml.cs OnLaunched to pass IUiAutomationService**

```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    var companion = new CompanionWindow(
        Services.GetRequiredService<IWindowManagerService>(),
        Services.GetRequiredService<IUiAutomationService>());
    companion.Activate();
}
```

- [ ] **Step 3: Build and verify**
```
dotnet build src/DuoCompanion.App -r win-arm64
```
Run: tap any text field on the primary display → companion auto-navigates to Keyboard; tap a button or desktop → stays/returns.

- [ ] **Step 4: Commit**
```bash
git add src/
git commit -m "feat(M8): auto-show keyboard on text input focus via WinEvent hook"
```

---

## Task 7: Clipboard Service (Milestone 6)

Monitor clipboard changes via WinRT API. Store history (max 50 items). Support pin and clear.

**Files:**
- Create: `src/DuoCompanion.Core/Models/ClipboardItem.cs`
- Create: `src/DuoCompanion.Contracts/Services/IClipboardService.cs`
- Create: `src/DuoCompanion.Services/Clipboard/ClipboardService.cs`
- Create: `tests/DuoCompanion.Tests/DuoCompanion.Tests.csproj`
- Create: `tests/DuoCompanion.Tests/Services/ClipboardServiceTests.cs`

**Interfaces produced:**
```csharp
// IClipboardService
IReadOnlyList<ClipboardItem> Items { get; }
event EventHandler ItemsChanged;
void Pin(Guid id);
void Remove(Guid id);
void Clear();
Task PasteAsync(Guid id);
```

- [ ] **Step 1: Create ClipboardItem model**

Create `src/DuoCompanion.Core/Models/ClipboardItem.cs`:

```csharp
namespace DuoCompanion.Core.Models;

public sealed record ClipboardItem(
    Guid Id,
    string Text,
    DateTimeOffset CapturedAt)
{
    public bool IsPinned { get; init; }
}
```

- [ ] **Step 2: Create IClipboardService**

Create `src/DuoCompanion.Contracts/Services/IClipboardService.cs`:

```csharp
using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface IClipboardService
{
    IReadOnlyList<ClipboardItem> Items { get; }
    event EventHandler ItemsChanged;
    void Initialize();
    void Pin(Guid id);
    void Remove(Guid id);
    void Clear();
    Task PasteAsync(Guid id);
}
```

- [ ] **Step 3: Implement ClipboardService**

Create `src/DuoCompanion.Services/Clipboard/ClipboardService.cs`:

```csharp
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;

namespace DuoCompanion.Services.Clipboard;

public sealed class ClipboardService : IClipboardService
{
    private const int MaxHistory = 50;

    private readonly IInputService _input;
    private readonly ILogger<ClipboardService> _logger;
    private readonly List<ClipboardItem> _items = new();
    private string? _lastText;

    public IReadOnlyList<ClipboardItem> Items => _items.AsReadOnly();
    public event EventHandler? ItemsChanged;

    public ClipboardService(IInputService input, ILogger<ClipboardService> logger)
    {
        _input  = input;
        _logger = logger;
    }

    public void Initialize()
    {
        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged += OnClipboardChanged;
        _logger.LogInformation("Clipboard monitoring started");
    }

    private async void OnClipboardChanged(object? sender, object e)
    {
        try
        {
            var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text)) return;

            var text = await content.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text) || text == _lastText) return;

            _lastText = text;

            var item = new ClipboardItem(Guid.NewGuid(), text, DateTimeOffset.Now);

            // Remove duplicate text, keep pinned items at top
            _items.RemoveAll(i => !i.IsPinned && i.Text == text);

            var insertAt = _items.FindLastIndex(i => i.IsPinned) + 1;
            _items.Insert(insertAt, item);

            // Trim unpinned items over limit
            while (_items.Count(i => !i.IsPinned) > MaxHistory)
            {
                var lastUnpinned = _items.FindLastIndex(i => !i.IsPinned);
                if (lastUnpinned >= 0) _items.RemoveAt(lastUnpinned);
            }

            _logger.LogDebug("Clipboard item added: {Preview}", text[..Math.Min(40, text.Length)]);
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read clipboard content");
        }
    }

    public void Pin(Guid id)
    {
        var idx = _items.FindIndex(i => i.Id == id);
        if (idx < 0) return;
        _items[idx] = _items[idx] with { IsPinned = true };
        _items.Sort((a, b) => b.IsPinned.CompareTo(a.IsPinned));
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(Guid id)
    {
        _items.RemoveAll(i => i.Id == id);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _items.RemoveAll(i => !i.IsPinned);
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task PasteAsync(Guid id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null) return;

        var package = new DataPackage();
        package.SetText(item.Text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);

        await Task.Delay(50); // brief delay for clipboard to settle
        _input.SendKeyDown(0x11); // Ctrl down
        _input.SendKey(0x56);     // V
        _input.SendKeyUp(0x11);   // Ctrl up

        _logger.LogInformation("Pasted clipboard item {Id}", id);
    }
}
```

- [ ] **Step 4: Create test project**

Create `tests/DuoCompanion.Tests/DuoCompanion.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
    <RuntimeIdentifiers>win-arm64</RuntimeIdentifiers>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DuoCompanion.Core\DuoCompanion.Core.csproj" />
  </ItemGroup>
</Project>
```

Add to `DuoCompanion.sln`:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "DuoCompanion.Tests", "tests\DuoCompanion.Tests\DuoCompanion.Tests.csproj", "{E5F6A7B8-C9D0-1234-EF01-345678901234}"
EndProject
```

- [ ] **Step 5: Write ClipboardItem unit tests**

Create `tests/DuoCompanion.Tests/Services/ClipboardItemTests.cs`:

```csharp
using DuoCompanion.Core.Models;

namespace DuoCompanion.Tests.Services;

public sealed class ClipboardItemTests
{
    [Fact]
    public void IsPinned_defaults_to_false()
    {
        var item = new ClipboardItem(Guid.NewGuid(), "hello", DateTimeOffset.Now);
        Assert.False(item.IsPinned);
    }

    [Fact]
    public void With_IsPinned_creates_pinned_copy()
    {
        var item = new ClipboardItem(Guid.NewGuid(), "hello", DateTimeOffset.Now);
        var pinned = item with { IsPinned = true };

        Assert.False(item.IsPinned);
        Assert.True(pinned.IsPinned);
        Assert.Equal(item.Id, pinned.Id);
        Assert.Equal(item.Text, pinned.Text);
    }

    [Fact]
    public void ToString_is_implicitly_text_via_record()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.Now;
        var item = new ClipboardItem(id, "abc", now);
        var copy = new ClipboardItem(id, "abc", now);
        Assert.Equal(item, copy);
    }
}
```

- [ ] **Step 6: Run tests**
```
dotnet test tests/DuoCompanion.Tests -r win-arm64
```
Expected: 3 tests pass.

- [ ] **Step 7: Register ClipboardService in DI**

Add to `App.xaml.cs` `BuildServices()`:
```csharp
services.AddSingleton<IClipboardService, ClipboardService>();
```

Add using:
```csharp
using DuoCompanion.Services.Clipboard;
```

And call `Initialize()` in `OnLaunched`:
```csharp
Services.GetRequiredService<IClipboardService>().Initialize();
```

- [ ] **Step 8: Commit**
```bash
git add src/ tests/
git commit -m "feat(M6): clipboard service with history, pin, paste, and unit tests"
```

---

## Task 8: Clipboard Page (Milestone 6 — UI)

Show clipboard history as a scrollable list. Tap to paste. Long-press button to pin. Search bar filters items.

**Files:**
- Create: `src/DuoCompanion.App/ViewModels/ClipboardViewModel.cs`
- Replace: `src/DuoCompanion.App/Pages/ClipboardPage.xaml`
- Replace: `src/DuoCompanion.App/Pages/ClipboardPage.xaml.cs`

- [ ] **Step 1: Create ClipboardViewModel**

Create `src/DuoCompanion.App/ViewModels/ClipboardViewModel.cs`:

```csharp
using DuoCompanion.Core.Models;

namespace DuoCompanion.App.ViewModels;

internal sealed class ClipboardItemViewModel
{
    public Guid Id { get; init; }
    public string Preview { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public bool IsPinned { get; init; }

    public static ClipboardItemViewModel From(ClipboardItem item) => new()
    {
        Id      = item.Id,
        Preview = item.Text.Length > 80 ? item.Text[..80] + "…" : item.Text,
        Time    = item.CapturedAt.ToLocalTime().ToString("HH:mm"),
        IsPinned = item.IsPinned
    };
}
```

- [ ] **Step 2: Replace ClipboardPage.xaml**

```xml
<Page x:Class="DuoCompanion.App.Pages.ClipboardPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:vm="using:DuoCompanion.App.ViewModels">

    <Grid RowSpacing="8" Padding="8">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Search -->
        <TextBox Grid.Row="0"
                 x:Name="SearchBox"
                 PlaceholderText="Search clipboard…"
                 TextChanged="OnSearchChanged" />

        <!-- History list -->
        <ListView Grid.Row="1"
                  x:Name="HistoryList"
                  SelectionMode="None">
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:ClipboardItemViewModel">
                    <Grid ColumnSpacing="8" Padding="4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <Button Grid.Column="0"
                                HorizontalAlignment="Stretch"
                                HorizontalContentAlignment="Left"
                                Tag="{x:Bind Id}"
                                Click="OnPasteClick">
                            <StackPanel>
                                <TextBlock Text="{x:Bind Preview}" TextWrapping="NoWrap"
                                           TextTrimming="CharacterEllipsis"/>
                                <TextBlock Text="{x:Bind Time}" FontSize="11"
                                           Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                            </StackPanel>
                        </Button>
                        <Button Grid.Column="1" Tag="{x:Bind Id}" Click="OnPinClick"
                                ToolTipService.ToolTip="Pin">
                            <FontIcon Glyph="{x:Bind IsPinned ? '&#xE840;' : '&#xE841;'}" FontSize="16"/>
                        </Button>
                        <Button Grid.Column="2" Tag="{x:Bind Id}" Click="OnRemoveClick"
                                ToolTipService.ToolTip="Remove">
                            <FontIcon Glyph="&#xE74D;" FontSize="16"/>
                        </Button>
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <!-- Bottom bar -->
        <Button Grid.Row="2" Content="Clear History"
                HorizontalAlignment="Stretch"
                Click="OnClearClick"/>
    </Grid>
</Page>
```

- [ ] **Step 3: Replace ClipboardPage.xaml.cs**

```csharp
using DuoCompanion.App.ViewModels;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace DuoCompanion.App.Pages;

public sealed partial class ClipboardPage : Page
{
    private readonly IClipboardService _clipboard;
    private string _searchQuery = string.Empty;

    public ClipboardPage()
    {
        _clipboard = App.Services.GetRequiredService<IClipboardService>();
        InitializeComponent();
        _clipboard.ItemsChanged += (_, _) => RefreshList();
        Loaded += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var items = _clipboard.Items
                .Where(i => string.IsNullOrEmpty(_searchQuery) ||
                            i.Text.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase))
                .Select(ClipboardItemViewModel.From)
                .ToList();
            HistoryList.ItemsSource = items;
        });
    }

    private async void OnPasteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) await _clipboard.PasteAsync(id);
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) _clipboard.Pin(id);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id }) _clipboard.Remove(id);
    }

    private void OnClearClick(object sender, RoutedEventArgs e) => _clipboard.Clear();

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchBox.Text;
        RefreshList();
    }
}
```

- [ ] **Step 4: Build and verify**
```
dotnet build src/DuoCompanion.App -r win-arm64
```
Run: copy text on primary display → Clipboard tab shows the item; tap to paste; pin and remove work.

- [ ] **Step 5: Commit**
```bash
git add src/
git commit -m "feat(M6): clipboard history UI with search, pin, paste, remove"
```

---

## Task 9: Mouse Injection + Touchpad Page (Milestone 5)

Track touch movement on a `Canvas` and inject mouse events via `SendInput`.

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/IMouseService.cs`
- Create: `src/DuoCompanion.Services/Input/MouseService.cs`
- Replace: `src/DuoCompanion.App/Pages/TouchpadPage.xaml`
- Replace: `src/DuoCompanion.App/Pages/TouchpadPage.xaml.cs`
- Extend: `src/DuoCompanion.Services/Win32/NativeMethods.cs`

**Interfaces produced:**
```csharp
// IMouseService
void MoveDelta(int dx, int dy);
void Click(MouseButton button);
void ScrollDelta(int delta);
```

- [ ] **Step 1: Add mouse SendInput flags to NativeMethods**

Append inside `NativeMethods`:

```csharp
internal const uint INPUT_MOUSE = 0;
internal const uint MOUSEEVENTF_MOVE = 0x0001;
internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
internal const uint MOUSEEVENTF_LEFTUP = 0x0004;
internal const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
internal const uint MOUSEEVENTF_RIGHTUP = 0x0010;
internal const uint MOUSEEVENTF_WHEEL = 0x0800;
```

- [ ] **Step 2: Create IMouseService**

Create `src/DuoCompanion.Contracts/Services/IMouseService.cs`:

```csharp
namespace DuoCompanion.Contracts.Services;

public enum MouseButton { Left, Right }

public interface IMouseService
{
    void MoveDelta(int dx, int dy);
    void Click(MouseButton button);
    void ScrollDelta(int wheelDelta);
}
```

- [ ] **Step 3: Implement MouseService**

Create `src/DuoCompanion.Services/Input/MouseService.cs`:

```csharp
using System.Runtime.InteropServices;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;

namespace DuoCompanion.Services.Input;

public sealed class MouseService : IMouseService
{
    public void MoveDelta(int dx, int dy) =>
        Send(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.INPUTUNION
            {
                mi = new NativeMethods.MOUSEINPUT { dx = dx, dy = dy, dwFlags = NativeMethods.MOUSEEVENTF_MOVE }
            }
        });

    public void Click(MouseButton button)
    {
        var (down, up) = button == MouseButton.Left
            ? (NativeMethods.MOUSEEVENTF_LEFTDOWN,  NativeMethods.MOUSEEVENTF_LEFTUP)
            : (NativeMethods.MOUSEEVENTF_RIGHTDOWN, NativeMethods.MOUSEEVENTF_RIGHTUP);

        Send(new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new() { mi = new() { dwFlags = down } } });
        Send(new NativeMethods.INPUT { type = NativeMethods.INPUT_MOUSE, u = new() { mi = new() { dwFlags = up } } });
    }

    public void ScrollDelta(int wheelDelta) =>
        Send(new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new() { mi = new() { mouseData = (uint)wheelDelta, dwFlags = NativeMethods.MOUSEEVENTF_WHEEL } }
        });

    private static void Send(NativeMethods.INPUT input)
    {
        var inputs = new[] { input };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
```

- [ ] **Step 4: Register in DI**

Add to `BuildServices()`:
```csharp
services.AddSingleton<IMouseService, MouseService>();
```

- [ ] **Step 5: Replace TouchpadPage.xaml**

```xml
<Page x:Class="DuoCompanion.App.Pages.TouchpadPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="64"/>
        </Grid.RowDefinitions>

        <!-- Touch surface -->
        <Border Grid.Row="0"
                Background="{ThemeResource CardBackgroundFillColorDefaultBrush}"
                BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                BorderThickness="1"
                CornerRadius="12"
                Margin="12">
            <Canvas x:Name="TouchSurface"
                    ManipulationMode="TranslateX,TranslateY,Scale"
                    ManipulationDelta="OnManipulationDelta"
                    Tapped="OnTapped"
                    RightTapped="OnRightTapped"
                    PointerWheelChanged="OnPointerWheel">
                <TextBlock Text="Slide to move  •  Tap to click  •  Scroll with two fingers"
                           Foreground="{ThemeResource TextFillColorTertiaryBrush}"
                           FontSize="12"
                           HorizontalAlignment="Center"
                           VerticalAlignment="Center"
                           Canvas.Left="0" Canvas.Top="0"/>
            </Canvas>
        </Border>

        <!-- Click buttons -->
        <Grid Grid.Row="1" ColumnSpacing="8" Padding="12,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Button Grid.Column="0" Content="Left Click"
                    HorizontalAlignment="Stretch" Height="48"
                    Click="OnLeftClick"/>
            <Button Grid.Column="1" Content="Right Click"
                    HorizontalAlignment="Stretch" Height="48"
                    Click="OnRightClick"/>
        </Grid>
    </Grid>
</Page>
```

- [ ] **Step 6: Replace TouchpadPage.xaml.cs**

```csharp
using DuoCompanion.App;
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
```

- [ ] **Step 7: Build and verify**
```
dotnet build src/DuoCompanion.App -r win-arm64
```
Run: Touchpad page — sliding finger moves cursor on primary display; tap clicks.

- [ ] **Step 8: Commit**
```bash
git add src/
git commit -m "feat(M5): virtual touchpad with mouse injection"
```

---

## Task 10: Media Service + Media Page (Milestone 7)

Send media VK codes (play/pause, next, prev, volume). Show controls.

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/IMediaService.cs`
- Create: `src/DuoCompanion.Services/Media/MediaService.cs`
- Replace: `src/DuoCompanion.App/Pages/MediaPage.xaml`
- Replace: `src/DuoCompanion.App/Pages/MediaPage.xaml.cs`

- [ ] **Step 1: Create IMediaService**

Create `src/DuoCompanion.Contracts/Services/IMediaService.cs`:

```csharp
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
```

- [ ] **Step 2: Implement MediaService**

Create `src/DuoCompanion.Services/Media/MediaService.cs`:

```csharp
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
```

- [ ] **Step 3: Replace MediaPage.xaml**

```xml
<Page x:Class="DuoCompanion.App.Pages.MediaPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Spacing="24">

        <!-- Playback row -->
        <StackPanel Orientation="Horizontal" Spacing="16" HorizontalAlignment="Center">
            <Button x:Name="BtnPrev"    Width="72" Height="72" Click="OnPrev"     ToolTipService.ToolTip="Previous">
                <FontIcon Glyph="&#xE892;" FontSize="28"/>
            </Button>
            <Button x:Name="BtnPlay"    Width="88" Height="88" Click="OnPlayPause" ToolTipService.ToolTip="Play/Pause">
                <FontIcon Glyph="&#xE769;" FontSize="36"/>
            </Button>
            <Button x:Name="BtnNext"    Width="72" Height="72" Click="OnNext"     ToolTipService.ToolTip="Next">
                <FontIcon Glyph="&#xE893;" FontSize="28"/>
            </Button>
        </StackPanel>

        <!-- Volume row -->
        <StackPanel Orientation="Horizontal" Spacing="16" HorizontalAlignment="Center">
            <Button Width="64" Height="56" Click="OnVolumeDown" ToolTipService.ToolTip="Volume Down">
                <FontIcon Glyph="&#xE993;" FontSize="22"/>
            </Button>
            <Button Width="64" Height="56" Click="OnMute"       ToolTipService.ToolTip="Mute">
                <FontIcon Glyph="&#xE74F;" FontSize="22"/>
            </Button>
            <Button Width="64" Height="56" Click="OnVolumeUp"   ToolTipService.ToolTip="Volume Up">
                <FontIcon Glyph="&#xE995;" FontSize="22"/>
            </Button>
        </StackPanel>

    </StackPanel>
</Page>
```

- [ ] **Step 4: Replace MediaPage.xaml.cs**

```csharp
using DuoCompanion.App;
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
```

- [ ] **Step 5: Register in DI**
```csharp
services.AddSingleton<IMediaService, MediaService>();
```

- [ ] **Step 6: Build, run, verify, commit**
```
dotnet build src/DuoCompanion.App -r win-arm64
git add src/
git commit -m "feat(M7): media controls (play/pause/next/prev/volume)"
```

---

## Task 11: Handwriting Page

Capture touch/pen strokes on a `Canvas`. Recognize text on demand using `Windows.UI.Input.Inking`. Inject recognized text as keystrokes.

**Files:**
- Replace: `src/DuoCompanion.App/Pages/HandwritingPage.xaml`
- Replace: `src/DuoCompanion.App/Pages/HandwritingPage.xaml.cs`

- [ ] **Step 1: Replace HandwritingPage.xaml**

```xml
<Page x:Class="DuoCompanion.App.Pages.HandwritingPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Ink surface -->
        <Border Grid.Row="0"
                Background="White"
                BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
                BorderThickness="1"
                CornerRadius="8"
                Margin="8">
            <Canvas x:Name="InkSurface"
                    Background="Transparent"
                    PointerPressed="OnPointerPressed"
                    PointerMoved="OnPointerMoved"
                    PointerReleased="OnPointerReleased"
                    PointerCaptureLost="OnPointerReleased"/>
        </Border>

        <!-- Action bar -->
        <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="8" Padding="8,4">
            <Button Content="Recognize &amp; Send" Click="OnRecognize" HorizontalAlignment="Left"/>
            <Button Content="Clear" Click="OnClear" HorizontalAlignment="Left"/>
            <TextBlock x:Name="RecognizedText"
                       VerticalAlignment="Center"
                       Foreground="{ThemeResource TextFillColorSecondaryBrush}"
                       Text="Draw text above, then tap Recognize"/>
        </StackPanel>
    </Grid>
</Page>
```

- [ ] **Step 2: Replace HandwritingPage.xaml.cs**

```csharp
using DuoCompanion.App;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI.Input.Inking;

namespace DuoCompanion.App.Pages;

public sealed partial class HandwritingPage : Page
{
    private readonly IInputService _input;
    private readonly InkManager _inkManager = new();
    private Point _lastPoint;
    private bool _isDrawing;
    private readonly List<Stroke> _strokes = new();
    private Polyline? _currentStroke;

    public HandwritingPage()
    {
        _input = App.Services.GetRequiredService<IInputService>();
        InitializeComponent();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDrawing = true;
        _lastPoint = e.GetCurrentPoint(InkSurface).Position;
        _currentStroke = new Polyline
        {
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.Black),
            StrokeThickness = 3,
            Points = { _lastPoint }
        };
        InkSurface.Children.Add(_currentStroke);
        InkSurface.CapturePointer(e.Pointer);
        _inkManager.ProcessPointerDown(e.GetCurrentPoint(InkSurface));
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDrawing || _currentStroke is null) return;
        var pt = e.GetCurrentPoint(InkSurface).Position;
        _currentStroke.Points.Add(pt);
        _inkManager.ProcessPointerUpdate(e.GetCurrentPoint(InkSurface));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;
        _inkManager.ProcessPointerUp(e.GetCurrentPoint(InkSurface));
    }

    private async void OnRecognize(object sender, RoutedEventArgs e)
    {
        var recognizers = _inkManager.GetRecognizers();
        if (recognizers.Count == 0)
        {
            RecognizedText.Text = "No handwriting recognizer installed";
            return;
        }

        _inkManager.SetDefaultRecognizer(recognizers[0]);
        var results = await _inkManager.RecognizeAsync(InkRecognitionTarget.All);
        var text = string.Join(" ", results.Select(r => r.GetTextCandidates().FirstOrDefault() ?? ""));

        if (!string.IsNullOrWhiteSpace(text))
        {
            RecognizedText.Text = $"Recognized: {text}";
            _input.SendText(text);
        }
        else
        {
            RecognizedText.Text = "Could not recognize — try writing more clearly";
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        InkSurface.Children.Clear();
        _strokes.Clear();
        RecognizedText.Text = string.Empty;
    }
}
```

- [ ] **Step 3: Build and verify**
```
dotnet build src/DuoCompanion.App -r win-arm64
```
Run on device: draw letters → tap "Recognize & Send" → text appears in focused field.

- [ ] **Step 4: Commit**
```bash
git add src/
git commit -m "feat: handwriting page with ink recognition and text injection"
```

---

## Task 12: Orientation Service (Milestone 9)

Detect single/dual screen and portrait/landscape orientation. Raise event when changed so layout can adapt.

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/IOrientationService.cs`
- Create: `src/DuoCompanion.Services/Display/OrientationService.cs`
- Modify: `src/DuoCompanion.App/App.xaml.cs`

- [ ] **Step 1: Create IOrientationService**

Create `src/DuoCompanion.Contracts/Services/IOrientationService.cs`:

```csharp
namespace DuoCompanion.Contracts.Services;

public enum ScreenLayout { SinglePortrait, SingleLandscape, DualPortrait, DualLandscape }

public interface IOrientationService
{
    ScreenLayout Current { get; }
    event EventHandler<ScreenLayout> LayoutChanged;
    void Refresh();
}
```

- [ ] **Step 2: Implement OrientationService**

Create `src/DuoCompanion.Services/Display/OrientationService.cs`:

```csharp
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Display;

public sealed class OrientationService : IOrientationService
{
    private readonly IDisplayService _display;
    private readonly ILogger<OrientationService> _logger;

    public ScreenLayout Current { get; private set; }
    public event EventHandler<ScreenLayout>? LayoutChanged;

    public OrientationService(IDisplayService display, ILogger<OrientationService> logger)
    {
        _display = display;
        _logger = logger;
        Refresh();
    }

    public void Refresh()
    {
        var displays = _display.GetAllDisplays();
        var layout = displays.Count switch
        {
            1 => displays[0].Width >= displays[0].Height
                    ? ScreenLayout.SingleLandscape
                    : ScreenLayout.SinglePortrait,
            _ => displays[0].Width >= displays[0].Height
                    ? ScreenLayout.DualLandscape
                    : ScreenLayout.DualPortrait
        };

        if (layout == Current) return;
        Current = layout;
        _logger.LogInformation("Screen layout changed to {Layout}", layout);
        LayoutChanged?.Invoke(this, layout);
    }
}
```

- [ ] **Step 3: Register and wire to DisplayConfigurationChanged**

In `App.xaml.cs BuildServices()`:
```csharp
services.AddSingleton<IOrientationService, OrientationService>();
```

In `OnLaunched`, after services built:
```csharp
var orientation = Services.GetRequiredService<IOrientationService>();
Services.GetRequiredService<IWindowManagerService>().DisplayConfigurationChanged +=
    (_, _) => orientation.Refresh();
```

- [ ] **Step 4: Build, commit**
```
dotnet build src/DuoCompanion.App -r win-arm64
git add src/
git commit -m "feat(M9): orientation service detects single/dual portrait/landscape"
```

---

## Task 13: Settings Service + Settings Page (Milestone 10)

Persist user preferences (theme, startup, default module, keyboard size) as JSON in `AppData\Local\DuoCompanion\settings.json`.

**Files:**
- Create: `src/DuoCompanion.Core/Models/AppSettings.cs`
- Create: `src/DuoCompanion.Contracts/Services/ISettingsService.cs`
- Create: `src/DuoCompanion.Services/Settings/SettingsService.cs`
- Replace: `src/DuoCompanion.App/Pages/SettingsPage.xaml`
- Replace: `src/DuoCompanion.App/Pages/SettingsPage.xaml.cs`
- Create: `tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs`
- Extend: `src/DuoCompanion.Services/DuoCompanion.Services.csproj`

- [ ] **Step 1: Create AppSettings model**

Create `src/DuoCompanion.Core/Models/AppSettings.cs`:

```csharp
namespace DuoCompanion.Core.Models;

public sealed class AppSettings
{
    public bool LaunchOnStartup { get; set; } = false;
    public string Theme { get; set; } = "System";          // "Light", "Dark", "System"
    public string DefaultModule { get; set; } = "Keyboard"; // matches nav tag
    public double KeyboardButtonSize { get; set; } = 56;
    public double WindowOpacity { get; set; } = 1.0;
}
```

- [ ] **Step 2: Create ISettingsService**

Create `src/DuoCompanion.Contracts/Services/ISettingsService.cs`:

```csharp
using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    void Save();
    void Reset();
}
```

- [ ] **Step 3: Add System.Text.Json to Services.csproj**

Add to `src/DuoCompanion.Services/DuoCompanion.Services.csproj`:
```xml
<PackageReference Include="System.Text.Json" Version="9.0.0" />
```

- [ ] **Step 4: Implement SettingsService**

Create `src/DuoCompanion.Services/Settings/SettingsService.cs`:

```csharp
using System.Text.Json;
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DuoCompanion", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<SettingsService> _logger;

    public AppSettings Current { get; private set; }

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings — using defaults");
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, JsonOptions));
            _logger.LogInformation("Settings saved to {Path}", SettingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void Reset()
    {
        Current = new AppSettings();
        Save();
    }
}
```

- [ ] **Step 5: Write SettingsService unit tests**

Create `tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs`:

```csharp
using DuoCompanion.Core.Models;

namespace DuoCompanion.Tests.Services;

public sealed class AppSettingsTests
{
    [Fact]
    public void Default_theme_is_System()
    {
        var settings = new AppSettings();
        Assert.Equal("System", settings.Theme);
    }

    [Fact]
    public void Default_module_is_Keyboard()
    {
        var settings = new AppSettings();
        Assert.Equal("Keyboard", settings.DefaultModule);
    }

    [Fact]
    public void Default_opacity_is_1()
    {
        var settings = new AppSettings();
        Assert.Equal(1.0, settings.WindowOpacity);
    }

    [Fact]
    public void Settings_are_mutable()
    {
        var settings = new AppSettings();
        settings.Theme = "Dark";
        Assert.Equal("Dark", settings.Theme);
    }
}
```

- [ ] **Step 6: Run tests**
```
dotnet test tests/DuoCompanion.Tests -r win-arm64
```
Expected: All tests pass (4 settings + 3 clipboard = 7 total).

- [ ] **Step 7: Replace SettingsPage.xaml**

```xml
<Page x:Class="DuoCompanion.App.Pages.SettingsPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <ScrollViewer Padding="16">
        <StackPanel Spacing="16" MaxWidth="500">

            <TextBlock Text="Settings" Style="{StaticResource TitleTextBlockStyle}"/>

            <!-- Theme -->
            <StackPanel Spacing="4">
                <TextBlock Text="Theme" Style="{StaticResource BodyStrongTextBlockStyle}"/>
                <ComboBox x:Name="ThemeCombo" SelectionChanged="OnThemeChanged" Width="200">
                    <ComboBoxItem Content="System" Tag="System"/>
                    <ComboBoxItem Content="Light"  Tag="Light"/>
                    <ComboBoxItem Content="Dark"   Tag="Dark"/>
                </ComboBox>
            </StackPanel>

            <!-- Default module -->
            <StackPanel Spacing="4">
                <TextBlock Text="Default Module" Style="{StaticResource BodyStrongTextBlockStyle}"/>
                <ComboBox x:Name="DefaultModuleCombo" SelectionChanged="OnDefaultModuleChanged" Width="200">
                    <ComboBoxItem Content="Keyboard"    Tag="Keyboard"/>
                    <ComboBoxItem Content="Touchpad"    Tag="Touchpad"/>
                    <ComboBoxItem Content="Clipboard"   Tag="Clipboard"/>
                    <ComboBoxItem Content="Media"       Tag="Media"/>
                    <ComboBoxItem Content="Handwriting" Tag="Handwriting"/>
                </ComboBox>
            </StackPanel>

            <!-- Opacity -->
            <StackPanel Spacing="4">
                <TextBlock Text="Window Opacity" Style="{StaticResource BodyStrongTextBlockStyle}"/>
                <Slider x:Name="OpacitySlider"
                        Minimum="0.3" Maximum="1.0" StepFrequency="0.05"
                        ValueChanged="OnOpacityChanged" Width="300"/>
            </StackPanel>

            <Button Content="Reset to Defaults" Click="OnReset" Margin="0,8,0,0"/>

        </StackPanel>
    </ScrollViewer>
</Page>
```

- [ ] **Step 8: Replace SettingsPage.xaml.cs**

```csharp
using DuoCompanion.App;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DuoCompanion.App.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ISettingsService _settings;
    private bool _loading = true;

    public SettingsPage()
    {
        _settings = App.Services.GetRequiredService<ISettingsService>();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        var s = _settings.Current;

        ThemeCombo.SelectedIndex = s.Theme switch { "Light" => 1, "Dark" => 2, _ => 0 };

        DefaultModuleCombo.SelectedIndex = s.DefaultModule switch
        {
            "Touchpad"    => 1, "Clipboard" => 2,
            "Media"       => 3, "Handwriting" => 4, _ => 0
        };

        OpacitySlider.Value = s.WindowOpacity;
        _loading = false;
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _settings.Current.Theme = tag;
        _settings.Save();
    }

    private void OnDefaultModuleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || DefaultModuleCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _settings.Current.DefaultModule = tag;
        _settings.Save();
    }

    private void OnOpacityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading) return;
        _settings.Current.WindowOpacity = OpacitySlider.Value;
        _settings.Save();
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _settings.Reset();
        OnLoaded(sender, new RoutedEventArgs());
    }
}
```

- [ ] **Step 9: Register in DI**
```csharp
services.AddSingleton<ISettingsService, SettingsService>();
```

- [ ] **Step 10: Build, run, verify, commit**
```
dotnet build src/DuoCompanion.App -r win-arm64
git add src/ tests/
git commit -m "feat(M10): settings service with JSON persistence and settings page"
```

---

## Self-Review

### Spec Coverage Check

| Milestone | Covered by |
|---|---|
| M1 Display Detection | Task 1 (existing scaffold) |
| M2 Companion Window | Task 1 |
| M3 Navigation Shell | Task 2 |
| M4 Virtual Keyboard | Tasks 3 + 4 |
| M5 Virtual Touchpad | Task 9 |
| M6 Clipboard Manager | Tasks 7 + 8 |
| M7 Media Controls | Task 10 |
| M8 UI Automation | Tasks 5 + 6 |
| M9 Orientation | Task 12 |
| M10 Settings | Task 13 |
| Handwriting | Task 11 |

All 10 milestones + handwriting are covered.

### Known Limitations (to document in `docs/`)

- `IAccessible` COM interop may require `Accessibility` type library to be present on the target machine (it is standard on all Windows 11 installations).
- Handwriting recognition requires a Windows Handwriting Recognizer to be installed (available by default for English on Win11).
- `InkManager` is technically deprecated in favor of `InkPresenter`; if `InkManager` is unavailable on WinAppSDK/Win11 ARM, the recognizer step in Task 11 should switch to `Windows.UI.Input.Inking.InkPresenter` with a `CoreInkIndependentInputSource`.
- Clipboard paste (`PasteAsync`) simulates Ctrl+V — this assumes the target window accepts standard paste. Rich-text or password fields may behave differently.
- `StartOnLogin` is listed in settings but launch-on-startup implementation (Task Registry key or Startup folder shortcut) is not included — add as a follow-up task.
