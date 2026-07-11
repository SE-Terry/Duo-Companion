# Auto-hide Companion Window, Tray Icon, and Quit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the companion window auto-hide/show based on text-input focus (configurable Off/Smart/Always), add a system tray icon as the manual show/hide/quit entry point, and add a Quit button to Settings.

**Architecture:** Reuse the existing `_lastManualPage` field in `CompanionWindow` as the "is a non-keyboard page manually pinned" signal driving a small state machine in the existing focus/blur handlers. Hide/show is a new pair of `IWindowManagerService` methods wrapping raw `ShowWindow` (Win32), matching how the rest of that service already manipulates the window. The tray icon is a new `ITrayIconService` implemented with `System.Windows.Forms.NotifyIcon`, isolated entirely inside `DuoCompanion.Services` so WinForms doesn't leak into other projects.

**Tech Stack:** C# / .NET 9, WinUI 3, Win32 P/Invoke (existing `NativeMethods`), `System.Windows.Forms.NotifyIcon`, xunit (existing test project).

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-11-auto-hide-window-design.md` — every requirement in that doc must map to a task below.
- **No local build/test execution is possible for this plan.** This repo targets Windows-only TFMs (`net9.0-windows10.0.19041.0`) built via MSBuild/WinAppSDK, and there is no `dotnet` CLI in this environment at all (confirmed: `dotnet --version` → "command not found"). Every task below is a code-writing task only — do not attempt `dotnet build`, `dotnet test`, or `build-release.ps1` locally; they will not work here. The final task in this plan is a manual verification checklist to hand to the user for their Windows machine.
- Follow existing project conventions: services live in `DuoCompanion.Services/<Area>/`, contracts in `DuoCompanion.Contracts/Services/`, event-based pub/sub for cross-service notifications (see `IUiAutomationService`, `IWindowManagerService.DisplayConfigurationChanged`), settings persisted as plain string/bool/double properties on `AppSettings` (see `Theme`, `DefaultModule`, `CompanionDisplay`).
- No app icon asset exists in the project (no `.ico` file, no `ApplicationIcon` in any csproj). Use `System.Drawing.SystemIcons.Application` (a stock Windows icon) for the tray icon rather than hand-crafting a new binary `.ico` asset that can't be verified from this environment. This is a deliberate, documented deviation from the spec's "add a minimal `.ico` file" suggestion — same visible behavior (a tray icon exists), lower risk.

---

### Task 1: `AutoHideMode` setting

**Files:**
- Modify: `src/DuoCompanion.Core/Models/AppSettings.cs`
- Modify: `tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs`

**Interfaces:**
- Produces: `AppSettings.AutoHideMode` (`string`, default `"Smart"`), consumed by Task 4 (`CompanionWindow`) and Task 6 (`SettingsPage`).

- [ ] **Step 1: Write the failing tests**

Add to the existing `AppSettingsTests` class in `tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs` (it already has `Default_theme_is_System`, `Default_module_is_Keyboard`, etc. — add these two alongside them, same style):

```csharp
    [Fact]
    public void Default_auto_hide_mode_is_Smart()
    {
        var settings = new AppSettings();
        Assert.Equal("Smart", settings.AutoHideMode);
    }

    [Fact]
    public void AutoHideMode_is_mutable()
    {
        var settings = new AppSettings();
        settings.AutoHideMode = "Always";
        Assert.Equal("Always", settings.AutoHideMode);
    }
```

- [ ] **Step 2: Confirm the tests reference a member that doesn't exist yet**

Cannot run `dotnet test` in this environment (no `dotnet` CLI). Instead, confirm by inspection: open `src/DuoCompanion.Core/Models/AppSettings.cs` and verify `AutoHideMode` is not yet a member — the two new tests reference a property that doesn't exist, so they would fail to compile if built right now.

- [ ] **Step 3: Add the property**

In `src/DuoCompanion.Core/Models/AppSettings.cs`, add a new property alongside the existing ones (after `CompanionDisplay`):

```csharp
    public string AutoHideMode { get; set; } = "Smart"; // "Off", "Smart", "Always"
```

The full file should read:

```csharp
namespace DuoCompanion.Core.Models;

