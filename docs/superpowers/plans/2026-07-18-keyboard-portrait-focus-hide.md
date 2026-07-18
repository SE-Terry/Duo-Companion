# Portrait Keyboard Scaling, Auto-Hide Blur Fix, and Toolbar Hide Button Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the on-screen keyboard being clipped in portrait orientation, fix `AutoHideMode` never hiding the companion window on same-window blur, and add a manual "Hide" toolbar button.

**Architecture:** Three independent, small changes against the existing WinUI3 `DuoCompanion` app: (1) a `Viewbox` wrapper around `KeyboardPage`'s key grid so it scales down to fit narrow widths, (2) a one-line removal of an overly-broad same-window suppression guard in `UiAutomationService`, paired with a wider debounce in `CompanionWindow` as a flicker safety net, (3) a new toolbar button in `CompanionWindow.CreateContent()`.

**Tech Stack:** C#, WinUI3 (Windows App SDK), XAML.

## Global Constraints

- This is a Windows-only WinUI3 app. It cannot be built, run, or tested from this macOS environment (no `dotnet`/Windows toolchain here) — every task's verification is manual, performed by the user on their Windows machine. Do not attempt `dotnet build`/`dotnet test` locally; do not claim a task verified without the user confirming the manual steps.
- Per this repo's established convention for Windows-unverified changes (see `.superpowers/sdd/progress.md`), each task's final step is `git add` (stage only) — not `git commit`. The user commits once they've confirmed the change works on real hardware.
- Design source of truth: `docs/superpowers/specs/2026-07-18-keyboard-portrait-focus-hide-design.md`. Follow it exactly; do not add scope beyond what it specifies (no orientation-detection code, no new settings, no AutoHideMode removal).
- Do not touch any file under `src/DuoCompanion.Services/Snap/` or `src/DuoCompanion.App/SpanOverlayWindow.*` — that's the separate, currently-paused hinge-snap bug investigation and is out of scope for this plan.

---

### Task 1: Portrait keyboard scaling

**Files:**
- Modify: `src/DuoCompanion.App/Pages/KeyboardPage.xaml`

**Interfaces:**
- Consumes: nothing new — `KeyboardPage.xaml.cs`'s `OnKeyClick`/`OnModifierClick` handlers and the `Button`/`FontIcon` tree are unchanged; only the wrapping visual container changes.
- Produces: nothing consumed by other tasks in this plan (fully independent of Tasks 2 and 3).

- [ ] **Step 1: Wrap the existing `Grid` in a `Viewbox` and change row heights to `Auto`**

Replace the full contents of `src/DuoCompanion.App/Pages/KeyboardPage.xaml` with:

```xml
<Page x:Class="DuoCompanion.App.Pages.KeyboardPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Viewbox Stretch="Uniform" StretchDirection="DownOnly"
             HorizontalAlignment="Center" VerticalAlignment="Center">
        <Grid Padding="8" RowSpacing="6">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <!-- Row 0: number row -->
            <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
                <Button Tag="49"  Content="1" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="50"  Content="2" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="51"  Content="3" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="52"  Content="4" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="53"  Content="5" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="54"  Content="6" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="55"  Content="7" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="56"  Content="8" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="57"  Content="9" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="48"  Content="0" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="8"   Width="80"  Height="56" FontSize="18" Click="OnKeyClick">
                    <FontIcon Glyph="&#xE750;" FontSize="18"/>
                </Button>
            </StackPanel>

            <!-- Row 1: QWERTY -->
            <StackPanel Grid.Row="1" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
                <Button Tag="81"  Content="Q" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="87"  Content="W" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="69"  Content="E" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="82"  Content="R" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="84"  Content="T" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="89"  Content="Y" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="85"  Content="U" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="73"  Content="I" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="79"  Content="O" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="80"  Content="P" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="13"  Content="↵" Width="80" Height="56" FontSize="18" Click="OnKeyClick"/>
            </StackPanel>

            <!-- Row 2: ASDF -->
            <StackPanel Grid.Row="2" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
                <Button Tag="65"  Content="A" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="83"  Content="S" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="68"  Content="D" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="70"  Content="F" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="71"  Content="G" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="72"  Content="H" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="74"  Content="J" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="75"  Content="K" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="76"  Content="L" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            </StackPanel>

            <!-- Row 3: ZXCV + Shift -->
            <StackPanel Grid.Row="3" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
                <Button x:Name="BtnShift" Tag="16" Width="80" Height="56" FontSize="18" Click="OnModifierClick">
                    <FontIcon Glyph="&#xE752;" FontSize="18"/>
                </Button>
                <Button Tag="90"  Content="Z" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="88"  Content="X" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="67"  Content="C" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="86"  Content="V" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="66"  Content="B" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="78"  Content="N" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
                <Button Tag="77"  Content="M" Width="56" Height="56" FontSize="18" Click="OnKeyClick"/>
            </StackPanel>

            <!-- Row 4: bottom row -->
            <StackPanel Grid.Row="4" Orientation="Horizontal" Spacing="4" HorizontalAlignment="Center">
                <Button x:Name="BtnCtrl" Tag="17" Content="Ctrl" Width="72" Height="56" FontSize="14" Click="OnModifierClick"/>
                <Button x:Name="BtnAlt"  Tag="18" Content="Alt"  Width="72" Height="56" FontSize="14" Click="OnModifierClick"/>
                <Button Tag="9"   Content="Tab" Width="72" Height="56" FontSize="14" Click="OnKeyClick"/>
                <Button Tag="32"  Content="Space" Width="200" Height="56" FontSize="16" Click="OnKeyClick"/>
                <Button Tag="37" Width="52" Height="56" Click="OnKeyClick"><FontIcon Glyph="&#xE76B;" FontSize="18"/></Button>
                <Button Tag="38" Width="52" Height="56" Click="OnKeyClick"><FontIcon Glyph="&#xE70E;" FontSize="18"/></Button>
                <Button Tag="40" Width="52" Height="56" Click="OnKeyClick"><FontIcon Glyph="&#xE70D;" FontSize="18"/></Button>
                <Button Tag="39" Width="52" Height="56" Click="OnKeyClick"><FontIcon Glyph="&#xE76C;" FontSize="18"/></Button>
                <Button x:Name="BtnWin" Tag="91" Content="Win" Width="72" Height="56" FontSize="14" Click="OnKeyClick"/>
            </StackPanel>
        </Grid>
    </Viewbox>
</Page>
```

