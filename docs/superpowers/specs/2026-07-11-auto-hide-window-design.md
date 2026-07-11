# Auto-hide companion window, tray icon, and quit — Design

Status: Approved
Date: 2026-07-11

## Problem

The companion window is currently always visible, topmost, and non-activating.
Focusing a text field navigates its internal `Frame` to `KeyboardPage`; losing
focus navigates back to whatever page the user last manually opened
(`_lastManualPage` in `CompanionWindow`). The window itself never hides, and
there is no title bar, minimize, or close affordance — the only way to exit
the app today is killing the process.

Desired behavior, modeled on Windows' own touch keyboard / Android IME: the
window should be able to disappear entirely when it's not needed and pop back
when a text field gains focus, be configurable per user preference, and be
quittable without Task Manager.

## Goals

- Add an `AutoHideMode` setting with three modes: `Off` (today's behavior),
  `Smart` (hide only when idle/keyboard state, not when a page is manually
  pinned), `Always` (hide on any blur, matching strict IME behavior).
- Add a system tray icon as the manual show/hide/quit entry point — required
  once the window can be hidden with no other pages open, since there is
  currently no way to reach Touchpad/Clipboard/Media/Handwriting/Settings
  without a focused text field.
- Add a "Quit" button to `SettingsPage`.
- The existing "Companion Display" (Right/Left/Auto) setting already covers
  screen selection — no new setting needed there.
- Explicitly out of scope: a visible title bar with minimize/close (rejected
  in favor of keeping the chromeless overlay style; quit is Settings/tray
  only).

## Settings model

`DuoCompanion.Core.Models.AppSettings` gains:

```csharp
public string AutoHideMode { get; set; } = "Smart"; // "Off", "Smart", "Always"
```

`SettingsPage.xaml` gains an "Auto-hide" `ComboBox` (Off / Smart / Always),
following the same pattern as the existing Theme and Default Module combos,
and a "Quit Duo Companion" button below the existing "Reset to Defaults"
button.

## Auto-hide state machine

Reuses the existing `_lastManualPage` field in `CompanionWindow` as the
"is a non-keyboard page manually pinned" signal — no new state beyond the
mode setting itself.

| Mode | On text focus | On text blur |
|---|---|---|
| `Off` | Navigate to `KeyboardPage` (unchanged). Show window if hidden (it never will be in this mode). | Navigate back to `_lastManualPage`. Never hides. |
| `Smart` | Show window if hidden; navigate to `KeyboardPage`. | If `_lastManualPage == typeof(KeyboardPage)` (nothing else pinned), hide the window after the debounce. Otherwise, behave exactly like `Off` (navigate back to `_lastManualPage`, stay visible). |
| `Always` | Show window if hidden; navigate to `KeyboardPage`. | Hide the window after the debounce, regardless of `_lastManualPage`. |

Debounce: a single cancelable `DispatcherTimer` (~200ms) is started on blur
before actually hiding; any subsequent focus event cancels it. This avoids
visible flicker when tabbing rapidly between adjacent fields. Page navigation
(the existing non-hide behavior) is not debounced — only the hide action is.

Startup: the window always starts visible regardless of `AutoHideMode` (only
becomes eligible to hide after the first blur event), so first-run users
aren't left with an invisible app and no discovered tray icon yet.

## Hide/show mechanism

`WindowManagerService` / `NativeMethods` (Win32/):

- New P/Invoke: `ShowWindow(IntPtr hwnd, int nCmdShow)`, constants
  `SW_HIDE = 0`, `SW_SHOWNOACTIVATE = 4`.
- New `IWindowManagerService` methods:
  - `HideCompanionWindow(IntPtr hwnd)` → `ShowWindow(hwnd, SW_HIDE)`.
  - `ShowCompanionWindow(IntPtr hwnd)` → calls `PositionCompanionWindow(hwnd)`
    first (in case the display configuration changed while hidden), then
    `ShowWindow(hwnd, SW_SHOWNOACTIVATE)` (does not steal focus from
    whatever app the user is in, consistent with `MakeWindowNonActivating`).

`CompanionWindow`'s existing `OnTextInputFocused`/`OnTextInputBlurred`
handlers (already dispatched via `DispatcherQueue.TryEnqueue`, so no new
threading concerns) are extended with the state machine above, reading
`_settings.Current.AutoHideMode`.

Chosen over `AppWindow.Hide()`/`Show()` (WinAppSDK's higher-level API) for
consistency with how the rest of `WindowManagerService` already manipulates
the window (`SetWindowPos`, `SetWindowLongPtr`) and more predictable
non-activating reappear semantics.

## Tray icon

New `ITrayIconService` / `TrayIconService`, started in `App.OnLaunched`
alongside `IUiAutomationService.Start()`, disposed on app exit.

- Implemented with `System.Windows.Forms.NotifyIcon`
  (`<UseWindowsForms>true</UseWindowsForms>` added to
  `DuoCompanion.App.csproj`). Chosen over hand-rolled `Shell_NotifyIcon`
  P/Invoke (which would need a hidden window + custom `WndProc` to receive
  tray messages) for lower implementation risk.
- No app icon asset exists in the project today (no `.ico` file, no
  `ApplicationIcon` set in `DuoCompanion.App.csproj`). Add a minimal `.ico`
  file (e.g. `Assets/tray.ico`) for the tray icon; `NotifyIcon.Icon` requires
  a real `System.Drawing.Icon`, not an arbitrary image format. Not setting
  `ApplicationIcon` on the project itself — out of scope here.
- Tooltip: "Duo Companion".
- Left-click: toggle window visibility — shows (position + show) if hidden,
  hides if visible. This is independent of `AutoHideMode` and is the manual
  escape hatch for reaching Touchpad/Clipboard/Media/Handwriting/Settings
  when the window is auto-hidden with nothing focused.
- Right-click context menu: "Show/Hide Duo Companion" (same toggle),
  "Quit" (same handler as the Settings quit button).

## Quit

- `SettingsPage.xaml`: "Quit Duo Companion" button below "Reset to
  Defaults", calls a shared quit path.
- Quit path: dispose the tray icon (required — an undisposed `NotifyIcon`
  leaves a ghost icon in the tray until the user mouses over it) then
  `Application.Current.Exit()`. Both the Settings button and the tray menu's
  "Quit" call the same method so cleanup isn't duplicated.

## Edge cases

- **Single-screen/folded mode**: unaffected. `ShowCompanionWindow` calls
  `PositionCompanionWindow`, which already no-ops safely (logs a warning,
  leaves the window where it is) when fewer than 2 displays are present.
- **Manual tray toggle while hidden by auto-hide, or vice versa**: both
  mechanisms drive the same `HideCompanionWindow`/`ShowCompanionWindow`
  methods, so state never diverges — a subsequent focus event always shows
  the window and navigates to `KeyboardPage` regardless of how it was hidden.

## Verification

This is a Windows-only WinUI3 app; it cannot be built or run from macOS.
Verification is manual, on the Windows machine used for prior builds:

1. Build via `build-release.ps1`, run `DuoCompanion.exe`.
2. For each `AutoHideMode` (Off/Smart/Always): confirm focus/blur behavior
   matches the state machine table above, including the debounce (rapid tab
   between two fields shouldn't flicker).
3. Tray icon: left-click toggle, right-click menu (show/hide, quit), confirm
   no ghost icon remains after quitting.
4. Settings "Quit Duo Companion" button exits cleanly.
5. Hide then show on both single-display and dual-display configurations
   (or simulate via the existing companion-display setting) to confirm
   repositioning still happens correctly on reappear.
