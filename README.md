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
| Touchpad (mouse control) | Works | Drag to move, pinch to scroll, tap/right-tap to click |
| Media controls | Works | Play/Pause, Next/Prev, Volume Up/Down/Mute |
| Settings persistence | Works | Saved to `%LOCALAPPDATA%\DuoCompanion\settings.json` |
| Auto-show keyboard on focus | Partial | Detects Win32 text fields (Notepad, browsers); may miss modern UWP/XAML apps |
| Handwriting recognition | Conditional | Requires Handwriting Recognition optional feature (see below) |
| Single-screen / folded mode | Safe | Logs a warning; no crash — window won't auto-position |

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

---

## Building from Source

See [SETUP.md](SETUP.md) for prerequisites and build instructions.

---

## License

MIT — see [LICENSE](LICENSE).
