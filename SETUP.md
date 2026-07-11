# Duo Companion — Build from Source

> Just want to run the app? Grab the prebuilt release instead — see the main [README](README.md#download-and-run). This guide is for building the project yourself.

---

## Prerequisites

Install these on the machine you're building on (or the Surface Duo itself, if building on-device):

### Option A — Visual Studio (recommended)

- [Visual Studio 2022 17.8+](https://visualstudio.microsoft.com/) with:
  - `.NET Desktop Development` workload
  - `Windows App SDK C# Templates` (Individual Components → search "Windows App SDK")

### Option B — CLI only

```powershell
winget install Microsoft.DotNet.SDK.9
winget install Microsoft.WindowsAppRuntime.2
```

If `winget` isn't available, download both manually:
- .NET 9 SDK (ARM64): https://dotnet.microsoft.com/download/dotnet/9.0
- Windows App SDK 2.2: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads

> Restart your terminal after installing so the `dotnet` command is recognized.

**Note:** the `dotnet` CLI's MSBuild cannot resolve the project's COM reference (`ResolveComReference`, used for UI Automation) or the AppX resource-packaging tasks. Building via `dotnet build`/`dotnet publish` alone will fail on `DuoCompanion.Services` and on PRI generation — use Visual Studio (or its MSBuild.exe) for a full build, or the `Windows App SDK C# Templates` component specifically provides the missing tooling if you're building from the CLI.

---

## Get the Source

```powershell
git clone https://github.com/SE-Terry/Surface-Duo-HSOSK.git
cd Surface-Duo-HSOSK
```

Or download and extract the ZIP from **Code → Download ZIP** on GitHub.

---

## Build

### Visual Studio

1. Open `DuoCompanion.sln`
2. Set configuration: **Release | ARM64**
3. **Build → Build Solution** (`Ctrl+Shift+B`)
4. Press `F5` or **Debug → Start Without Debugging**

### Command Line

```powershell
msbuild DuoCompanion.sln /restore /p:Configuration=Release /p:Platform=ARM64 /p:RuntimeIdentifier=win-arm64
```

Output:
```
src\DuoCompanion.App\bin\ARM64\Release\net9.0-windows10.0.19041.0\DuoCompanion.exe
```

Run directly — no installer or MSIX required:

```powershell
.\src\DuoCompanion.App\bin\ARM64\Release\net9.0-windows10.0.19041.0\DuoCompanion.exe
```

### Publish Release Package

The prebuilt files in `dist/DuoCompanion-win-arm64/` must be produced on a **Windows** machine. WinUI 3 requires Windows Visual Studio / MSBuild tooling to compile XAML and publish the executable — this cannot be done from macOS or Linux.

From a Windows machine with Visual Studio's Windows App SDK tooling installed (see [Prerequisites](#prerequisites)):

```powershell
$staging = Join-Path $PWD 'dist-staging\DuoCompanion-win-arm64'
$release = Join-Path $PWD 'dist\DuoCompanion-win-arm64'
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
msbuild src\DuoCompanion.App\DuoCompanion.App.csproj /restore /t:Publish /p:Configuration=Release /p:Platform=ARM64 /p:RuntimeIdentifier=win-arm64 /p:SelfContained=false /p:PublishDir="$staging\"
Remove-Item $release -Recurse -Force -ErrorAction SilentlyContinue
Move-Item $staging $release
```

Zip `dist/DuoCompanion-win-arm64` to create the release artifact for GitHub Releases (`DuoCompanion-win-arm64.zip`).

---

## Tests

```powershell
dotnet test tests\DuoCompanion.Tests\DuoCompanion.Tests.csproj
```

---

## Updating Later

When a new version is available:

1. `git pull` (or download and extract the new ZIP, overwriting the old folder)
2. Rebuild: `msbuild DuoCompanion.sln /restore /p:Configuration=Release /p:Platform=ARM64 /p:RuntimeIdentifier=win-arm64`
3. If you are regenerating the release package, rerun the publish steps above and replace `dist/DuoCompanion-win-arm64`
4. Run the `.exe` — no reinstall needed

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

- C# 13 / .NET 9 / WinUI 3 (Windows App SDK 2.2)
- MVVM — CommunityToolkit.Mvvm 8.3.2
- Win32 P/Invoke — `SetWinEventHook`, `SendInput`, `SetWindowPos`, IAccessible
- `Windows.UI.Input.Inking` — handwriting recognition via `InkStrokeBuilder` + `InkRecognizerContainer`
- `Windows.ApplicationModel.DataTransfer.Clipboard` — clipboard monitoring
- Target: `net9.0-windows10.0.19041.0`, `win-arm64`, unpackaged (`WindowsPackageType=None`)
