# Duo Companion

A WinUI 3 companion app for Microsoft Surface Duo running Windows 11 ARM (DuoWOA).
Occupies the secondary display and provides a virtual keyboard, clipboard manager, touchpad, media controls, handwriting input, and settings.

The app **does not replace Windows** and **does not modify the system touch keyboard** — it's a dedicated companion window that occupies the secondary display, built specifically for Surface Duo's dual-display form factor.

---

## Features

| Feature | Status | Notes |
|---|---|---|
| Window on secondary display | Works | Auto-positions at startup; repositions on display change |
| Virtual keyboard + key injection | Works | Full QWERTY, modifier toggles, arrow/special keys |
| Clipboard history + paste | Works | 50-item history, pin/remove, Ctrl+V injection |
| Media controls | Works | Play/Pause, Next/Prev, Volume Up/Down/Mute |
| Settings persistence | Works | Saved to `%LOCALAPPDATA%\DuoCompanion\settings.json` |
| Auto-show keyboard on focus | Works | Uses UI Automation focus tracking — covers native Win32, UWP, WinUI3, and Chromium/Edge text fields |
| Auto-hide when unfocused | Works | Configurable in Settings → Auto-hide: Off (always visible), Smart (hides only when idle, stays open if you've manually opened Clipboard/Media/etc.), Always (hides on blur from the keyboard; stays open if you're on a manually-opened page when focus moves away) |
| Handwriting recognition | Conditional | Requires Handwriting Recognition optional feature (see below) |
| Single-screen / folded mode | Safe | Logs a warning; no crash — window won't auto-position |
| Dual-screen window spanning (DuoSnap) | New, unverified on hardware | Drag any window into the hinge between the two displays and release to span it across both, or use per-app profiles/hotkeys for explicit layouts. See [DuoSnap](#duosnap-dual-screen-window-management) below. |

---

## DuoSnap: Dual-Screen Window Management

DuoSnap treats the hinge between the two Surface Duo displays as its own
interactive zone. Windows Snap keeps working exactly as before everywhere
else — DuoSnap only intercepts a drag that ends inside the hinge activation
region (default: 30 px each side of center, configurable).

### Windows Snap integration modes (Settings → Windows Snap Integration)

| Mode | Behavior |
|---|---|
| Extend Windows Snap (default) | DuoSnap only offers hinge-drag spanning. Ordinary Windows Snap (edges, corners, Snap Layouts, `Win`+arrow) is untouched. |
| Replace Windows Snap | Adds explicit layout commands (hotkeys, per-app profiles) on top of hinge-drag spanning. Still never changes any Windows Snap OS setting itself. |
| Disabled manually | For users who have already turned Windows Snap off themselves. DuoSnap switches to layout-commands-only (no drag-through auto-span) and requires a one-time confirmation dialog before this mode takes effect. This setting is purely informational — DuoSnap never flips a Windows Snap setting on your behalf, in any mode. |

### Settings surface (Settings → Dual-Screen Spanning)

- **Auto Span** — master on/off for hinge-drag-to-span (on by default).
- **Hinge Activation Half-Width** (5–120 px) — how close to the hinge center a drag must get to register.
- **Span Overlay Opacity** (0.05–0.80) and **Fade Duration** (0–1000 ms) — the preview shown while dragging toward the hinge.
- **Span Dwell Duration** (0–1500 ms) — how long a drag must hover near the hinge (or a title bar must be held over it) before it's treated as confirmed.
- **Restore Original Size** — `OnNextDrag` (default; restores when you next drag the spanned window away) or `Never`.
- **Ignored Applications** — executables that are never auto-spanned.
- **Per-App Layout Profiles** — assign an executable a fixed layout (Left / Right / Span / 70-30 / 30-70), or mark it ignored. A matching profile always wins over the ambient hinge-drag decision.
- **Global Hotkeys** — one binding per layout action (e.g. `Ctrl+Alt+S` for Span), edited as `Modifier+Modifier+Key` text. Changes take effect after restarting Duo Companion (hotkeys are registered once at launch; there is no live re-registration path yet).
- **Launch on startup** — registers/removes a per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value. DuoCompanion only ever touches the single value it created itself — a pre-existing or third-party value under the same name is left alone.

### Other DuoSnap behaviors

- **Title-bar hold gesture**: holding a window's title bar over the hinge for the configured dwell confirms a span even in `WindowsSnapDisabledManually` mode, where ordinary drag-through auto-span is off. It still respects the ignore list and per-app-profile exclusions.
- **Layout suggestions**: a conservative, deterministic suggestion (`LayoutSuggested` event) is raised for a small set of executables (browsers, PDF readers, Explorer) that have no configured profile. This is preview-only — nothing is moved until a caller calls `ApplySuggestedLayout`. No settings-page or in-app UI currently surfaces this suggestion to the user; it exists at the service layer for a future confirmation affordance.
- **External/third monitor safety**: DuoSnap only activates when exactly one display pair shares an edge with matching perpendicular bounds. A second valid pair, or any other ambiguous topology, disables hinge-specific behavior entirely rather than guessing.

### Known limitations

- This entire feature (DuoSnap M5–M7: safe topology detection, settings, layout profiles, hotkeys, and this Settings UI) was built and reviewed on a macOS host with no Windows/.NET toolchain available. Every test in `tests/DuoCompanion.Tests/Snap/` was hand-traced against the implementation rather than compiled and run. **All of it needs a real Windows/Surface Duo verification pass before being trusted** — see [`docs/duosnap-windows-validation.md`](docs/duosnap-windows-validation.md) for the checklist.
- Hotkey edits in Settings require an app restart to take effect.
- Layout suggestions have no UI confirmation affordance yet (service-layer only, see above).

---

## Download and Run

1. Go to [Releases](https://github.com/SE-Terry/Surface-Duo-HSOSK/releases/latest) and download `DuoCompanion-win-arm64.zip`.
2. Extract it anywhere on your Surface Duo (e.g. `Documents\DuoCompanion`).
3. Run `DuoCompanion.exe` — no installer, no build, no admin rights required.

The release is self-contained: keep every file from the extracted folder together, then run the `.exe`. It does not require separate .NET or Windows App Runtime installation.

Before running the app, you can verify the extracted release folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\Test-DuoCompanionRelease.ps1
```

### First-Run Checklist

1. **Handwriting**: Windows Settings → Optional Features → search "Handwriting" → Install.
   If missing, the handwriting page shows "No handwriting recognizer installed" and does nothing else.

2. **Wrong screen**: If the companion window appears on the primary display at first launch, unfold/fold the device once — the display-change event will reposition it automatically.

### Exiting the app

Duo Companion has no title bar or taskbar entry by design (it's a
non-activating overlay, like a system on-screen keyboard). Look for its
icon in the system tray (bottom-right, primary screen) — left-click to
show/hide the window, right-click for a Show/Hide and Quit menu. You can
also quit from Settings → Quit Duo Companion.

---

## Building from Source

See [SETUP.md](SETUP.md) for prerequisites and build instructions.

---

## License

MIT — see [LICENSE](LICENSE).