public sealed class AppSettings
{
    public bool LaunchOnStartup { get; set; } = false;
    public string Theme { get; set; } = "System";          // "Light", "Dark", "System"
    public string DefaultModule { get; set; } = "Keyboard"; // matches nav tag
    public double KeyboardButtonSize { get; set; } = 56;
    public double WindowOpacity { get; set; } = 1.0;
    public string CompanionDisplay { get; set; } = "Right"; // "Right", "Left", "Auto" (whichever Windows reports as secondary)
    public string AutoHideMode { get; set; } = "Smart"; // "Off", "Smart", "Always"
}
```

- [ ] **Step 4: Confirm by inspection that the tests would now pass**

Again, no local `dotnet test` is possible. Re-read the two new test bodies against the new property: `new AppSettings().AutoHideMode` is `"Smart"` (matches `Default_auto_hide_mode_is_Smart`), and it's a mutable auto-property so `AutoHideMode_is_mutable` holds. This gets exercised for real in the final manual-verification task once the user builds on Windows — flag in that task that `dotnet test` should be run there if convenient.

- [ ] **Step 5: Commit**

```bash
git add src/DuoCompanion.Core/Models/AppSettings.cs tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs
git commit -m "Add AutoHideMode setting (Off/Smart/Always)"
```

---

### Task 2: Window hide/show plumbing

**Files:**
- Modify: `src/DuoCompanion.Contracts/Services/IWindowManagerService.cs`
- Modify: `src/DuoCompanion.Services/Win32/NativeMethods.cs`
- Modify: `src/DuoCompanion.Services/Window/WindowManagerService.cs`

**Interfaces:**
- Consumes: nothing new (uses existing `PositionCompanionWindow(IntPtr)` already on this class).
- Produces: `IWindowManagerService.HideCompanionWindow(IntPtr hwnd)` and `IWindowManagerService.ShowCompanionWindow(IntPtr hwnd)`, consumed by Task 4 (`CompanionWindow`).

No automated test here — this is raw Win32 window-visibility manipulation with no existing test harness in this project (consistent with `WindowManagerService`'s other methods, e.g. `PositionCompanionWindow`/`MakeWindowNonActivating`, which also have no unit tests — they're verified manually, same as this task will be in the final verification pass).

- [ ] **Step 1: Add the P/Invoke and constants**

In `src/DuoCompanion.Services/Win32/NativeMethods.cs`, add a new section after the existing `// --- Window positioning + display change hook (Task 1) ---` block (right after the `WinEventProc` delegate declaration, before `// --- Keyboard/mouse injection (Tasks 3 + 9) ---`):

```csharp
    // --- Window visibility (auto-hide) ---

    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNOACTIVATE = 4;

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
```

- [ ] **Step 2: Add the interface methods**

In `src/DuoCompanion.Contracts/Services/IWindowManagerService.cs`, add two methods. Full file:

```csharp
namespace DuoCompanion.Contracts.Services;

public interface IWindowManagerService
{
    void MakeWindowNonActivating(IntPtr hwnd);
    void PositionCompanionWindow(IntPtr hwnd);
    void HideCompanionWindow(IntPtr hwnd);
    void ShowCompanionWindow(IntPtr hwnd);
    event EventHandler DisplayConfigurationChanged;
    void StartMonitoring(IntPtr hostHwnd);
    void StopMonitoring();
}
```

- [ ] **Step 3: Implement them**

In `src/DuoCompanion.Services/Window/WindowManagerService.cs`, add two methods right after the existing `PositionCompanionWindow` method (before `private DisplayInfo? SelectCompanionDisplay()`):

```csharp
    public void HideCompanionWindow(IntPtr hwnd)
    {
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_HIDE);
        _logger.LogInformation("Companion window hidden");
    }

    public void ShowCompanionWindow(IntPtr hwnd)
    {
        PositionCompanionWindow(hwnd);
        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNOACTIVATE);
        _logger.LogInformation("Companion window shown");
    }
```

`ShowCompanionWindow` repositions first — the display configuration may have changed while the window was hidden, and `PositionCompanionWindow` already safely no-ops (logs a warning, leaves the window alone) when fewer than 2 displays are present.