The only changes from the current file: the `<Viewbox Stretch="Uniform" StretchDirection="DownOnly" ...>` wrapper around the `Grid`, and all five `RowDefinition Height="*"` changed to `Height="Auto"`. Every `Button`/`FontIcon`/`Tag`/`Click` stays byte-for-byte identical — do not rename or renumber any `Tag` value.

- [ ] **Step 2: Manual verification (Windows machine required)**

This cannot be verified from this environment. Ask the user to:
1. Rebuild (`build-release.ps1` or their usual build step) and run `DuoCompanion.exe`.
2. With the companion window on the Duo's portrait screen (or by resizing the window narrower than ~700px if testing without the hardware), open the Keyboard page and confirm all five rows are fully visible with no clipping on either side.
3. Compare against the landscape/wide-window layout — it must look pixel-identical to before this change (same key size, same spacing) since `StretchDirection="DownOnly"` never enlarges.

Do not proceed to Step 3 until the user confirms both checks pass.

- [ ] **Step 3: Stage**

```bash
git add src/DuoCompanion.App/Pages/KeyboardPage.xaml
```

---

### Task 2: Fix auto-hide never firing on same-window blur

**Files:**
- Modify: `src/DuoCompanion.Services/Automation/UiAutomationService.cs:73-81`
- Modify: `src/DuoCompanion.App/CompanionWindow.xaml.cs:17`

**Interfaces:**
- Consumes: nothing new from Task 1 or Task 3.
- Produces: nothing consumed by other tasks in this plan. `IUiAutomationService.TextInputBlurred` keeps its existing `event EventHandler?` signature — only when it fires changes, not its shape. `CompanionWindow`'s `_hideTimer` stays a `DispatcherTimer` field — only its `Interval` value changes.

- [ ] **Step 1: Remove the same-window suppression guard in `UiAutomationService.OnFocusChanged`**

In `src/DuoCompanion.Services/Automation/UiAutomationService.cs`, the method currently ends with:

```csharp
            // UIA fires focus-changed events far more often than the legacy MSAA hook
            // did — autocomplete popups, caret helpers, and per-keystroke re-validation
            // in rich editors/WebView2 all raise transient, non-text focus events while
            // the user is still actively typing in the same field. Only treat this as
            // actually leaving text input if focus moved to a different top-level
            // window; same-window noise must not hide the keyboard mid-sentence.
            if (root == IntPtr.Zero || root == _lastTextFieldRootHwnd) return;

            TextInputBlurred?.Invoke(this, EventArgs.Empty);
```

Replace it with:

```csharp
            // Any focus target that isn't an edit/document control counts as blur,
            // regardless of whether it's in the same top-level window as the last
            // text field — a click on a button or empty space in the same app window
            // is a legitimate "the user is done with this field" signal and must not
            // be suppressed. The transient same-window noise this guard used to filter
            // (autocomplete popups, caret helpers, per-keystroke re-validation in rich
            // editors/WebView2) is instead absorbed by CompanionWindow's hide debounce:
            // a fresh TextInputFocused cancels the pending hide before it fires.
            if (root == IntPtr.Zero) return;

            TextInputBlurred?.Invoke(this, EventArgs.Empty);
```

`_lastTextFieldRootHwnd` is still assigned two lines above (on the `TextInputFocused` branch) — leave that assignment as-is; it's simply no longer read here.

