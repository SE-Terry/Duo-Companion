# Portrait keyboard clipping, auto-hide blur bug, and toolbar hide button — Design

Status: Approved
Date: 2026-07-18

## Problem

Three issues observed running the companion window on the Surface Duo's
secondary screen in portrait orientation:

1. **Keyboard clipped in portrait.** `KeyboardPage.xaml`'s five key rows are
   `StackPanel`s of fixed-pixel-width buttons (56-200px each), horizontally
   centered inside `Grid` rows sized `Height="*"`. Each row's total width
   (~700-970px depending on row) is wider than the Duo's portrait screen
   width, so it overflows evenly on both sides and is cut off at the screen
   edge — confirmed by photo (e.g. the number row shows `2` through `0`,
   missing both `1` on the left and the backspace key on the right).

2. **Auto-hide never triggers on same-window blur.** `AutoHideMode` (`Smart`/
   `Always`, see [[2026-07-11-auto-hide-window-design]]) is supposed to hide
   the companion window when the focused text field loses focus. In practice
   it never hides when the user taps away to a *non-text element within the
   same application window* (e.g. clicking a button or empty space in the
   same app the text field belongs to) — confirmed: symptom is "never hides
   at all," repro is "elsewhere in the same app/window."

   Root cause: `UiAutomationService.OnFocusChanged` only raises
   `TextInputBlurred` when the newly-focused element's top-level window
   differs from the last text field's window
   (`root == _lastTextFieldRootHwnd` short-circuits). This guard exists to
   suppress noise from autocomplete popups, caret helpers, and per-keystroke
   re-validation in rich editors/WebView2, which raise transient non-text
   focus events *within the same window* while the user is still typing —
   but it also blocks the legitimate case of the user deliberately clicking
   away from the field to something else in the same window, which is
   exactly the repro reported.

3. **No manual hide affordance in the toolbar.** The only ways to hide the
   companion window today are auto-hide-on-blur (buggy, see above) and the
   tray icon's toggle. There's no quick, always-visible manual hide button
   in the window's own toolbar.

## 1. Portrait keyboard scaling

Wrap `KeyboardPage.xaml`'s existing `Grid` in a `Viewbox`:

```xml
<Viewbox Stretch="Uniform" StretchDirection="DownOnly"
         HorizontalAlignment="Center" VerticalAlignment="Center">
    <Grid Padding="8" RowSpacing="6">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <!-- x5, was Height="*" -->
        </Grid.RowDefinitions>
        <!-- rows unchanged -->
    </Grid>
</Viewbox>
```

- Row heights change from `Height="*"` to `Height="Auto"` so the `Grid`
  reports its natural (current landscape-fit) size to the `Viewbox` as a
  measurement, rather than stretching to fill the page.
- `StretchDirection="DownOnly"` means the `Viewbox` only ever shrinks its
  child to fit available space — it never enlarges beyond the natural
  (56px-key) size, so landscape/wide layouts are pixel-identical to today.
- In portrait, where available width < the keyboard's natural width, the
  `Viewbox` uniformly scales the whole keyboard (both dimensions, keeping
  key aspect ratio) down to fit — no orientation detection or per-row
  layout logic needed.
- No changes to `KeyboardPage.xaml.cs` — button `Tag`/`Click` wiring,
  modifier state, etc. are untouched; only the visual tree changes.

## 2. Auto-hide blur fix

In `UiAutomationService.OnFocusChanged`, remove the same-window suppression
for the blur signal. New logic: raise `TextInputBlurred` whenever the
newly-focused element is not an edit/document control, provided the root
window resolved successfully (`root != IntPtr.Zero`) and isn't our own UI
(`root == _hostHwnd`) — regardless of whether that root matches
`_lastTextFieldRootHwnd`.

```csharp
if (controlType is UIA_EditControlTypeId or UIA_DocumentControlTypeId)
{
    _lastTextFieldRootHwnd = root;
    TextInputFocused?.Invoke(this, EventArgs.Empty);
    return;
}

if (root == IntPtr.Zero) return; // couldn't resolve a window — not a real signal

TextInputBlurred?.Invoke(this, EventArgs.Empty);
```

`_lastTextFieldRootHwnd` is no longer read here, but stays (still set on
focus) since nothing else in this file needs to change.