- [ ] **Step 4: Commit**

```bash
git add src/DuoCompanion.Contracts/Services/IWindowManagerService.cs src/DuoCompanion.Services/Win32/NativeMethods.cs src/DuoCompanion.Services/Window/WindowManagerService.cs
git commit -m "Add HideCompanionWindow/ShowCompanionWindow to IWindowManagerService"
```

---

### Task 3: Tray icon service

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/ITrayIconService.cs`
- Create: `src/DuoCompanion.Services/Tray/TrayIconService.cs`
- Modify: `src/DuoCompanion.Services/DuoCompanion.Services.csproj`

**Interfaces:**
- Produces: `ITrayIconService` with `event EventHandler ToggleVisibilityRequested`, `event EventHandler QuitRequested`, `void Start()`, `void Stop()` — consumed by Task 5 (`App.xaml.cs`).

No automated test — OS tray-icon interaction has no test harness in this project or feasible one here; verified manually in the final task.

- [ ] **Step 1: Add `<UseWindowsForms>` to the Services project**

`System.Windows.Forms.NotifyIcon` requires this. In `src/DuoCompanion.Services/DuoCompanion.Services.csproj`, add it to the `PropertyGroup` (after `<ImplicitUsings>enable</ImplicitUsings>`):

```xml
    <UseWindowsForms>true</UseWindowsForms>
```

Full `PropertyGroup` should read:

```xml
  <PropertyGroup>
    <TargetFramework>net9.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RuntimeIdentifiers>win-arm64</RuntimeIdentifiers>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
  </PropertyGroup>
```

This is scoped to `DuoCompanion.Services` only — `DuoCompanion.App`, `Contracts`, and `Core` do not reference WinForms types directly, keeping the dependency isolated to where it's actually used.

- [ ] **Step 2: Write the contract**

Create `src/DuoCompanion.Contracts/Services/ITrayIconService.cs`:

```csharp
namespace DuoCompanion.Contracts.Services;

public interface ITrayIconService
{
    event EventHandler ToggleVisibilityRequested;
    event EventHandler QuitRequested;
    void Start();
    void Stop();
}
```

- [ ] **Step 3: Implement the service**

Create `src/DuoCompanion.Services/Tray/TrayIconService.cs`:

```csharp
using System.Windows.Forms;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Tray;

public sealed class TrayIconService : ITrayIconService, IDisposable
{
    private readonly ILogger<TrayIconService> _logger;
    private NotifyIcon? _notifyIcon;

    public event EventHandler? ToggleVisibilityRequested;
    public event EventHandler? QuitRequested;

    public TrayIconService(ILogger<TrayIconService> logger) => _logger = logger;

    public void Start()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show/Hide Duo Companion", null, (_, _) => ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Quit", null, (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Duo Companion",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += OnMouseClick;

        _logger.LogInformation("Tray icon started");
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            ToggleVisibilityRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_notifyIcon is null) return;
        _notifyIcon.MouseClick -= OnMouseClick;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
        _logger.LogInformation("Tray icon stopped");
    }

    public void Dispose() => Stop();
}
```

`Stop()` explicitly sets `Visible = false` before disposing — an undisposed/hidden `NotifyIcon` can leave a ghost icon in the tray until the user mouses over it; this is the standard fix.

- [ ] **Step 4: Commit**

```bash
git add src/DuoCompanion.Contracts/Services/ITrayIconService.cs src/DuoCompanion.Services/Tray/TrayIconService.cs src/DuoCompanion.Services/DuoCompanion.Services.csproj
git commit -m "Add system tray icon service"
```

---

### Task 4: `CompanionWindow` auto-hide state machine

**Files:**
- Modify: `src/DuoCompanion.App/CompanionWindow.xaml.cs`

**Interfaces:**
- Consumes: `ISettingsService.Current.AutoHideMode` (Task 1), `IWindowManagerService.HideCompanionWindow`/`ShowCompanionWindow` (Task 2).
- Produces: `CompanionWindow` constructor now takes `(IWindowManagerService, IUiAutomationService, ISettingsService)` — consumed by Task 5 (`App.xaml.cs`). Also produces `public void ToggleVisibility()`, consumed by Task 5.

No automated test — this is a WinUI `Window` subclass with no existing test harness (the `App`/`CompanionWindow`/`Pages` code in this project has never been unit tested; verification is manual, same as always for this project).

- [ ] **Step 1: Add the `ISettingsService` dependency and hide-state fields**

In `src/DuoCompanion.App/CompanionWindow.xaml.cs`, add a using and two fields. Change:

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
    private readonly Frame _contentFrame = new();
    private Type _lastManualPage = typeof(KeyboardPage);
```

