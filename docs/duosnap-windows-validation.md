# DuoSnap Windows Acceptance Checklist

This checklist covers DuoSnap M5–M7 (safe topology detection, full settings
surface, layout profiles, hotkeys, title-bar gesture, layout suggestions).
Everything below was implemented and code-reviewed on a macOS host with no
Windows/.NET toolchain — nothing in this list has been compiled, built, or
run for real. Run through it on an actual Surface Duo (or a Windows machine
with two displays that share an edge, for the parts that don't require the
physical hinge/pen/touch hardware) before considering DuoSnap M5–M7 done.

Prerequisite: `dotnet test DuoCompanion.sln` and
`dotnet build DuoCompanion.sln -c Release` both need to succeed with zero
failures/errors first — the entire Snap test suite under
`tests/DuoCompanion.Tests/Snap/` has only ever been hand-traced, never
compiled.

## Build and automated tests

- [ ] `dotnet build DuoCompanion.sln -c Release` succeeds with zero errors/warnings.
- [ ] `dotnet test DuoCompanion.sln` passes with zero failures.
- [ ] `dotnet test --filter FullyQualifiedName~Snap` specifically passes (this is the DuoSnap-only subset).

## Input methods

- [ ] Mouse: drag a window into the hinge zone and release — it spans.
- [ ] Touch: drag a window into the hinge zone with a finger and release — it spans.
- [ ] Pen: drag a window into the hinge zone with the pen and release — it spans.
- [ ] Grab a spanned window's title bar and drag it away from the hinge — it restores to its pre-span rect (per the configured restore behavior, see below).

## Topology / orientation

- [ ] Vertical hinge (landscape, side-by-side displays): span works as expected.
- [ ] Horizontal hinge (portrait, stacked displays): span works as expected.
- [ ] Rotate the device 180°: hinge orientation updates correctly, span still works.
- [ ] One external monitor connected in addition to the two Duo displays: DuoSnap correctly identifies the Duo pair and does not get confused by the third display (should behave as "detached third monitor, valid" — see `HingeCalculatorTests`).
- [ ] Two external monitors connected such that a second valid hinge-like pair could exist: DuoSnap correctly refuses to activate (ambiguous topology) rather than guessing which pair is the real hinge.
- [ ] Folded to single-screen mode: DuoSnap does not crash; hinge-specific behavior is safely disabled.

## Restore modes (Settings → Restore Original Size)

- [ ] `OnNextDrag` (default): dragging a spanned window away from the hinge restores its pre-span position/size.
- [ ] `Never`: dragging a spanned window away from the hinge does *not* auto-restore it.

## Ignore list and per-app profiles (Settings → Ignored Applications / Per-App Layout Profiles)

- [ ] Add an executable to the ignore list: dragging that app's window into the hinge does *not* span it.
- [ ] Add a profile for an executable with a fixed layout (e.g. always Span): opening that app and completing any qualifying drag applies the profile's layout, overriding the ambient hinge-drag decision.
- [ ] Mark a profile "Ignored": that executable is never auto-spanned or layout-suggested, even via the title-bar hold gesture.
- [ ] A profiled app dragged to an ordinary (non-hinge) location on screen is *not* forcibly relocated — only a drop that lands near the hinge is eligible.

## Layout commands and hotkeys (Settings → Global Hotkeys)

- [ ] Assign a hotkey to each action (Left, Right, Span, 70/30, 30/70) with distinct bindings, restart the app, and confirm each hotkey applies the correct layout to the current foreground window.
- [ ] Assign the same binding to two different actions: Settings shows a duplicate-binding conflict message (live, no restart needed to see the warning).
- [ ] Assign a binding already in use by another application/the OS: after restart, Settings shows a registration-failure status for that binding ("as of last launch").
- [ ] Layout commands (hotkeys, profiles) are unavailable when Snap Integration Mode is `Extend Windows Snap` (hinge-drag-only mode) — confirm hotkeys/profiles have no effect in that mode.
- [ ] Layout commands work normally in `Replace Windows Snap` and `Disabled manually` modes.

## Title-bar hold gesture

- [ ] With Snap Integration Mode set to `Disabled manually` (auto-span off), grab a window's title bar, hold it over the hinge for the configured dwell duration, and release — it spans (explicit gesture confirms even though ordinary drag-through auto-span is off).
- [ ] Same gesture on an ignored executable (ignore list or ignored profile) — it does *not* span.
- [ ] Release before the dwell duration completes — it does *not* span.

## Layout suggestions

- [ ] Open a browser (msedge/chrome/firefox), a PDF reader (acrord32/sumatrapdf), or Explorer with no profile configured for it, and complete a qualifying drag: confirm whether any suggestion signal is observable (note: no UI currently surfaces `LayoutSuggested` — this is expected; just confirm nothing crashes or misbehaves).
- [ ] An app with an explicit profile configured never produces a suggestion (profile always wins).
- [ ] **Known gap:** `ILayoutSuggestionService.ApplySuggestedLayout` — the actual layout-applying half of this feature — is not called anywhere in production code yet (no UI confirmation affordance exists). This means it is currently unverifiable through normal app usage. If you have a way to invoke it directly (e.g. attach a debugger and call it, or add a temporary test hook), confirm: it applies the correct layout for the most recently suggested window, it is a no-op when there is no pending suggestion, and it is a no-op when the hinge topology is currently ambiguous. Otherwise, treat this as an explicit follow-up task (build the confirmation UI first) rather than something this checklist can close out.

## Span overlay

- [ ] The preview overlay appears while dragging toward the hinge, at the configured opacity.
- [ ] The overlay fades in/out over the configured fade duration.
- [ ] The overlay is click-through (you can interact with whatever is underneath it).
- [ ] Turning off Auto Span while the overlay is visible hides it immediately.
- [ ] Changing overlay opacity/fade duration in Settings while a preview is visible updates it live.

## Snap-mode switching and quit

- [ ] Switching to `Disabled manually` mode shows a confirmation dialog; choosing Cancel leaves the mode unchanged.
- [ ] Choosing Disable in the confirmation dialog commits the mode change (and does *not* change any actual Windows Snap OS setting — check Windows Settings → Multitasking / Snap yourself before and after to confirm).
- [ ] Quit the app (tray menu and Settings → Quit) while nothing is spanned: clean exit, no hang, no exception.
- [ ] Quit the app while the span preview overlay is actively fading: clean exit, no hang, no exception, no visible leftover overlay window.

## Startup registration (Settings → Launch on startup)

- [ ] Enable "Launch Duo Companion when I sign in", sign out and back in (or reboot): the app launches automatically.
- [ ] Disable it: the app no longer launches automatically, and the `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value DuoCompanion created is removed.
- [ ] Manually create an unrelated Run value under a different name and confirm DuoCompanion's toggle does not touch it (sanity check of the ownership-by-content-comparison design, not something DuoCompanion's own toggle would normally encounter — mainly a "did the safety check actually get built correctly" sanity check).