**Flicker safety net:** removing the same-window guard reintroduces the risk
the original guard was meant to prevent — transient non-text focus events
(autocomplete popups, caret helpers) firing a blur that's immediately
followed by a refocus into the same field. This is already handled one layer
up, in `CompanionWindow`:

- `ShowCompanionWindowNow()` (called from `OnTextInputFocused`) stops the
  pending hide `DispatcherTimer` before it fires — so any refocus into a text
  field before the debounce elapses cancels the hide outright.
- Widen `_hideTimer`'s interval from 200ms to 350ms, giving same-window
  refocus round-trips (e.g. picking an autocomplete suggestion) more room to
  land before the hide would otherwise fire. This is the only change in
  `CompanionWindow.xaml.cs` — the rest of the state machine from
  [[2026-07-11-auto-hide-window-design]] (mode table, `_lastManualPage`
  gating) is unchanged and still applies on top of the now-corrected blur
  signal.

## 3. Toolbar hide button

Add a fourth button to `CompanionWindow.CreateContent()`'s `navigationBar`,
alongside the existing Keyboard/Clipboard/Media/Handwriting group and the
Settings button — placed to the right of Settings (rightmost position),
using `CreateNavigationButton`'s existing helper:

```csharp
var hideButton = CreateNavigationButton("Hide", "Hide", ""); // ChromeMinimize glyph (U+E921)
hideButton.Click += (_, _) => HideCompanionWindowNow();
Grid.SetColumn(hideButton, 1);
navigationBar.Children.Add(hideButton);
```

(Exact column/margin arrangement follows the existing pattern used for
`settingsButton`.)

- Calls `HideCompanionWindowNow()` directly — the same underlying hide as
  the tray icon's toggle and the (now-fixed) auto-hide path — bypassing
  `AutoHideMode` entirely. This is an explicit user action, not an inferred
  one, so it always hides immediately regardless of the current mode
  (including `Off`).
- No new state: `_isHidden`/`_hideTimer` bookkeeping already handles a
  direct call to `HideCompanionWindowNow()` correctly (it no-ops if already
  hidden), same as every other caller.
- The window reappears the same way it always does: text focus
  (`OnTextInputFocused` always shows+navigates unconditionally, regardless of
  `AutoHideMode`) or the tray icon toggle.

## Edge cases

- **Rapid same-field refocus (autocomplete pick).** Covered by the 350ms
  debounce + focus-cancels-timer mechanism above; not a new edge case, just
  a wider safety margin on an existing one.
- **User hides via toolbar button while a text field is still focused.**
  window hides immediately; if the same field is still focused, no new
  focus-changed event will fire (nothing changed), so the window stays
  hidden until the user taps into a field again (blurs then re-focuses) or
  uses the tray icon. This matches existing tray-icon-hide-while-focused
  behavior, so no special-case handling is added.
- **Portrait scaling at extreme narrow widths** (narrower than a single
  56px key plus padding): `Viewbox` will still shrink proportionally; keys
  become very small but remain tappable in proportion to the available
  space. No minimum-size clamp is added — out of scope, no evidence this
  occurs on Duo's actual portrait width from the reported photo.

## Verification

This is a Windows-only WinUI3 app; it cannot be built or run from macOS.
Verification is manual, on the Windows machine used for prior builds:

1. Build via `build-release.ps1`, run `DuoCompanion.exe`.
2. **Portrait scaling**: fold/rotate the Duo (or resize the companion
   window to a narrow width) and confirm all five keyboard rows are fully
   visible with no clipping, matching the landscape layout proportionally.
   Confirm landscape/wide layout is pixel-identical to before this change.
3. **Auto-hide fix**: with `AutoHideMode = Smart` and no page manually
   pinned, focus a text field in some app, then click a non-text element
   (a button, empty canvas) *within that same app window* — confirm the
   companion window hides after ~350ms. Repeat with `Always`. Confirm
   rapid tab-between-fields and an autocomplete-popup pick do not cause a
   visible flicker.
4. **Toolbar hide button**: click it while the window is visible — confirm
   immediate hide regardless of `AutoHideMode` setting (test with `Off` too).
   Confirm the window still reappears normally via text focus or the tray
   icon afterward.