to:

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
    private readonly ISettingsService _settings;
    private readonly Frame _contentFrame = new();
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private Type _lastManualPage = typeof(KeyboardPage);
    private bool _isHidden;
```

- [ ] **Step 2: Update the constructor**

Change:

```csharp
    public CompanionWindow(IWindowManagerService windowManager, IUiAutomationService automation)
    {
        _windowManager = windowManager;
        _automation    = automation;
        InitializeComponent();
        Content = CreateContent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption =
            Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;

        _automation.TextInputFocused += OnTextInputFocused;
        _automation.TextInputBlurred += OnTextInputBlurred;
        Activated += OnFirstActivated;
        Closed += OnClosed;
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
    }
```

to:

```csharp
    public CompanionWindow(IWindowManagerService windowManager, IUiAutomationService automation, ISettingsService settings)
    {
        _windowManager = windowManager;
        _automation    = automation;
        _settings      = settings;
        InitializeComponent();
        Content = CreateContent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption =
            Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed;

        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            HideCompanionWindowNow();
        };

        _automation.TextInputFocused += OnTextInputFocused;
        _automation.TextInputBlurred += OnTextInputBlurred;
        Activated += OnFirstActivated;
        Closed += OnClosed;
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
    }
```

- [ ] **Step 3: Add the hide/show helpers and `ToggleVisibility`**

Add these new methods right after `NavigateTo`:

```csharp
    public void ToggleVisibility()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isHidden)
            {
                NavigateTo(_lastManualPage);
                ShowCompanionWindowNow();
            }
            else
            {
                HideCompanionWindowNow();
            }
        });
    }

    private void ShowCompanionWindowNow()
    {
        _hideTimer.Stop();
        if (!_isHidden) return;
        _windowManager.ShowCompanionWindow(WindowNative.GetWindowHandle(this));
        _isHidden = false;
    }

    private void HideCompanionWindowNow()
    {
        if (_isHidden) return;
        _windowManager.HideCompanionWindow(WindowNative.GetWindowHandle(this));
        _isHidden = true;
    }

    private void HideCompanionWindowDebounced()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }
```

- [ ] **Step 4: Wire the state machine into focus/blur**

Change:

```csharp
    private void OnTextInputFocused(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => NavigateTo(typeof(KeyboardPage)));
    }

    private void OnTextInputBlurred(object? sender, EventArgs e)
    {
        // Only revert if user hadn't manually switched away from keyboard
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_contentFrame.CurrentSourcePageType == typeof(KeyboardPage))
                NavigateTo(_lastManualPage == typeof(KeyboardPage) ? typeof(KeyboardPage) : _lastManualPage);
        });
    }
```

to:

```csharp
    private void OnTextInputFocused(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowCompanionWindowNow();
            NavigateTo(typeof(KeyboardPage));
        });
    }

    private void OnTextInputBlurred(object? sender, EventArgs e)
    {
        // Only act if the window is currently on the auto-triggered keyboard page —
        // if the user manually pinned a different page, a background focus change
        // elsewhere on the system shouldn't touch this window at all.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_contentFrame.CurrentSourcePageType != typeof(KeyboardPage)) return;

            var mode = _settings.Current.AutoHideMode;
            var hasManualPin = _lastManualPage != typeof(KeyboardPage);

            if (mode == "Always" || (mode == "Smart" && !hasManualPin))
            {
                HideCompanionWindowDebounced();
                return;
            }

            NavigateTo(_lastManualPage);
        });
    }
