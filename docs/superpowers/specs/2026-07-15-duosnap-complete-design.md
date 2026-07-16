# DuoSnap Complete Feature Design

## Goal

Complete DuoSnap as a safe, configurable, hinge-aware extension to Windows Snap in DuoCompanion. It must span a window only for the Surface Duo panel pair, restore it predictably, and expose the remaining planned settings and advanced workflows without taking ownership of normal Windows snapping by default.

## Scope and delivery order

The work is delivered in three dependent milestones. Each milestone must retain the existing M1-M4 behavior and be independently testable.

1. **M5: Device robustness and interaction polish.** Identify a valid Duo panel pair from the display topology, recompute on display changes, reject ambiguous or external-monitor topologies, and add touch/pen-aware dwell behavior. The overlay gets a cancellable fade transition.
2. **M6: Configuration.** Persist and present all DuoSnap preferences: enabled state, activation width, overlay opacity, animation speed, restore policy, launch at startup, ignored applications, hotkeys, and Snap integration mode.
3. **M7: Advanced management.** Add keyboard shortcuts, per-application profiles, 50/50 and 70/30 layout targets, title-bar gesture entry points, and rule-based layout suggestions.

## Architecture

Pure topology, profile, layout, and policy calculations live in `DuoCompanion.Core` and receive xUnit coverage. Contracts define the service boundary. `DuoCompanion.Services.Snap` owns Win32 hooks, hotkey registration, process/window information, settings-driven policy, and persisted profile behavior. `DuoCompanion.App` contains settings controls and visual feedback only.

`AutoSpanCoordinatorService` remains the single decision point for drag-to-span. It asks a topology service for a validated Duo pair, asks a policy service whether the active window is eligible, and emits a target only after the pointer/window stays in the configured hinge activation region. Outside that region, no DuoSnap placement occurs, so the default mode continues to defer to Windows Snap.

## Topology and safety

`HingeCalculator` will select only two displays that share a full usable edge and whose dimensions/orientation form one contiguous Duo pair. It will not select the first two displays by coordinate order. If a third display is present, DuoSnap stays active only if exactly one unambiguous panel pair can be identified; otherwise it disables hinge actions and hides any active preview. This protects external-monitor configurations.

The calculated hinge supports vertical and horizontal arrangements and uses the configured activation half-width. Display configuration changes recompute topology and cancel an in-progress candidate.

## Input, preview, and restoration

Window tracking continues to use out-of-context move/size events, which cover mouse, touch, and pen window moves. A configurable dwell interval reduces accidental touch/pen spans. The preview is transparent, click-through, topmost, and fades to its configured opacity. It hides when a candidate ends, topology changes, the feature is disabled, or the app quits.

The restore policy is explicit: `OnNextDrag` restores the captured pre-span rectangle at drag start; `Never` retains a spanned window until the user changes it through a layout command. Original rectangles are retained per window only while the restore policy can use them and are discarded when the window is destroyed.

## Settings and persistence

All settings are serialized through the existing settings service. Defaults are:

- Auto Span enabled.
- 30-pixel activation half-width.
- 25% overlay opacity.
- 150 ms fade duration.
- `OnNextDrag` restore behavior.
- Windows Snap extension mode.
- No ignored apps, profiles, or custom hotkeys.

The settings page validates numeric ranges, edits ignored executable names and profiles, and updates active services without restart when possible. Launch-at-startup uses a per-user Windows startup registration and clearly reports failures in the UI/log.

## Snap modes

`ExtendWindowsSnap` is the default and only spans on a qualified hinge drop. `ReplaceWindowsSnap` enables DuoSnap layout commands and custom edge-zone layout choices while leaving unrecognized interactions to Windows. `WindowsSnapDisabledManually` is an informational diagnostic mode for users who have disabled Windows Snap themselves; DuoCompanion never changes Windows Snap settings.

## Layouts, profiles, hotkeys, and suggestions

The layout engine returns pure `WindowLayoutTarget` values for Left, Right, Span, 70/30, and 30/70. The span service applies them using the existing window-placement infrastructure. Profiles match executable name and choose a default layout or exclusion. They are applied when a qualifying window becomes foreground or completes a drag, never repeatedly during a move.

Hotkeys use a registered global hotkey service. Defaults are disabled until the user assigns them; conflicts are surfaced without replacing another application’s registration. A title-bar gesture requires a configured drag-and-hold duration over the hinge. Suggestions are deterministic and conservative: browser, document, and file-manager windows suggest Span; unknown applications make no suggestion. Suggestions are preview-only and require user confirmation.

## Error handling and lifecycle

Win32 registration, window movement, startup registration, and settings parsing failures are logged and leave DuoSnap safely inactive for the failed operation. Service start/stop methods are idempotent. All hook callbacks that feed UI events are marshaled to the UI dispatcher by the app layer. Quit unregisters hooks/hotkeys, restores any changed Windows Snap preference, hides the overlay, and releases transient window state.

## Testing and verification

Add pure unit tests before implementation for display-pair selection, external-monitor ambiguity, layout calculation, validation, matching/profile policy, and dwell/restore decisions. Add service tests around policy orchestration using interfaces/mocks where no Win32 runtime is required. Windows validation must build the solution, run the complete test project, and manually exercise mouse, touch, pen, orientation changes, an external monitor, all Snap modes, each hotkey, ignored apps, profiles, and app startup/quit cleanup.

## Local development metadata

CodeGraph is installed as a CLI and indexed only in this repository’s `.codegraph/` directory. `.codegraph/` is excluded through this clone’s `.git/info/exclude`, so no tracked project configuration or shared `.gitignore` entry is created.
