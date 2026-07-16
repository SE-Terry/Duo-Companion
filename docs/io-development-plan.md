# Duo Companion

**Project:** Duo Companion

**Platform:** Windows 11 ARM (DuoWOA)

**Target Device:** Microsoft Surface Duo / Surface Duo 2 (DuoWOA)

**Language:** C#

**Framework:** .NET 9 + WinUI 3 (Windows App SDK)

**Architecture:** MVVM

**License:** MIT

---

# Overview

## Goal

Develop an open-source companion application for Surface Duo running Windows 11 ARM (DuoWOA).

The application aims to recreate and expand upon the dual-screen productivity experience found on devices like the ASUS Zenbook Duo (ScreenXpert), while being specifically designed for Surface Duo's dual-display form factor.

The app **does not replace Windows** and **does not modify the system touch keyboard**. Instead, it provides a dedicated companion window that occupies the secondary display.

Future versions will support modules such as:

- Virtual Keyboard
- Virtual Touchpad
- Clipboard Manager
- Macro Pad
- Handwriting
- Additional productivity tools

---

# Design Principles

- Native Windows application
- ARM64 optimized
- Touch-first interface
- Modular architecture
- Plugin-ready
- Open source
- No administrator privileges required
- No system hacks
- No replacement of Windows shell components
- Auto detect when user enter a input field to show this as a always-run-service.

---

# Technology Stack

| Component | Technology |
|------------|------------|
| Language | C# |
| Framework | .NET 9 |
| UI | WinUI 3 |
| Pattern | MVVM |
| Logging | Microsoft.Extensions.Logging |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |
| Testing | xUnit |
| Packaging | MSIX (optional) |

---

# Repository Structure

```
DuoCompanion/

├── src/
│   ├── DuoCompanion.App/
│   ├── DuoCompanion.Core/
│   ├── DuoCompanion.Modules/
│   ├── DuoCompanion.Services/
│   ├── DuoCompanion.UI/
│   └── DuoCompanion.Contracts/
│
├── tests/
│
├── docs/
│
└── assets/
```

---

# High-Level Architecture

```
App

│

├── Display Service
│
├── Window Manager
│
├── Orientation Service
│
├── Input Service
│
├── UI Automation Service
│
├── Settings Service
│
└── Module Host
        │
        ├── Keyboard Module
        ├── Touchpad Module
        ├── Clipboard Module
        ├── Media Module
        ├── Notes Module
        └── Future Modules
```

Each module should be completely independent.

---

# Plugin Architecture

The application should be designed around modules instead of hardcoded pages.

Example interface:

```csharp
public interface IDuoModule
{
    string Name { get; }

    IconSource Icon { get; }

    UserControl CreateView();

    void OnActivated();

    void OnDeactivated();
}
```

Future modules can be added without modifying the application core.

---

# Development Roadmap

---

# Milestone 1 — Display Detection

## Goal

Detect all connected displays.

### Requirements

- Enumerate monitors
- Detect primary monitor
- Detect secondary monitor
- Read monitor resolution
- Read monitor position
- Display debug information

### Deliverables

Working monitor detection.

---

# Milestone 2 — Companion Window

## Goal

Create a dedicated window for the lower display.

### Requirements

- Borderless
- Fullscreen
- Always on top
- Automatically move to secondary display
- Restore after monitor changes
- Close cleanly

Result:

```
Top Display
--------------------------
Main Application

Bottom Display
--------------------------
Empty Companion Window
```

---

# Milestone 3 — Navigation Shell

Replace the blank window with navigation.

Modules:

- Keyboard
- Touchpad
- Clipboard
- Media
- Settings

No functionality yet.

Navigation only.

---

# Milestone 4 — Virtual Keyboard

Implement a touch-friendly keyboard.

Requirements:

- QWERTY
- Shift
- Enter
- Backspace
- Space
- Ctrl
- Alt
- Win
- Tab
- Arrow keys

Keyboard should inject real Windows key events.

---

# Milestone 5 — Virtual Touchpad

Implement a touchpad.

Support:

- Cursor movement
- Tap
- Double tap
- Drag
- Two-finger scroll
- Right click

Optional:

- Pinch zoom

---

# Milestone 6 — Clipboard Manager

Features:

- Clipboard history
- Search
- Pin items
- Tap to paste

---

# Milestone 7 — Media Controls

Display:

- Play
- Pause
- Previous
- Next
- Volume

Optional:

- Brightness
- Media artwork

---

# Milestone 8 — Automation

Automatically detect text input.

Flow:

```
TextBox Focused

↓

Open Keyboard Module

↓

TextBox Lost Focus

↓

Hide Keyboard
```

Use Windows UI Automation APIs.

---

# Milestone 9 — Orientation Detection

Support:

- Portrait
- Landscape
- Single Screen
- Dual Screen

Automatically adjust layouts.

---

# Milestone 10 — Settings

Configuration:

- Startup
- Theme
- Opacity
- Animations
- Module order
- Default module
- Keyboard size
- Keyboard position

---

# Coding Standards

- MVVM
- Dependency Injection
- Async-first APIs
- XML documentation
- No business logic in Views
- Logging through ILogger
- One responsibility per class

---

# Future Modules

Not part of MVP.

Possible modules:

- AI Assistant
- Calculator
- Sticky Notes
- Handwriting
- Emoji Picker
- Macro Pad
- Stream Deck Mode
- Timer
- Calendar
- Task Manager
- File Transfer
- Home Assistant Dashboard
- Spotify Controller
- OBS Controller

---

# Non-Goals

The MVP should **NOT**:

- Replace Windows Touch Keyboard
- Modify Explorer
- Patch Windows
- Require Administrator privileges
- Hook undocumented Windows APIs
- Depend on ASUS ScreenXpert
- Emulate Android behavior at the system level

---

# Deliverables Per Milestone

Each milestone should include:

- Working source code
- Build instructions
- Brief implementation summary
- Known limitations
- Suggested next milestone

---

# Long-Term Vision

Duo Companion should evolve into a modular second-screen workspace for Surface Duo, inspired by ASUS ScreenXpert but designed specifically for DuoWOA.

Rather than focusing solely on a keyboard, the lower display should become a configurable productivity surface capable of hosting interchangeable modules such as:

- Keyboard
- Touchpad
- Clipboard
- AI Assistant
- Notes
- Media Controls
- Automation Tools

The architecture should prioritize extensibility so that new modules can be added with minimal changes to the application core.