```

Behavior check against the spec's state machine table:
- `Off`: neither condition in the `if` is true (`mode` is never `"Always"`, and `mode == "Smart"` is false), so it always falls through to `NavigateTo(_lastManualPage)` — unchanged from today.
- `Smart` with a manual page pinned (`hasManualPin == true`): condition is false, falls through to `NavigateTo(_lastManualPage)` — same as `Off` for this case, matching the spec.
- `Smart` with nothing pinned (`hasManualPin == false`, i.e. `_lastManualPage` is already `KeyboardPage`): condition is true, hides.
- `Always`: condition is always true, hides regardless of `hasManualPin`.

- [ ] **Step 5: Commit**

```bash
git add src/DuoCompanion.App/CompanionWindow.xaml.cs
git commit -m "Add auto-hide state machine to CompanionWindow"
```

---

### Task 5: Wire tray icon and settings into `App`

**Files:**
- Modify: `src/DuoCompanion.App/App.xaml.cs`

**Interfaces:**
- Consumes: `ITrayIconService` (Task 3), `CompanionWindow(IWindowManagerService, IUiAutomationService, ISettingsService)` constructor and `ToggleVisibility()` (Task 4).
- Produces: `public static void App.Quit()`, consumed by Task 6 (`SettingsPage`'s Quit button).

No automated test — `App` is the WinUI application entry point, not unit-testable in this project's existing setup.

- [ ] **Step 1: Register the tray icon service and update imports**

In `src/DuoCompanion.App/App.xaml.cs`, add a using and a DI registration. Change:

```csharp
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Automation;
using DuoCompanion.Services.Clipboard;
using DuoCompanion.Services.Display;
using DuoCompanion.Services.Input;
using DuoCompanion.Services.Media;
using DuoCompanion.Services.Settings;
using DuoCompanion.Services.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
```

to:

```csharp
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Automation;
using DuoCompanion.Services.Clipboard;
using DuoCompanion.Services.Display;
using DuoCompanion.Services.Input;
using DuoCompanion.Services.Media;
using DuoCompanion.Services.Settings;
using DuoCompanion.Services.Tray;
using DuoCompanion.Services.Window;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
```

And in `BuildServices`, change:

```csharp
        services.AddSingleton<IOrientationService, OrientationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        return services.BuildServiceProvider();
```

to:

```csharp
        services.AddSingleton<IOrientationService, OrientationService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        return services.BuildServiceProvider();
```

- [ ] **Step 2: Update the `CompanionWindow` construction call and wire the tray icon**

Change:

```csharp
            CompanionWindow = new CompanionWindow(
                Services.GetRequiredService<IWindowManagerService>(),
                Services.GetRequiredService<IUiAutomationService>());
            CompanionWindow.Activate();
            WriteStartupLog("Companion window activated.");
```

to:

```csharp
            CompanionWindow = new CompanionWindow(
                Services.GetRequiredService<IWindowManagerService>(),
                Services.GetRequiredService<IUiAutomationService>(),
                Services.GetRequiredService<ISettingsService>());
            CompanionWindow.Activate();
            WriteStartupLog("Companion window activated.");

            var tray = Services.GetRequiredService<ITrayIconService>();
            tray.ToggleVisibilityRequested += (_, _) => CompanionWindow?.ToggleVisibility();
            tray.QuitRequested += (_, _) => Quit();
            tray.Start();
            WriteStartupLog("Tray icon started.");
```

- [ ] **Step 3: Add the shared `Quit` method**

Add this method to the `App` class, after `OnLaunched`:

```csharp
    public static void Quit()
    {
        Services.GetRequiredService<ITrayIconService>().Stop();
        Application.Current.Exit();
    }
