# DuoSnap Flagship Feature (M1–M4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the DuoSnap flagship feature inside the existing DuoCompanion app: drag any window into the hinge zone between the Surface Duo's two displays and it automatically spans across both; drag a spanned window's title bar again and it restores to its pre-span size/position.

**Architecture:** This is Plan 1 of the full `docs/snap-development-plan.md` (12 phases / 7 milestones), covering Phases 1–7 (Milestones M1–M4: display topology, window tracking, drag/hinge detection, span preview overlay, span engine, restore engine). Later phases (M5 touch/orientation polish, M6 settings, M7 advanced features — snap layouts, gestures, per-app profiles, keyboard shortcuts, external-monitor awareness) are separate follow-up plans, written after this one is built and reviewed, since their exact file/line references depend on the code this plan produces.

DuoSnap is **combined into the existing DuoCompanion app** (per explicit user decision — not a separate service/process), reusing its established layering: `DuoCompanion.Core` (plain data models + pure logic, unit-tested), `DuoCompanion.Contracts` (interfaces), `DuoCompanion.Services` (Win32 interop + business logic, registered as DI singletons in `App.xaml.cs`), `DuoCompanion.App` (WinUI3 windows/pages). New code goes in a `Snap` sub-namespace/folder in each project, alongside the existing `Window`, `Display`, `Automation` etc. folders in `DuoCompanion.Services`.

