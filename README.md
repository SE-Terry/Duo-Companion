# Duo Companion

A WinUI 3 companion app for Microsoft Surface Duo running Windows 11 ARM (DuoWOA).  
Occupies the secondary display and provides a virtual keyboard, clipboard manager, touchpad, media controls, handwriting input, and settings.

---

## Prerequisites

Install these on the Surface Duo (or any Windows 11 ARM64 machine):

### Option A — Visual Studio (recommended)

- [Visual Studio 2022 17.8+](https://visualstudio.microsoft.com/) with:
  - `.NET Desktop Development` workload
  - `Windows App SDK C# Templates` (Individual Components → search "Windows App SDK")

### Option B — CLI only

```powershell
winget install Microsoft.DotNet.SDK.9
winget install Microsoft.WindowsAppRuntime.1.6
```

---

## Build

### Visual Studio

1. Open `DuoCompanion.sln`
2. Set configuration: **Release | ARM64**
3. **Build → Build Solution** (`Ctrl+Shift+B`)
4. Press `F5` or **Debug → Start Without Debugging**

### Command Line

```powershell
git clone <your-repo-url>
cd Surface-Duo-HSOSK

dotnet build DuoCompanion.sln -c Release -r win-arm64
```

Output:
```
src\DuoCompanion.App\bin\ARM64\Release\net9.0-windows10.0.19041.0\win-arm64\DuoCompanion.exe
```

Run directly — no installer or MSIX required:

```powershell
.\src\DuoCompanion.App\bin\ARM64\Release\net9.0-windows10.0.19041.0\win-arm64\DuoCompanion.exe
```

---

## Tests

```powershell
dotnet test tests\DuoCompanion.Tests\DuoCompanion.Tests.csproj
```

---

## Feature Status

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

## First-Run Checklist

1. **Handwriting**: Windows Settings → Optional Features → search "Handwriting" → Install.  
   If missing, the handwriting page shows "No handwriting recognizer installed" and does nothing else.

2. **Wrong screen**: If the companion window appears on the primary display at first launch, unfold/fold the device once — the display-change event will reposition it automatically.

---

## Project Structure

```
DuoCompanion.sln
├── src/
│   ├── DuoCompanion.App        # WinUI 3 UI — pages, windows, view models
│   ├── DuoCompanion.Core       # Models (DisplayInfo, ClipboardItem, AppSettings)
│   ├── DuoCompanion.Contracts  # Service interfaces (IInputService, IClipboardService, …)
│   └── DuoCompanion.Services   # Implementations — Win32 P/Invoke, ink, settings
└── tests/
    └── DuoCompanion.Tests      # xUnit unit tests (Core + Services)
```

---

## Tech Stack

- C# 13 / .NET 9 / WinUI 3 (Windows App SDK 1.6)
- MVVM — CommunityToolkit.Mvvm 8.3.2
- Win32 P/Invoke — `SetWinEventHook`, `SendInput`, `SetWindowPos`, IAccessible
- `Windows.UI.Input.Inking` — handwriting recognition via `InkStrokeBuilder` + `InkRecognizerContainer`
- `Windows.ApplicationModel.DataTransfer.Clipboard` — clipboard monitoring
- Target: `net9.0-windows10.0.19041.0`, `win-arm64`, unpackaged (`WindowsPackageType=None`)