```

This is the single quit path both the tray menu's "Quit" item and the Settings page's "Quit Duo Companion" button call — the tray icon must be explicitly stopped/disposed before exit or it can leave a ghost icon behind (see Task 3).

- [ ] **Step 4: Commit**

```bash
git add src/DuoCompanion.App/App.xaml.cs
git commit -m "Wire tray icon service and shared Quit path into App"
```

---

### Task 6: Settings UI — Auto-hide mode and Quit button

**Files:**
- Modify: `src/DuoCompanion.App/Pages/SettingsPage.xaml`
- Modify: `src/DuoCompanion.App/Pages/SettingsPage.xaml.cs`

**Interfaces:**
- Consumes: `AppSettings.AutoHideMode` (Task 1), `App.Quit()` (Task 5).

No automated test — `SettingsPage` is a WinUI `Page`, not unit-testable in this project's existing setup (same as `SettingsPage`'s other combo handlers, none of which are tested today).

- [ ] **Step 1: Add the Auto-hide combo box to the XAML**

In `src/DuoCompanion.App/Pages/SettingsPage.xaml`, insert a new section between the "Companion display" `StackPanel` and the "Opacity" `StackPanel`. Change:

```xml
            <!-- Companion display -->
            <StackPanel Spacing="4">
                <TextBlock Text="Companion Display" Style="{StaticResource BodyStrongTextBlockStyle}"/>
                <ComboBox x:Name="CompanionDisplayCombo" SelectionChanged="OnCompanionDisplayChanged" Width="200">
                    <ComboBoxItem Content="Right screen" Tag="Right"/>
                    <ComboBoxItem Content="Left screen"  Tag="Left"/>
                    <ComboBoxItem Content="Auto (Windows secondary)" Tag="Auto"/>
                </ComboBox>
            </StackPanel>

            <!-- Opacity -->
```

to:

```xml
            <!-- Companion display -->
            <StackPanel Spacing="4">
                <TextBlock Text="Companion Display" Style="{StaticResource BodyStrongTextBlockStyle}"/>
                <ComboBox x:Name="CompanionDisplayCombo" SelectionChanged="OnCompanionDisplayChanged" Width="200">
                    <ComboBoxItem Content="Right screen" Tag="Right"/>
                    <ComboBoxItem Content="Left screen"  Tag="Left"/>
                    <ComboBoxItem Content="Auto (Windows secondary)" Tag="Auto"/>
                </ComboBox>
            </StackPanel>

            <!-- Auto-hide -->
            <StackPanel Spacing="4">
                <TextBlock Text="Auto-hide" Style="{StaticResource BodyStrongTextBlockStyle}"/>
                <ComboBox x:Name="AutoHideModeCombo" SelectionChanged="OnAutoHideModeChanged" Width="200">
                    <ComboBoxItem Content="Off"    Tag="Off"/>
                    <ComboBoxItem Content="Smart"  Tag="Smart"/>
                    <ComboBoxItem Content="Always" Tag="Always"/>
                </ComboBox>
            </StackPanel>

            <!-- Opacity -->
```

- [ ] **Step 2: Add the Quit button to the XAML**

Change:

```xml
            <Button Content="Reset to Defaults" Click="OnReset" Margin="0,8,0,0"/>

        </StackPanel>
    </ScrollViewer>
</Page>
```

to:

```xml
            <Button Content="Reset to Defaults" Click="OnReset" Margin="0,8,0,0"/>
            <Button Content="Quit Duo Companion" Click="OnQuit"/>

        </StackPanel>
    </ScrollViewer>
</Page>
```

- [ ] **Step 3: Wire the combo box and button in code-behind**

In `src/DuoCompanion.App/Pages/SettingsPage.xaml.cs`, add the new combo's initial value in `OnLoaded`. Change:

```csharp
        CompanionDisplayCombo.SelectedIndex = s.CompanionDisplay switch { "Left" => 1, "Auto" => 2, _ => 0 };

        OpacitySlider.Value = s.WindowOpacity;
        _loading = false;
```

to:

```csharp
        CompanionDisplayCombo.SelectedIndex = s.CompanionDisplay switch { "Left" => 1, "Auto" => 2, _ => 0 };

        AutoHideModeCombo.SelectedIndex = s.AutoHideMode switch { "Off" => 0, "Always" => 2, _ => 1 };

        OpacitySlider.Value = s.WindowOpacity;
        _loading = false;
```

Then add the new handlers after `OnCompanionDisplayChanged`:

```csharp
    private void OnAutoHideModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || AutoHideModeCombo.SelectedItem is not ComboBoxItem { Tag: string tag }) return;
        _settings.Current.AutoHideMode = tag;
        _settings.Save();
    }
```

And add the quit handler after `OnReset`:

```csharp
    private void OnQuit(object sender, RoutedEventArgs e)
    {
        App.Quit();
    }