The hinge is **not a real gap in Windows' coordinate space** — Windows treats the Duo's two panels as adjacent monitors with no gap between them. "The hinge zone" is therefore a coordinate-space band (±30px, matching `docs/snap-development-plan.md`'s suggested default) straddling the shared edge between the two displays, computed from `IDisplayService.GetAllDisplays()` (already implemented, reused as-is).

**Tech Stack:** C#, .NET 9, WinUI 3 (Windows App SDK) — matching the rest of DuoCompanion, **not** the WPF/.NET 8 stack originally suggested in `docs/snap-development-plan.md`'s "Technical Stack" section (superseded by the "combine into DuoCompanion" decision). Raw Win32 P/Invoke for window tracking/manipulation (`user32.dll`), consistent with the rest of `DuoCompanion.Services.Win32.NativeMethods`. xUnit for pure-logic unit tests (matching the existing convention: only `DuoCompanion.Core` plain-logic code is unit-tested; Win32/WinUI-interacting services are not, since there's no Windows test runner in this dev environment).

## Global Constraints

- **Do NOT run `git commit`.** Stage every change with `git add` and stop there — the user commits everything themselves. This overrides any "Commit" step text below; wherever a step says "Commit," it means `git add` only.
- No `dotnet`/Windows toolchain is available in the implementation environment (macOS, no Windows). "Run tests" steps describe the command to run and the expected result for when the user builds on their Windows machine — implementers cannot execute them locally and must instead carefully re-read their code against the expected behavior before marking a step done.
- Follow existing codebase conventions exactly: sealed classes, `internal` `NativeMethods` P/Invokes grouped by feature with a `// --- Feature (context) ---` comment header, constructor DI via `ILogger<T>` + interfaces, singleton service registration in `App.xaml.cs`'s `BuildServices()`, no XML doc comments on interface members (see `IWindowManagerService.cs` for the house style), default to no inline comments unless documenting a non-obvious Win32/threading quirk.
- All new cross-thread event handlers (Win32 hooks/timers fire off the UI thread) must be documented as such at the point they're raised, matching the existing comment on `UiAutomationService.OnFocusChanged`.

---

### Task 1: Core hinge/span math

**Files:**
- Create: `src/DuoCompanion.Core/Models/HingeZone.cs`
- Create: `src/DuoCompanion.Core/Models/SpanTarget.cs`
- Create: `src/DuoCompanion.Core/Snap/HingeCalculator.cs`
- Test: `tests/DuoCompanion.Tests/Snap/HingeCalculatorTests.cs`

**Interfaces:**
- Consumes: `DuoCompanion.Core.Models.DisplayInfo` (existing — `Index, DeviceName, X, Y, Width, Height, IsPrimary`, plus computed `IsSecondary`).
- Produces: `HingeZone(DisplayInfo DisplayA, DisplayInfo DisplayB, bool IsVertical, int ActivationCenter, int ActivationHalfWidth)` with `bool Contains(int x, int y)`. `SpanTarget(int Left, int Top, int Width, int Height)`. `HingeCalculator.ComputeHingeZone(IReadOnlyList<DisplayInfo> displays, int activationHalfWidth = HingeCalculator.DefaultActivationHalfWidth) : HingeZone?` and `HingeCalculator.ComputeSpanTarget(DisplayInfo a, DisplayInfo b) : SpanTarget`. These are the exact names/signatures every later task in this plan calls.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/DuoCompanion.Tests/Snap/HingeCalculatorTests.cs
using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Xunit;

namespace DuoCompanion.Tests.Snap;

public sealed class HingeCalculatorTests
{
    private static readonly DisplayInfo LeftDisplay =
        new(0, "LEFT", X: 0, Y: 0, Width: 1350, Height: 1800, IsPrimary: true);

    private static readonly DisplayInfo RightDisplay =
        new(1, "RIGHT", X: 1350, Y: 0, Width: 1350, Height: 1800, IsPrimary: false);

    private static readonly DisplayInfo TopDisplay =
        new(0, "TOP", X: 0, Y: 0, Width: 1800, Height: 1350, IsPrimary: true);

    private static readonly DisplayInfo BottomDisplay =
        new(1, "BOTTOM", X: 0, Y: 1350, Width: 1800, Height: 1350, IsPrimary: false);

    [Fact]
    public void ComputeHingeZone_returns_null_for_single_display()
    {
        var result = HingeCalculator.ComputeHingeZone(new[] { LeftDisplay });
        Assert.Null(result);
    }

    [Fact]
    public void ComputeHingeZone_detects_vertical_hinge_for_side_by_side_displays()
    {
        var zone = HingeCalculator.ComputeHingeZone(new[] { LeftDisplay, RightDisplay });

        Assert.NotNull(zone);
        Assert.True(zone!.IsVertical);
        Assert.Equal(1350, zone.ActivationCenter);
        Assert.Equal(30, zone.ActivationHalfWidth);
    }

    [Fact]
    public void ComputeHingeZone_detects_horizontal_hinge_for_stacked_displays()
    {
        var zone = HingeCalculator.ComputeHingeZone(new[] { TopDisplay, BottomDisplay });

        Assert.NotNull(zone);
        Assert.False(zone!.IsVertical);
        Assert.Equal(1350, zone.ActivationCenter);
    }

    [Fact]
    public void ComputeHingeZone_accepts_displays_in_either_order()
    {
        var zone = HingeCalculator.ComputeHingeZone(new[] { RightDisplay, LeftDisplay });

        Assert.NotNull(zone);
        Assert.Equal(1350, zone.ActivationCenter);
    }

    [Fact]
    public void HingeZone_Contains_is_true_inside_vertical_activation_band()
    {
        var zone = new HingeZone(LeftDisplay, RightDisplay, IsVertical: true, ActivationCenter: 1350, ActivationHalfWidth: 30);

        Assert.True(zone.Contains(1350, 900));
        Assert.True(zone.Contains(1325, 900));
        Assert.True(zone.Contains(1380, 900));
        Assert.False(zone.Contains(1300, 900));
        Assert.False(zone.Contains(1400, 900));
    }

    [Fact]
    public void HingeZone_Contains_is_true_inside_horizontal_activation_band()
    {
        var zone = new HingeZone(TopDisplay, BottomDisplay, IsVertical: false, ActivationCenter: 1350, ActivationHalfWidth: 30);

        Assert.True(zone.Contains(900, 1350));
        Assert.False(zone.Contains(900, 1200));
    }

    [Fact]
    public void ComputeSpanTarget_unions_both_displays_side_by_side()
    {
        var target = HingeCalculator.ComputeSpanTarget(LeftDisplay, RightDisplay);

        Assert.Equal(0, target.Left);
        Assert.Equal(0, target.Top);
        Assert.Equal(2700, target.Width);
        Assert.Equal(1800, target.Height);
    }

    [Fact]
    public void ComputeSpanTarget_unions_both_displays_stacked()
    {
        var target = HingeCalculator.ComputeSpanTarget(TopDisplay, BottomDisplay);

        Assert.Equal(0, target.Left);
        Assert.Equal(0, target.Top);
        Assert.Equal(1800, target.Width);
        Assert.Equal(2700, target.Height);
    }

    [Fact]
    public void ComputeSpanTarget_is_order_independent()
    {
        var a = HingeCalculator.ComputeSpanTarget(LeftDisplay, RightDisplay);
        var b = HingeCalculator.ComputeSpanTarget(RightDisplay, LeftDisplay);

        Assert.Equal(a, b);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run (on Windows, with the .NET 9 SDK): `dotnet test tests/DuoCompanion.Tests/DuoCompanion.Tests.csproj --filter FullyQualifiedName~HingeCalculatorTests`
Expected: build FAILS — `HingeZone`, `SpanTarget`, `HingeCalculator` don't exist yet.

- [ ] **Step 3: Create `HingeZone`**

```csharp
// src/DuoCompanion.Core/Models/HingeZone.cs
namespace DuoCompanion.Core.Models;

public sealed record HingeZone(
    DisplayInfo DisplayA,
    DisplayInfo DisplayB,
    bool IsVertical,
    int ActivationCenter,
    int ActivationHalfWidth)
{
    public bool Contains(int x, int y) =>
        IsVertical
            ? x >= ActivationCenter - ActivationHalfWidth && x <= ActivationCenter + ActivationHalfWidth
            : y >= ActivationCenter - ActivationHalfWidth && y <= ActivationCenter + ActivationHalfWidth;
}
```

- [ ] **Step 4: Create `SpanTarget`**

```csharp
// src/DuoCompanion.Core/Models/SpanTarget.cs
namespace DuoCompanion.Core.Models;

public sealed record SpanTarget(int Left, int Top, int Width, int Height);
```

- [ ] **Step 5: Create `HingeCalculator`**

```csharp
// src/DuoCompanion.Core/Snap/HingeCalculator.cs
using DuoCompanion.Core.Models;

namespace DuoCompanion.Core.Snap;

public static class HingeCalculator
{
    public const int DefaultActivationHalfWidth = 30;

    public static HingeZone? ComputeHingeZone(
        IReadOnlyList<DisplayInfo> displays, int activationHalfWidth = DefaultActivationHalfWidth)
    {
        if (displays.Count < 2) return null;

        var ordered = displays.OrderBy(d => d.X).ThenBy(d => d.Y).ToList();
        var a = ordered[0];
        var b = ordered[1];

        var isVertical = Math.Abs(a.X - b.X) >= Math.Abs(a.Y - b.Y);

        var activationCenter = isVertical
            ? (a.X + a.Width + b.X) / 2
            : (a.Y + a.Height + b.Y) / 2;

        return new HingeZone(a, b, isVertical, activationCenter, activationHalfWidth);
    }

    public static SpanTarget ComputeSpanTarget(DisplayInfo a, DisplayInfo b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new SpanTarget(left, top, right - left, bottom - top);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/DuoCompanion.Tests/DuoCompanion.Tests.csproj --filter FullyQualifiedName~HingeCalculatorTests`
Expected: PASS (10 tests).

- [ ] **Step 7: Stage (do NOT commit)**

```bash
git add src/DuoCompanion.Core/Models/HingeZone.cs src/DuoCompanion.Core/Models/SpanTarget.cs src/DuoCompanion.Core/Snap/HingeCalculator.cs tests/DuoCompanion.Tests/Snap/HingeCalculatorTests.cs
```

---

### Task 2: Hinge topology service

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/IHingeTopologyService.cs`
- Create: `src/DuoCompanion.Services/Snap/HingeTopologyService.cs`

**Interfaces:**
- Consumes: `IDisplayService.GetAllDisplays() : IReadOnlyList<DisplayInfo>` (existing, `DuoCompanion.Contracts.Services`). `IWindowManagerService.DisplayConfigurationChanged : EventHandler?` (existing). `HingeCalculator.ComputeHingeZone` (Task 1).
- Produces: `IHingeTopologyService` with `HingeZone? CurrentHinge { get; }`, `event EventHandler? TopologyChanged`, `void Start()`, `void Stop()`. Task 6 (coordinator) calls `Start()`/`Stop()` and reads `CurrentHinge`.

- [ ] **Step 1: Create the interface**

```csharp
// src/DuoCompanion.Contracts/Services/IHingeTopologyService.cs
using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface IHingeTopologyService
{
    HingeZone? CurrentHinge { get; }
    event EventHandler? TopologyChanged;
    void Start();
    void Stop();
}
```

- [ ] **Step 2: Implement it**

```csharp
// src/DuoCompanion.Services/Snap/HingeTopologyService.cs
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Core.Snap;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

public sealed class HingeTopologyService : IHingeTopologyService, IDisposable
{
    private readonly IDisplayService _display;
    private readonly IWindowManagerService _windowManager;
    private readonly ILogger<HingeTopologyService> _logger;

    public HingeZone? CurrentHinge { get; private set; }
    public event EventHandler? TopologyChanged;

    public HingeTopologyService(IDisplayService display, IWindowManagerService windowManager, ILogger<HingeTopologyService> logger)
    {
        _display = display;
        _windowManager = windowManager;
        _logger = logger;
    }

    public void Start()
    {
        Recompute();
        _windowManager.DisplayConfigurationChanged += OnDisplayConfigurationChanged;
    }

    public void Stop()
    {
        _windowManager.DisplayConfigurationChanged -= OnDisplayConfigurationChanged;
    }

    private void OnDisplayConfigurationChanged(object? sender, EventArgs e) => Recompute();

    private void Recompute()
    {
        CurrentHinge = HingeCalculator.ComputeHingeZone(_display.GetAllDisplays());
        _logger.LogInformation("Hinge topology recomputed: {Hinge}", CurrentHinge);
        TopologyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 3: Stage (do NOT commit)**

```bash
git add src/DuoCompanion.Contracts/Services/IHingeTopologyService.cs src/DuoCompanion.Services/Snap/HingeTopologyService.cs
```

---

### Task 3: Window drag tracking service

**Files:**
- Modify: `src/DuoCompanion.Services/Win32/NativeMethods.cs` (append a new region)
- Create: `src/DuoCompanion.Contracts/Services/IWindowTrackerService.cs`
- Create: `src/DuoCompanion.Services/Snap/WindowTrackerService.cs`

**Interfaces:**
- Consumes: existing `NativeMethods.RECT`, `NativeMethods.WinEventProc`, `NativeMethods.SetWinEventHook`, `NativeMethods.UnhookWinEvent`, `NativeMethods.WINEVENT_OUTOFCONTEXT` (all in `src/DuoCompanion.Services/Win32/NativeMethods.cs`, unchanged).
- Produces: `WindowDragEventArgs(IntPtr hwnd, int left, int top, int width, int height)` with `Hwnd, Left, Top, Width, Height, CenterX, CenterY` properties. `IWindowTrackerService` with `event EventHandler<WindowDragEventArgs>? DragStarted/DragMoved/DragEnded`, `void Start(IntPtr hostHwnd)`, `void Stop()`. Task 6 (coordinator) subscribes to all three events and calls `Start(hostHwnd)`/`Stop()`.
- **Threading note for the next task's author**: `DragStarted`/`DragMoved`/`DragEnded` fire off the UI thread (`DragMoved` from a `System.Threading.Timer` callback, the other two from a raw `WinEventProc`). Any consumer touching WinUI objects must marshal via `DispatcherQueue.TryEnqueue`.

- [ ] **Step 1: Add window-tracking P/Invokes to `NativeMethods.cs`**

Append this new region at the end of the file, immediately before the closing `}` of the `NativeMethods` class (i.e. after the existing `// --- App/tray icon ...` region that ends with `DestroyIcon`):

```csharp
    // --- Window tracking / drag detection (DuoSnap) ---

    internal const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
    internal const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    internal static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    internal static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);
```

`RECT` is already declared earlier in this file (Display enumeration region) and is reused here as-is — do not redeclare it.

- [ ] **Step 2: Create the interface (with its event-args type, matching the existing precedent of `MouseButton` living inside `IMouseService.cs`)**

```csharp
// src/DuoCompanion.Contracts/Services/IWindowTrackerService.cs
namespace DuoCompanion.Contracts.Services;

public sealed class WindowDragEventArgs : EventArgs
{
    public IntPtr Hwnd { get; }
    public int Left { get; }
    public int Top { get; }
    public int Width { get; }
    public int Height { get; }

    public WindowDragEventArgs(IntPtr hwnd, int left, int top, int width, int height)
    {
        Hwnd = hwnd;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
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
```

- [ ] **Step 3: Implement it**

```csharp
// src/DuoCompanion.Services/Snap/WindowTrackerService.cs
using DuoCompanion.Contracts.Services;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

public sealed class WindowTrackerService : IWindowTrackerService, IDisposable
{
    private const int PollIntervalMs = 30;
    private static readonly string[] IgnoredClassNames =
    {
        "Shell_TrayWnd", "Progman", "WorkerW", "Windows.UI.Core.CoreWindow"
    };

    private readonly ILogger<WindowTrackerService> _logger;
    private IntPtr _hostHwnd;
    private IntPtr _draggedHwnd;
    private NativeMethods.WinEventProc? _startHookProc;
    private NativeMethods.WinEventProc? _endHookProc;
    private IntPtr _startHook;
    private IntPtr _endHook;
    private Timer? _pollTimer;

    public event EventHandler<WindowDragEventArgs>? DragStarted;
    public event EventHandler<WindowDragEventArgs>? DragMoved;
    public event EventHandler<WindowDragEventArgs>? DragEnded;

    public WindowTrackerService(ILogger<WindowTrackerService> logger) => _logger = logger;

    public void Start(IntPtr hostHwnd)
    {
        _hostHwnd = hostHwnd;
        _startHookProc = OnMoveSizeStart;
        _endHookProc = OnMoveSizeEnd;

        _startHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MOVESIZESTART, NativeMethods.EVENT_SYSTEM_MOVESIZESTART,
            IntPtr.Zero, _startHookProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _endHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_MOVESIZEEND, NativeMethods.EVENT_SYSTEM_MOVESIZEEND,
            IntPtr.Zero, _endHookProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        _logger.LogInformation("Window drag tracking started");
    }

    public void Stop()
    {
        StopPolling();
        if (_startHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_startHook); _startHook = IntPtr.Zero; }
        if (_endHook != IntPtr.Zero) { NativeMethods.UnhookWinEvent(_endHook); _endHook = IntPtr.Zero; }
        _startHookProc = null;
        _endHookProc = null;
    }

    // Fires off the UI thread — raw WinEventProc callback.
    private void OnMoveSizeStart(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        if (!IsTrackable(hwnd)) return;
        if (!TryGetRect(hwnd, out var args)) return;

        _draggedHwnd = hwnd;
        DragStarted?.Invoke(this, args);
        StartPolling();
    }

    // Fires off the UI thread — raw WinEventProc callback.
    private void OnMoveSizeEnd(IntPtr hook, uint @event, IntPtr hwnd,
        int idObject, int idChild, uint idThread, uint dwTime)
    {
        if (hwnd != _draggedHwnd) return;

        StopPolling();
        if (TryGetRect(hwnd, out var args))
            DragEnded?.Invoke(this, args);

        _draggedHwnd = IntPtr.Zero;
    }

    // Fires off the UI thread — System.Threading.Timer callback.
    private void StartPolling()
    {
        _pollTimer = new Timer(_ =>
        {
            var hwnd = _draggedHwnd;
            if (hwnd == IntPtr.Zero) return;
            if (TryGetRect(hwnd, out var args))
                DragMoved?.Invoke(this, args);
        }, null, PollIntervalMs, PollIntervalMs);
    }

    private void StopPolling()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    private bool IsTrackable(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == _hostHwnd) return false;
        if (!NativeMethods.IsWindowVisible(hwnd)) return false;
        if (NativeMethods.IsIconic(hwnd)) return false;

        var className = new System.Text.StringBuilder(256);
        NativeMethods.GetClassName(hwnd, className, className.Capacity);
        return Array.IndexOf(IgnoredClassNames, className.ToString()) < 0;
    }

    private static bool TryGetRect(IntPtr hwnd, out WindowDragEventArgs args)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            args = null!;
            return false;
        }

        args = new WindowDragEventArgs(hwnd, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return true;
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 4: Stage (do NOT commit)**

```bash
git add src/DuoCompanion.Services/Win32/NativeMethods.cs src/DuoCompanion.Contracts/Services/IWindowTrackerService.cs src/DuoCompanion.Services/Snap/WindowTrackerService.cs
```

---

### Task 4: Window span/restore service

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/IWindowSpanService.cs`
- Create: `src/DuoCompanion.Services/Snap/WindowSpanService.cs`

**Interfaces:**
- Consumes: `NativeMethods.GetWindowRect`, `NativeMethods.SetWindowPos`, `NativeMethods.SWP_NOZORDER`, `NativeMethods.SWP_NOACTIVATE` (all existing in `NativeMethods.cs`). `SpanTarget` (Task 1).
- Produces: `IWindowSpanService` with `bool IsSpanned(IntPtr hwnd)`, `void Span(IntPtr hwnd, SpanTarget target)`, `void Restore(IntPtr hwnd)`. Task 6 (coordinator) calls all three.

- [ ] **Step 1: Create the interface**

```csharp
// src/DuoCompanion.Contracts/Services/IWindowSpanService.cs
using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface IWindowSpanService
{
    bool IsSpanned(IntPtr hwnd);
    void Span(IntPtr hwnd, SpanTarget target);
    void Restore(IntPtr hwnd);
}
```

- [ ] **Step 2: Implement it**

```csharp
// src/DuoCompanion.Services/Snap/WindowSpanService.cs
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using DuoCompanion.Services.Win32;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

public sealed class WindowSpanService : IWindowSpanService
{
    private readonly Dictionary<IntPtr, SpanTarget> _originalRects = new();
    private readonly ILogger<WindowSpanService> _logger;

    public WindowSpanService(ILogger<WindowSpanService> logger) => _logger = logger;

    public bool IsSpanned(IntPtr hwnd) => _originalRects.ContainsKey(hwnd);

    public void Span(IntPtr hwnd, SpanTarget target)
    {
        if (!_originalRects.ContainsKey(hwnd))
        {
            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return;
            _originalRects[hwnd] = new SpanTarget(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        Move(hwnd, target);
        _logger.LogInformation("Spanned window {Hwnd} to {Target}", hwnd, target);
    }

    public void Restore(IntPtr hwnd)
    {
        if (!_originalRects.TryGetValue(hwnd, out var original)) return;

        Move(hwnd, original);
        _originalRects.Remove(hwnd);
        _logger.LogInformation("Restored window {Hwnd} to {Original}", hwnd, original);
    }

    private static void Move(IntPtr hwnd, SpanTarget target) =>
        NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero,
            target.Left, target.Top, target.Width, target.Height,
            (uint)(NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE));
}
```

- [ ] **Step 3: Stage (do NOT commit)**

```bash
git add src/DuoCompanion.Contracts/Services/IWindowSpanService.cs src/DuoCompanion.Services/Snap/WindowSpanService.cs
```

---

### Task 5: Overlay window support in `WindowManagerService`

**Files:**
- Modify: `src/DuoCompanion.Services/Win32/NativeMethods.cs` (append two constants)
- Modify: `src/DuoCompanion.Contracts/Services/IWindowManagerService.cs`
- Modify: `src/DuoCompanion.Services/Window/WindowManagerService.cs`

**Interfaces:**
- Consumes: existing `NativeMethods.GetWindowLongPtr/SetWindowLongPtr/GWL_EXSTYLE/WS_EX_NOACTIVATE/SetWindowPos/HWND_TOPMOST/SWP_NOACTIVATE`.
- Produces: two new `IWindowManagerService` members — `void MakeWindowClickThrough(IntPtr hwnd)`, `void SetWindowBounds(IntPtr hwnd, int left, int top, int width, int height)`. Task 7 (`SpanOverlayWindow`, App project) calls both; it's the only consumer.

- [ ] **Step 1: Add click-through style constants to `NativeMethods.cs`**

Add these two lines into the existing `// --- Window positioning + display change hook (Task 1) ---` region, right after the existing `internal const nint WS_EX_NOACTIVATE = 0x08000000;` line:

```csharp
    internal const nint WS_EX_TRANSPARENT = 0x00000020;
    internal const nint WS_EX_LAYERED = 0x00080000;
```

- [ ] **Step 2: Add the two methods to `IWindowManagerService.cs`**

Current file (`src/DuoCompanion.Contracts/Services/IWindowManagerService.cs`):

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

Replace it with:

```csharp
namespace DuoCompanion.Contracts.Services;

public interface IWindowManagerService
{
    void MakeWindowNonActivating(IntPtr hwnd);
    void PositionCompanionWindow(IntPtr hwnd);
    void HideCompanionWindow(IntPtr hwnd);
    void ShowCompanionWindow(IntPtr hwnd);
    void MakeWindowClickThrough(IntPtr hwnd);
    void SetWindowBounds(IntPtr hwnd, int left, int top, int width, int height);
    event EventHandler DisplayConfigurationChanged;
    void StartMonitoring(IntPtr hostHwnd);
    void StopMonitoring();
}
```

- [ ] **Step 3: Implement both in `WindowManagerService.cs`**

Add these two methods right after the existing `MakeWindowNonActivating` method (which ends with the `_logger.LogInformation("Companion window configured to preserve the active application");` line and closing brace):

```csharp
    public void MakeWindowClickThrough(IntPtr hwnd)
    {
        var extendedStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(
            hwnd,
            NativeMethods.GWL_EXSTYLE,
            extendedStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE);
    }

    public void SetWindowBounds(IntPtr hwnd, int left, int top, int width, int height)
    {
        NativeMethods.SetWindowPos(
            hwnd, NativeMethods.HWND_TOPMOST,
            left, top, width, height,
            (uint)NativeMethods.SWP_NOACTIVATE);
    }
```

- [ ] **Step 4: Stage (do NOT commit)**

```bash
git add src/DuoCompanion.Services/Win32/NativeMethods.cs src/DuoCompanion.Contracts/Services/IWindowManagerService.cs src/DuoCompanion.Services/Window/WindowManagerService.cs
```

---

### Task 6: Auto-span coordinator + settings toggle

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/IAutoSpanCoordinatorService.cs`
- Create: `src/DuoCompanion.Services/Snap/AutoSpanCoordinatorService.cs`
- Modify: `src/DuoCompanion.Core/Models/AppSettings.cs`
- Modify: `tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs`

**Interfaces:**
- Consumes: `IWindowTrackerService` (Task 3), `IHingeTopologyService` (Task 2), `IWindowSpanService` (Task 4), `HingeCalculator.ComputeSpanTarget` (Task 1), `ISettingsService.Current : AppSettings` (existing).
- Produces: `IAutoSpanCoordinatorService` with `event EventHandler<SpanCandidateEventArgs>? SpanCandidateEntered`, `event EventHandler? SpanCandidateExited`, `void Start(IntPtr hostHwnd)`, `void Stop()`. `SpanCandidateEventArgs(SpanTarget Target)`. `AppSettings.AutoSpanEnabled : bool` (default `true`). Task 8 (`App.xaml.cs`) calls `Start`/`Stop` and subscribes to both events to drive the overlay window from Task 7.
- **Threading note for Task 8's author**: `SpanCandidateEntered`/`SpanCandidateExited` fire off the UI thread (relayed from `IWindowTrackerService`'s events). Marshal via `DispatcherQueue.TryEnqueue` before touching any WinUI object.

- [ ] **Step 1: Add the setting**

In `src/DuoCompanion.Core/Models/AppSettings.cs`, add one line after the existing `AutoHideMode` property:

```csharp
    public bool AutoSpanEnabled { get; set; } = true;
```

- [ ] **Step 2: Write the failing tests**

Add to `tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs`, inside the existing `AppSettingsTests` class, after the `AutoHideMode_is_mutable` test:

```csharp
    [Fact]
    public void Default_auto_span_enabled_is_true()
    {
        var settings = new AppSettings();
        Assert.True(settings.AutoSpanEnabled);
    }

    [Fact]
    public void AutoSpanEnabled_is_mutable()
    {
        var settings = new AppSettings();
        settings.AutoSpanEnabled = false;
        Assert.False(settings.AutoSpanEnabled);
    }
```

- [ ] **Step 3: Run tests to verify they pass** (Step 1 already makes them pass — this just confirms no typo)

Run: `dotnet test tests/DuoCompanion.Tests/DuoCompanion.Tests.csproj --filter FullyQualifiedName~AppSettingsTests`
Expected: PASS (8 tests total in this class).

- [ ] **Step 4: Create the coordinator interface**

```csharp
// src/DuoCompanion.Contracts/Services/IAutoSpanCoordinatorService.cs
using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public sealed class SpanCandidateEventArgs : EventArgs
{
    public SpanTarget Target { get; }
    public SpanCandidateEventArgs(SpanTarget target) => Target = target;
}

public interface IAutoSpanCoordinatorService
{
    event EventHandler<SpanCandidateEventArgs>? SpanCandidateEntered;
    event EventHandler? SpanCandidateExited;
    void Start(IntPtr hostHwnd);
    void Stop();
}
```

- [ ] **Step 5: Implement it**

```csharp
// src/DuoCompanion.Services/Snap/AutoSpanCoordinatorService.cs
using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Snap;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Snap;

public sealed class AutoSpanCoordinatorService : IAutoSpanCoordinatorService, IDisposable
{
    private readonly IWindowTrackerService _tracker;
    private readonly IHingeTopologyService _hinge;
    private readonly IWindowSpanService _span;
    private readonly ISettingsService _settings;
    private readonly ILogger<AutoSpanCoordinatorService> _logger;
    private bool _isCandidate;

    public event EventHandler<SpanCandidateEventArgs>? SpanCandidateEntered;
    public event EventHandler? SpanCandidateExited;

    public AutoSpanCoordinatorService(
        IWindowTrackerService tracker,
        IHingeTopologyService hinge,
        IWindowSpanService span,
        ISettingsService settings,
        ILogger<AutoSpanCoordinatorService> logger)
    {
        _tracker = tracker;
        _hinge = hinge;
        _span = span;
        _settings = settings;
        _logger = logger;
    }

    public void Start(IntPtr hostHwnd)
    {
        _hinge.Start();
        _tracker.DragStarted += OnDragStarted;
        _tracker.DragMoved += OnDragMoved;
        _tracker.DragEnded += OnDragEnded;
        _tracker.Start(hostHwnd);
        _logger.LogInformation("Auto-span coordinator started");
    }

    public void Stop()
    {
        _tracker.Stop();
        _tracker.DragStarted -= OnDragStarted;
        _tracker.DragMoved -= OnDragMoved;
        _tracker.DragEnded -= OnDragEnded;
        _hinge.Stop();
    }

    private void OnDragStarted(object? sender, WindowDragEventArgs e)
    {
        if (!_settings.Current.AutoSpanEnabled) return;
        if (_span.IsSpanned(e.Hwnd)) _span.Restore(e.Hwnd);
    }

    private void OnDragMoved(object? sender, WindowDragEventArgs e)
    {
        if (!_settings.Current.AutoSpanEnabled) return;

        var hinge = _hinge.CurrentHinge;
        var inZone = hinge is not null && hinge.Contains(e.CenterX, e.CenterY);

        if (inZone && !_isCandidate)
        {
            _isCandidate = true;
            var target = HingeCalculator.ComputeSpanTarget(hinge!.DisplayA, hinge.DisplayB);
            SpanCandidateEntered?.Invoke(this, new SpanCandidateEventArgs(target));
        }
        else if (!inZone && _isCandidate)
        {
            _isCandidate = false;
            SpanCandidateExited?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDragEnded(object? sender, WindowDragEventArgs e)
    {
        if (!_settings.Current.AutoSpanEnabled) return;
        if (!_isCandidate) return;

        _isCandidate = false;
        SpanCandidateExited?.Invoke(this, EventArgs.Empty);

        var hinge = _hinge.CurrentHinge;
        if (hinge is null) return;

        var target = HingeCalculator.ComputeSpanTarget(hinge.DisplayA, hinge.DisplayB);
        _span.Span(e.Hwnd, target);
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 6: Stage (do NOT commit)**

```bash
git add src/DuoCompanion.Core/Models/AppSettings.cs tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs src/DuoCompanion.Contracts/Services/IAutoSpanCoordinatorService.cs src/DuoCompanion.Services/Snap/AutoSpanCoordinatorService.cs
```

---

### Task 7: Span preview overlay window

**Files:**
- Create: `src/DuoCompanion.App/SpanOverlayWindow.xaml`
- Create: `src/DuoCompanion.App/SpanOverlayWindow.xaml.cs`

**Interfaces:**
- Consumes: `IWindowManagerService.MakeWindowClickThrough(IntPtr)` and `SetWindowBounds(IntPtr, int, int, int, int)` (Task 5).
- Produces: `SpanOverlayWindow(IWindowManagerService windowManager)` with `void ShowAt(int left, int top, int width, int height)` and `void HideOverlay()`. Task 8 (`App.xaml.cs`) constructs one instance and calls both methods from the coordinator's events.

- [ ] **Step 1: Create the XAML**

```xml
<!-- src/DuoCompanion.App/SpanOverlayWindow.xaml -->
<Window
    x:Class="DuoCompanion.App.SpanOverlayWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="DuoSnap">

    <Grid Background="#4000A2FF">
        <TextBlock Text="Drop to Span"
                   HorizontalAlignment="Center" VerticalAlignment="Center"
                   FontSize="28" Foreground="White"/>
    </Grid>
</Window>
```

- [ ] **Step 2: Create the code-behind**

```csharp
// src/DuoCompanion.App/SpanOverlayWindow.xaml.cs
using DuoCompanion.Contracts.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace DuoCompanion.App;

public sealed partial class SpanOverlayWindow : Window
{
    private readonly IWindowManagerService _windowManager;

    public SpanOverlayWindow(IWindowManagerService windowManager)
    {
        _windowManager = windowManager;
        InitializeComponent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _windowManager.MakeWindowClickThrough(WindowNative.GetWindowHandle(this));
    }

    public void ShowAt(int left, int top, int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        _windowManager.SetWindowBounds(hwnd, left, top, width, height);
        AppWindow.Show();
    }

    public void HideOverlay() => AppWindow.Hide();
}
```

- [ ] **Step 3: Stage (do NOT commit)**

```bash
git add src/DuoCompanion.App/SpanOverlayWindow.xaml src/DuoCompanion.App/SpanOverlayWindow.xaml.cs
```

---

### Task 8: Wire everything into `App.xaml.cs`, add the Settings toggle, update the README

**Files:**
- Modify: `src/DuoCompanion.App/App.xaml.cs`
- Modify: `src/DuoCompanion.App/Pages/SettingsPage.xaml`
- Modify: `src/DuoCompanion.App/Pages/SettingsPage.xaml.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes everything produced by Tasks 2–7. This is the final integration task — nothing later depends on it.

- [ ] **Step 1: Register the new services in `App.xaml.cs`**

In `BuildServices()`, add these four lines right after the existing `services.AddSingleton<ITrayIconService, TrayIconService>();` line:

```csharp
        services.AddSingleton<IHingeTopologyService, HingeTopologyService>();
        services.AddSingleton<IWindowTrackerService, WindowTrackerService>();
        services.AddSingleton<IWindowSpanService, WindowSpanService>();
        services.AddSingleton<IAutoSpanCoordinatorService, AutoSpanCoordinatorService>();
```

Add this `using` alongside the existing `using DuoCompanion.Services.Tray;` line:

```csharp
using DuoCompanion.Services.Snap;
```

- [ ] **Step 2: Start the coordinator and wire the overlay in `OnLaunched`**

The current `OnLaunched` body (after the Task 5/tray work earlier this session) ends with:

```csharp
            var tray = Services.GetRequiredService<ITrayIconService>();
            tray.ToggleVisibilityRequested += (_, _) => CompanionWindow?.ToggleVisibility();
            tray.QuitRequested += (_, _) => Quit();
            tray.Start();
            WriteStartupLog("Tray icon started.");
        }
        catch (Exception ex)
```

Insert this block right before the closing `}` of the `try` block (i.e. right after `WriteStartupLog("Tray icon started.");`, still before `catch`):

```csharp

            var overlay = new SpanOverlayWindow(Services.GetRequiredService<IWindowManagerService>());
            var autoSpan = Services.GetRequiredService<IAutoSpanCoordinatorService>();
            autoSpan.SpanCandidateEntered += (_, e) => overlay.DispatcherQueue.TryEnqueue(() =>
                overlay.ShowAt(e.Target.Left, e.Target.Top, e.Target.Width, e.Target.Height));
            autoSpan.SpanCandidateExited += (_, _) => overlay.DispatcherQueue.TryEnqueue(overlay.HideOverlay);
            autoSpan.Start(WindowNative.GetWindowHandle(CompanionWindow));
            WriteStartupLog("Auto-span coordinator started.");
```

This requires `WindowNative` — add this `using` alongside the existing usings at the top of the file:

```csharp
using WinRT.Interop;
```

- [ ] **Step 3: Stop the coordinator on quit**

The current `Quit()` method:

```csharp
    public static void Quit()
    {
        Services.GetRequiredService<ITrayIconService>().Stop();
        Application.Current.Exit();
    }
```

Replace it with:

```csharp
    public static void Quit()
    {
        Services.GetRequiredService<ITrayIconService>().Stop();
        Services.GetRequiredService<IAutoSpanCoordinatorService>().Stop();
        Application.Current.Exit();
    }
```

- [ ] **Step 4: Add the Settings toggle — XAML**

In `src/DuoCompanion.App/Pages/SettingsPage.xaml`, insert this new section right after the existing `<!-- Auto-hide -->` `StackPanel` block and before the `<!-- Opacity -->` block:

```xml
            <!-- Auto Span -->
            <StackPanel Spacing="4">
                <TextBlock Text="Dual-Screen Spanning" Style="{StaticResource BodyStrongTextBlockStyle}"/>
                <ToggleSwitch x:Name="AutoSpanToggle" Header="Enable Auto Span" Toggled="OnAutoSpanToggled"/>
            </StackPanel>
```

- [ ] **Step 5: Add the Settings toggle — code-behind**

In `src/DuoCompanion.App/Pages/SettingsPage.xaml.cs`, in `OnLoaded`, add this line right after the existing `AutoHideModeCombo.SelectedIndex = ...;` line:

```csharp
        AutoSpanToggle.IsOn = s.AutoSpanEnabled;
```

Add this new handler method right after the existing `OnAutoHideModeChanged` method:

```csharp
    private void OnAutoSpanToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _settings.Current.AutoSpanEnabled = AutoSpanToggle.IsOn;
        _settings.Save();
    }
```

- [ ] **Step 6: Update the README feature table**

In `README.md`, add this row to the feature table right after the `Single-screen / folded mode` row:

```
| Dual-screen window spanning | New | Drag any window into the hinge between the two displays and release to span it across both. Grab a spanned window's title bar again to restore it. Toggle in Settings → Dual-Screen Spanning (on by default). |
```

- [ ] **Step 7: Stage (do NOT commit)**

```bash
git add src/DuoCompanion.App/App.xaml.cs src/DuoCompanion.App/Pages/SettingsPage.xaml src/DuoCompanion.App/Pages/SettingsPage.xaml.cs README.md
```

---

## What this plan deliberately does not cover

Deferred to follow-up plans, per `docs/snap-development-plan.md`:

- **Phase 8 (orientation edge cases)** beyond what `HingeCalculator`'s vertical/horizontal detection already handles automatically on `DisplayConfigurationChanged`.
- **Phase 9 (touch/pen tuning)** — this plan's drag detection is pointer-agnostic (works via `SetWinEventHook`, not raw touch input), but no touch-specific thresholds/inertia handling.
- **Phase 10 (animation)** — the overlay in Task 7 appears/disappears instantly; no fade.
- **Phase 12 (advanced features)** — snap layouts (70/30 etc.), gestures, per-application profiles, keyboard shortcuts, intelligent recommendations, external-monitor awareness.

Milestone 6 ("Background service — startup integration, settings application, stable performance") is effectively already satisfied by being combined into DuoCompanion, which already has tray-icon background operation and a settings page — Task 8 above is that integration.
