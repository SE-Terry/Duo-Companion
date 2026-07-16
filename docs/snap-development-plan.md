# DuoSnap – Android-Style Dual-Screen Window Management for Surface Duo WOA

## Status (2026-07-16)

- **M1–M4** (display topology, window tracking, span preview, window spanning): implemented and previously reviewed. See `docs/superpowers/plans/2026-07-12-duosnap-flagship.md`.
- **M5–M7** (safe topology/ambiguity handling, full settings surface, layout profiles, hotkeys, title-bar gesture, layout suggestions): implementation-complete and staged, not committed, per `docs/superpowers/plans/2026-07-15-duosnap-complete.md`. Every task was implemented and reviewed (implementer + independent reviewer, with fix rounds where issues were found) on a macOS host with no Windows/.NET toolchain — **all of it is pending a real Windows/Surface Duo verification pass.** See `docs/duosnap-windows-validation.md` for the acceptance checklist.
- Layout suggestions (part of M7's "Intelligent Recommendations") exist at the service layer (`ILayoutSuggestionService`) but have no UI confirmation affordance yet — out of scope for the M5–M7 plan's file list, left for a future task.
- MSIX packaging (mentioned under Technical Stack below) was not part of M5–M7; the app remains an unpackaged, self-contained portable executable.

## Overview

### Goal

Develop a lightweight background service for **Windows on Surface Duo (WOA)** that recreates the intuitive dual-screen experience of Android.

The flagship feature is **automatic spanning**:

> When a user drags an application window into the hinge area between the two displays, the system previews the action and automatically spans the window across both screens when released.

The long-term vision is to evolve DuoSnap into a complete **hinge-aware window manager** for Windows on dual-screen devices.

---

# Objectives

- Provide Android-like window spanning.
- Make dual-screen workflows feel native.
- Support both Win32 and modern Windows applications.
- Minimize CPU and memory usage.
- Operate entirely in the background.
- Be configurable without requiring technical knowledge.
- **Extend Windows Snap rather than replace it whenever possible.**

---

# Windows Snap Integration Strategy

## Design Philosophy

DuoSnap is designed as an **extension of Windows Snap**, not a replacement.

Windows already provides excellent snap functionality for:

- Left edge snapping
- Right edge snapping
- Corner snapping
- Windows 11 Snap Layouts
- Keyboard shortcuts (`Win + Arrow`)
- Multi-monitor layouts

These features should continue working normally.

DuoSnap only introduces one new concept that Windows does not understand:

> **The hinge between the two Surface Duo displays.**

Therefore, DuoSnap should only take control when the user intentionally drags a window into the hinge activation zone.

---

## Interaction Zones

```
+-------------+====+-------------+
|             |||| |             |
| Left Screen |||| | Right Screen|
|             |||| |             |
+-------------+====+-------------+

      Hinge Activation Zone
```

### Expected Behavior

| User Action | Responsible System |
|-------------|--------------------|
| Drag to left edge | Windows Snap |
| Drag to right edge | Windows Snap |
| Drag to screen corners | Windows Snap |
| Win + Z Snap Layouts | Windows Snap |
| Win + Arrow shortcuts | Windows Snap |
| Drag into hinge | DuoSnap |

This preserves the familiar Windows experience while adding native dual-screen functionality.

---

## Hinge Override Logic

```
Window Drag Begins
        │
        ▼
Track Cursor Position
        │
        ▼
Cursor Inside Hinge Zone?
        │
 ┌──────┴──────┐
 │             │
No            Yes
 │             │
 ▼             ▼
Allow      Activate
Windows     DuoSnap
Snap        Preview
```

Only after the user releases the window inside the hinge zone will DuoSnap resize and span the application.

---

## Snap Compatibility Modes

To accommodate different user preferences, DuoSnap should provide configurable integration modes.

### Recommended (Default)

```
✓ Keep Windows Snap

Only intercept hinge interactions.
```

This preserves the native Windows experience while adding Duo-specific functionality.

---

### Replace Windows Snap

```
Replace Windows Snap
```

Future option for users who want DuoSnap to implement custom snapping behavior across the device.

---

### Disable Windows Snap

```
Disable Windows Snap while DuoSnap is running
```

Intended primarily for testing, debugging, or users who prefer DuoSnap to manage all window placement.

---

# Architecture

```
                    DuoSnap Service
                           │
          ┌────────────────┴────────────────┐
          │                                 │
 DisplayTopology                  WindowTracker
          │                                 │
          └──────────────┬──────────────────┘
                         │
                  DragDetector
                         │
                HingeDetector
                         │
                 OverlayManager
                         │
                   SpanEngine
                         │
               RestoreManager
                         │
                 LayoutProfiles
                         │
                 Settings Service
```

---

# Phase 1 – Display Topology

## Purpose

Understand the physical layout of the Surface Duo.

### Responsibilities

- Detect both displays
- Determine hinge location
- Calculate virtual desktop size
- Detect orientation changes
- Support landscape and portrait modes

### Deliverable

```
DisplayTopology.cs
```

---

# Phase 2 – Window Tracking

## Purpose

Track movable application windows in real time.

### Responsibilities

- Detect foreground window
- Track moving windows
- Ignore system windows
- Ignore minimized windows
- Detect maximize/restore state

### Candidate APIs

- EnumWindows()
- GetWindowRect()
- GetForegroundWindow()
- SetWinEventHook()

### Deliverable

```
WindowTracker.cs
```

---

# Phase 3 – Drag Detection

## Purpose

Determine when a user begins dragging a window.

Workflow:

```
Mouse Down
      │
      ▼
Window Move Begins
      │
      ▼
Track Window Position
```

The system should monitor movement with minimal latency while remaining lightweight.

---

# Phase 4 – Hinge Detection

## Purpose

Treat the hinge as its own interactive zone.

Example:

```
+-----------+====+-----------+
| Screen A  |Gap | Screen B  |
+-----------+====+-----------+
```

Rather than checking whether a window enters another monitor, determine whether the window's center point enters the hinge activation region.

Example threshold:

```
Hinge Center ±30 px
```

When triggered:

```
Normal Drag
      │
      ▼
Span Candidate
```

Windows Snap remains active outside of this region.

---

# Phase 5 – Span Preview Overlay

## Purpose

Provide visual feedback before spanning.

Features:

- Transparent overlay
- Highlight both displays
- Draw future window bounds
- Smooth fade animation

Example:

```
█████████████████████████████

      Drop to Span

█████████████████████████████
```

Only perform spanning after the drag operation ends.

---

# Phase 6 – Span Engine

## Purpose

Resize and reposition the application window.

Example calculation:

```
Left   = ScreenA.Left
Top    = min(ScreenA.Top, ScreenB.Top)

Width  = ScreenA.Width
       + HingeWidth
       + ScreenB.Width

Height = max(ScreenA.Height,
             ScreenB.Height)
```

Candidate APIs:

- MoveWindow()
- SetWindowPos()

Store:

- Previous position
- Previous size

These values will be used for restoration.

---

# Phase 7 – Restore Engine

## Purpose

Return applications to their original state.

When the user drags a spanned window away from the hinge:

```
Restore Previous Position
        │
        ▼
Restore Previous Size
```

The transition should be seamless and require no manual resizing.

---

# Phase 8 – Orientation Support

Supported layouts:

## Portrait

```
+
|
|
|
+
```

## Landscape

```
+---------+---------+
```

## Reverse Landscape

Supported.

## Reverse Portrait

Supported.

Orientation changes should automatically update hinge geometry.

---

# Phase 9 – Touch Optimization

Support:

- Finger drag
- Pen drag
- Touch release
- Inertia
- Touch-friendly thresholds

Touch interaction should feel identical to Android's spanning behavior.

---

# Phase 10 – Animation

Animations should be subtle and responsive.

Ideas:

- Fade overlay
- Outline expansion
- Window transition
- Restore animation

The goal is to improve clarity without distracting the user.

---

# Phase 11 – Settings Application

Provide a lightweight configuration utility.

Options:

- Enable Auto Span
- Hinge activation width
- Overlay transparency
- Animation speed
- Restore behavior
- Launch on startup
- Ignore selected applications
- Hotkeys
- Snap integration mode

---

# Phase 12 – Advanced Features

## Snap Layouts

Support layouts such as:

```
Left Screen

Right Screen

Both Screens

70 / 30

30 / 70
```

---

## Keyboard Shortcuts

Examples:

```
Win + Shift + Left

Win + Shift + Right

Win + Shift + S
```

Actions:

- Move to left display
- Move to right display
- Span across both displays

---

## Gesture Support

Examples:

- Double-tap title bar to span
- Long-press title bar
- Drag-and-hold over hinge
- Three-finger swipe

---

## Per-Application Profiles

Allow applications to remember preferred layouts.

| Application | Preferred Behavior |
|-------------|--------------------|
| Browser | Always span |
| PDF Reader | Always span |
| Explorer | Single screen |
| Settings | Single screen |
| Video Player | User choice |

---

## Intelligent Recommendations

The system can suggest layouts based on application type.

Example:

```
Browser

↓

Suggested:
Span
```

```
Settings

↓

Suggested:
Single Screen
```

---

## External Monitor Awareness

When an external display is connected:

- Disable hinge-specific logic
- Preserve window layouts
- Continue using standard Windows Snap
- Resume DuoSnap hinge behavior when disconnected

---

# Technical Stack

## Language

- C#
- .NET 8

## UI

- WPF

## Window APIs

- SetWinEventHook
- EnumWindows
- GetWindowRect
- MoveWindow
- SetWindowPos
- MonitorFromWindow
- GetMonitorInfo

## Configuration

JSON

## Packaging

- MSIX
- Portable executable (development builds)

---

# Milestones

## M1

Display topology detection

**Outcome**

- Detect displays
- Detect hinge
- Detect orientation

---

## M2

Window tracking

**Outcome**

- Detect moving windows
- Reliable drag tracking

---

## M3

Span preview

**Outcome**

- Overlay
- Hinge activation
- Drop indicator

---

## M4

Window spanning

**Outcome**

- Automatic spanning
- Restore support

---

## M5

Touch and orientation support

**Status:** Safe display-pair/topology detection (the ambiguity-rejection part of this milestone — see `HingeCalculator.ComputeDuoTopology`) is implemented, staged, and reviewed — pending Windows verification. Window-drag tracking is input-method-agnostic (it hooks OS-level window move/resize events, not raw mouse input), so mouse/touch/pen should all already exercise the same code path; this has not been confirmed on real touch/pen hardware. Explicit orientation-rotation handling beyond what M1-M4 already covers was not part of the M5-M7 plan's scope.

**Outcome**

- Portrait
- Landscape
- Rotation handling

---

## M6

Background service

**Status:** Implemented, staged, and reviewed — pending Windows verification. Startup integration (`IStartupRegistrationService`, per-user `HKCU\...\Run` key with content-comparison ownership checks) and the full Settings application (Settings → Dual-Screen Spanning) are complete. "Stable performance" has not been measured on real hardware.

**Outcome**

- Startup integration
- Settings application
- Stable performance

---

## M7

Advanced window management

**Status:** Implemented, staged, and reviewed — pending Windows verification, except where noted. Snap layouts (Span/Left/Right/70-30/30-70), the title-bar hold gesture, per-app profiles, and global keyboard-shortcut hotkeys are all complete. Intelligent layout suggestions exist at the service layer (`ILayoutSuggestionService`) but have no UI confirmation affordance — a suggestion is raised as an event but nothing in this plan wires it to a visible prompt. "Full Windows Snap integration" means DuoSnap's own three integration modes (Extend/Replace/Disabled-manually); DuoSnap never mutates an actual Windows Snap OS setting in any mode, by design.

**Outcome**

- Snap layouts
- Gestures
- Profiles
- Keyboard shortcuts
- Intelligent layout suggestions
- Full Windows Snap integration

---

# Long-Term Vision

DuoSnap aims to become the missing dual-screen shell experience for Windows on Surface Duo.

Rather than replacing Windows Snap, DuoSnap complements it by introducing a **hinge-aware interaction model** that Windows lacks. Users continue to enjoy the native Windows snapping experience while gaining seamless Android-inspired spanning, intelligent layouts, touch-first interactions, and application-aware behaviors optimized specifically for dual-screen hardware.