```

- [ ] **Step 4: Commit**

```bash
git add src/DuoCompanion.App/Pages/SettingsPage.xaml src/DuoCompanion.App/Pages/SettingsPage.xaml.cs
git commit -m "Add Auto-hide setting and Quit button to Settings page"
```

---

### Task 7: Update README

**Files:**
- Modify: `README.md`

**Interfaces:** none (documentation only).

- [ ] **Step 1: Update the feature table**

Find the row `| Auto-show keyboard on focus | Partial | Detects Win32 text fields (Notepad, browsers); may miss modern UWP/XAML apps |` in the features table and replace it with two rows — one for the (unchanged) detection caveat, one for the new hide behavior:

```markdown
| Auto-show keyboard on focus | Partial | Detects Win32 text fields (Notepad, browsers); may miss modern UWP/XAML apps |
| Auto-hide when unfocused | Works | Configurable in Settings → Auto-hide: Off (always visible), Smart (hides only when idle, stays open if you've manually opened Touchpad/Clipboard/etc.), Always (hides on any blur) |
```

(Keep the existing "Auto-show keyboard on focus" row as-is; add the new "Auto-hide when unfocused" row directly below it.)

- [ ] **Step 2: Document the tray icon and quit**

Find the "First-Run Checklist" section (or the section immediately following the features table) and add a short paragraph:

```markdown
### Exiting the app

Duo Companion has no title bar or taskbar entry by design (it's a
non-activating overlay, like a system on-screen keyboard). Look for its
icon in the system tray (bottom-right, primary screen) — left-click to
show/hide the window, right-click for a Show/Hide and Quit menu. You can
also quit from Settings → Quit Duo Companion.
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "Document auto-hide, tray icon, and quit in README"
```

---

### Task 8: Manual verification on Windows

**Files:** none — this task produces no code changes, only a verification report.

This cannot be executed in this session (no Windows machine, no `dotnet`/MSBuild here). Hand this checklist to the user to run on their Windows machine, the same way prior fixes in this project were verified (build via `build-release.ps1`, run `DuoCompanion.exe`, share `startup.log` and observations).

- [ ] **Step 1: Build and run**

```powershell
.\build-release.ps1
.\dist\DuoCompanion-win-arm64\DuoCompanion.exe
```

Confirm `startup.log` shows a clean launch (no `Application launch failed` entries), matching the pattern already established in this project's crash-fix history.

- [ ] **Step 2: Verify each `AutoHideMode`**

For each of Off / Smart / Always (set via Settings → Auto-hide):
- **Off**: focus a text field → keyboard shows. Blur it → window stays visible, reverts to last manually-opened page (or Keyboard if none). Window never disappears.
- **Smart**: with no page manually pinned, focus a text field → keyboard shows; blur it → window hides after a brief pause; focus again → window reappears showing Keyboard. Then manually open Touchpad, then focus/blur a text field → window should NOT hide (stays on Touchpad, since it's pinned).
- **Always**: manually open Touchpad, then focus and blur a text field → window hides even though Touchpad was open. Focusing again reopens it on Keyboard.
- Rapidly tab between two adjacent text fields in Smart/Always mode — window should not visibly flicker hide/show (debounce working).

- [ ] **Step 3: Verify the tray icon**

- Icon appears in the system tray after launch, tooltip reads "Duo Companion".
- Left-click toggles the window's visibility.
- Right-click shows a menu with "Show/Hide Duo Companion" and "Quit"; both work.
- After quitting via the tray menu, confirm no ghost icon remains in the tray (may need to hover the tray area to confirm Windows has cleared it).

- [ ] **Step 4: Verify Quit from Settings**

Open Settings → "Quit Duo Companion" → app exits cleanly, tray icon is removed.

- [ ] **Step 5: Verify positioning after wake-from-hide**

With a dual-display setup (or by toggling the Companion Display setting), hide then show the window (via tray or focus) and confirm it's still positioned on the correct screen per the Companion Display setting.

- [ ] **Step 6: Report back**

Share `startup.log` and a summary of anything that didn't match the expected behavior above — same format as the crash-fix verification earlier in this project.
