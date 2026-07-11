# Duo Companion — Setup Guide

Do all of this **on your Surface Duo** (Windows 11 ARM).

---

## Step 1 — Download the ZIP

Go to: `https://github.com/SE-Terry/Surface-Duo-HSOSK`

Click **Code → Download ZIP**, then save it somewhere on the Duo (e.g. `Downloads`).

---

## Step 2 — Extract

Right-click the ZIP → **Extract All** → choose a destination, e.g.:

```
C:\Users\<you>\Documents\DuoCompanion
```

---

## Step 3 — Install Prerequisites

You only need to do this once.

### Install .NET 9 SDK

Open **Terminal** or **PowerShell** and run:

```powershell
winget install Microsoft.DotNet.SDK.9
```

### Install Windows App SDK Runtime

```powershell
winget install Microsoft.WindowsAppRuntime.1.6
```

If `winget` isn't available, download both manually:
- .NET 9 SDK (ARM64): https://dotnet.microsoft.com/download/dotnet/9.0
- Windows App SDK 1.6: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads

> Restart Terminal after installing so the `dotnet` command is recognized.

---

## Step 4 — Build

Open Terminal in the extracted folder (right-click the folder → **Open in Terminal**), then run:

```powershell
dotnet build DuoCompanion.sln -c Release -r win-arm64
```

This downloads NuGet packages and compiles everything. It takes 1–3 minutes on first run.

When it finishes you should see:

```
Build succeeded.
```

---

## Step 5 — Run

```powershell
.\src\DuoCompanion.App\bin\ARM64\Release\net9.0-windows10.0.19041.0\win-arm64\DuoCompanion.exe
```

No installer required — just run the `.exe` directly.

---

## First-Run Checklist

**Handwriting not working?**
Go to **Windows Settings → Optional Features → Add a feature** → search `Handwriting` → Install.  
Without it the handwriting page will show "No handwriting recognizer installed" and do nothing.

**Window appeared on the wrong screen?**
Fold and unfold the device once. The app detects the display change and repositions itself automatically.

**App won't start / "runtime not found" error?**
Re-run Step 3 to make sure the Windows App SDK Runtime is installed.

---

## Updating Later

When a new version is available:

1. Download the new ZIP and extract it (overwrite the old folder)
2. Run `dotnet build DuoCompanion.sln -c Release -r win-arm64` again
3. Run the `.exe` — no reinstall needed