- [ ] **Step 2: Widen the hide debounce in `CompanionWindow`**

In `src/DuoCompanion.App/CompanionWindow.xaml.cs:17`, change:

```csharp
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
```

to:

```csharp
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
```

No other line in `CompanionWindow.xaml.cs` changes for this task.

- [ ] **Step 3: Manual verification (Windows machine required)**

This cannot be verified from this environment. Ask the user to rebuild and run, then:
1. Set Settings → Auto-hide to `Smart`, with no page manually pinned (stay on the default Keyboard page).
2. Focus a text field in some other app, then click a non-text element (a button, empty canvas) **within that same app's window** — confirm the companion window now hides after roughly 350ms (previously: never hid).
3. Repeat with Auto-hide set to `Always` — same expected result.
4. Rapidly tab between two adjacent text fields in the same app — confirm no visible flicker (the window should stay visible throughout, not hide-then-reappear).
5. If the target app has an autocomplete/suggestion popup (e.g. a browser address bar), type to trigger it and pick a suggestion — confirm this doesn't cause a visible hide-then-reappear flicker.

Do not proceed to Step 4 until the user confirms all checks pass.

- [ ] **Step 4: Stage**

```bash
git add src/DuoCompanion.Services/Automation/UiAutomationService.cs src/DuoCompanion.App/CompanionWindow.xaml.cs
```

---

### Task 3: Add a manual "Hide" button to the toolbar

**Files:**
- Modify: `src/DuoCompanion.App/CompanionWindow.xaml.cs:172-216` (`CreateContent`/`CreateNavigationButton`)

**Interfaces:**
- Consumes: `HideCompanionWindowNow()` (already defined at `CompanionWindow.xaml.cs:90-95`, `private void HideCompanionWindowNow()`) and `CreateNavigationButton(string tag, string toolTip, string glyph)` (already defined at `CompanionWindow.xaml.cs:206-216`) — both existing methods, unchanged signatures.
- Produces: nothing consumed by other tasks in this plan.

- [ ] **Step 1: Add the hide button next to Settings in `CreateContent()`**

In `src/DuoCompanion.App/CompanionWindow.xaml.cs`, the `CreateContent()` method currently ends with:

```csharp
        var settingsButton = CreateNavigationButton("Settings", "Settings", "");
        settingsButton.Margin = new Thickness(0, 0, 8, 0);

        navigationBar.Children.Add(navigationButtons);
        Grid.SetColumn(settingsButton, 1);
        navigationBar.Children.Add(settingsButton);

        root.Children.Add(navigationBar);
        Grid.SetRow(_contentFrame, 1);
        root.Children.Add(_contentFrame);
        return root;
    }
```

Replace it with:

```csharp
        var settingsButton = CreateNavigationButton("Settings", "Settings", "");

        var hideButton = CreateNavigationButton("Hide", "Hide", "");
        hideButton.Margin = new Thickness(0, 0, 8, 0);
        hideButton.Click += (_, _) => HideCompanionWindowNow();

        var rightButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };
        rightButtons.Children.Add(settingsButton);
        rightButtons.Children.Add(hideButton);

        navigationBar.Children.Add(navigationButtons);
        Grid.SetColumn(rightButtons, 1);
        navigationBar.Children.Add(rightButtons);

        root.Children.Add(navigationBar);
        Grid.SetRow(_contentFrame, 1);
        root.Children.Add(_contentFrame);
        return root;
    }
```

This introduces a `rightButtons` `StackPanel` (same `Orientation`/`Spacing` pattern already used for `navigationButtons` a few lines above) holding both `settingsButton` and the new `hideButton`, so the `Margin = new Thickness(0, 0, 8, 0)` right-edge padding that used to live on `settingsButton` moves to whichever button is now rightmost (`hideButton`). `hideButton.Click` calls `HideCompanionWindowNow()` directly — not `ToggleVisibility()` — since this is an explicit, unconditional hide regardless of `AutoHideMode`, matching the design spec. `CreateNavigationButton`'s existing `Click += OnNavClick` wiring inside that helper is untouched; this button simply adds one more handler via `+=` for its own click, same pattern the helper already uses internally.

- [ ] **Step 2: Manual verification (Windows machine required)**

This cannot be verified from this environment. Ask the user to rebuild and run, then:
1. Confirm a new button with a minimize-style glyph appears in the toolbar, to the right of the Settings button.
2. Click it while the window is visible — confirm the window hides immediately.
3. Repeat with Settings → Auto-hide set to `Off` — confirm the button still hides the window immediately (this path bypasses `AutoHideMode` entirely).
4. Confirm the window still reappears normally afterward via focusing a text field, or via the tray icon's show/hide toggle.

Do not proceed to Step 3 until the user confirms all checks pass.

- [ ] **Step 3: Stage**

```bash
git add src/DuoCompanion.App/CompanionWindow.xaml.cs
```
