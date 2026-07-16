# DuoSnap Complete Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish DuoSnap M5-M7: safe Duo topology, configurable behavior, and advanced layouts, profiles, gestures, hotkeys, and suggestions.

**Architecture:** Keep calculations and policy in Core with xUnit tests; expose Services through Contracts; use Services for Win32 interop and App only for WinUI controls/overlay. `AutoSpanCoordinatorService` remains the one drag-to-span policy gate.

**Tech Stack:** C#/.NET 9, WinUI 3, raw user32/shell32 P/Invoke, xUnit/NSubstitute.

## Global Constraints

- Do not run `git commit`; stage completed task files only.
- This macOS environment cannot execute the Windows build. Run the specified test commands on Windows and manually validate the scenario list in Task 10.
- Preserve normal Windows Snap in `ExtendWindowsSnap` mode.
- `.codegraph/` remains a local index excluded only through `.git/info/exclude`.
- Hook and timer callbacks are off the UI thread; marshal all WinUI work through `DispatcherQueue.TryEnqueue`.

---

## File map

- `Core/Models/DuoSnap*.cs`: immutable enums/records for topology, settings validation, layouts, profiles, and suggestions.
- `Core/Snap/*.cs`: pure display-pair selection, layout calculation, eligibility, and suggestion logic.
- `Contracts/Services/I*.cs`: topology, policy, layout, hotkey, startup, and window-identity service boundaries.
- `Services/Snap/*.cs`: settings-aware orchestration, Win32 lifecycle adapters, and profile application.
- `Services/Win32/NativeMethods.cs`: only the P/Invokes/constants required by service adapters.
- `App/Pages/SettingsPage.*` and `SpanOverlayWindow.*`: settings editing and preview UI.
- `tests/DuoCompanion.Tests/Snap/*.cs`: pure Core behavior and coordinator-policy tests.

### Task 1: Safe Duo display-pair selection

**Files:**
- Modify: `src/DuoCompanion.Core/Models/HingeZone.cs`, `src/DuoCompanion.Core/Snap/HingeCalculator.cs`
- Create: `src/DuoCompanion.Core/Models/DuoDisplayTopology.cs`
- Modify: `tests/DuoCompanion.Tests/Snap/HingeCalculatorTests.cs`

**Produces:** `DuoDisplayTopology(HingeZone Hinge, bool HasExternalDisplays)` and `HingeCalculator.ComputeDuoTopology(IReadOnlyList<DisplayInfo>, int): DuoDisplayTopology?`. Return a topology only when exactly one pair shares an edge and has matching perpendicular bounds; reject an ambiguous pair.

- [ ] **Step 1: Add failing tests** for a valid vertical pair plus a detached third monitor (valid), an external monitor that forms a second valid pair (null), offset/non-touching displays (null), and stacked displays (valid).

```csharp
[Fact]
public void ComputeDuoTopology_returns_null_when_two_pairs_are_valid()
{
    var topology = HingeCalculator.ComputeDuoTopology(new[] { Left, Right, ExternalRight });
    Assert.Null(topology);
}
```

- [ ] **Step 2: Run** `dotnet test tests/DuoCompanion.Tests/DuoCompanion.Tests.csproj --filter FullyQualifiedName~HingeCalculatorTests`; expected new-test failure.
- [ ] **Step 3: Implement** pair enumeration using every unordered display pair, `SharesVerticalEdge`/`SharesHorizontalEdge` helpers, and require exactly one candidate. Keep `ComputeHingeZone` as a compatibility wrapper over `ComputeDuoTopology`.
- [ ] **Step 4: Run the same command**; expected PASS.
- [ ] **Step 5: Stage** the four task files.

### Task 2: Settings, layouts, profiles, and policy primitives

**Files:**
- Modify: `src/DuoCompanion.Core/Models/AppSettings.cs`
- Create: `src/DuoCompanion.Core/Models/DuoSnapSettings.cs`, `src/DuoCompanion.Core/Models/WindowLayout.cs`, `src/DuoCompanion.Core/Models/AppLayoutProfile.cs`
- Create: `src/DuoCompanion.Core/Snap/WindowLayoutCalculator.cs`, `src/DuoCompanion.Core/Snap/DuoSnapPolicy.cs`
- Create: `tests/DuoCompanion.Tests/Snap/WindowLayoutCalculatorTests.cs`, `tests/DuoCompanion.Tests/Snap/DuoSnapPolicyTests.cs`

**Produces:** `SnapIntegrationMode`, `RestoreBehavior`, `WindowLayoutKind`, `WindowLayoutTarget`, `DuoSnapSettings`, and pure `DuoSnapPolicy.CanSpan(...)`/`WindowLayoutCalculator.Compute(...)` APIs.

- [ ] **Step 1: Add failing tests** for 50/50 span, left/right panel, 70/30 and 30/70 bounds in vertical and horizontal layouts; tests that disabled, ignored, ambiguous, and dwell-incomplete windows cannot span; test case-insensitive executable profile matching.

```csharp
[Fact]
public void Compute_70_30_assigns_seventy_percent_to_left_panel()
{
    var target = WindowLayoutCalculator.Compute(VerticalTopology, WindowLayoutKind.Left70Right30);
    Assert.Equal(1890, target.Width);
}
```

- [ ] **Step 2: Run** the two new test classes; expected compile failure before the new types exist.
- [ ] **Step 3: Implement** defaults (30 px, .25 opacity, 150 ms, `OnNextDrag`, `ExtendWindowsSnap`), clamp helpers (`ActivationHalfWidth` 5-120, opacity .05-.80, fade 0-1000 ms, dwell 0-1500 ms), JSON-serializable `List<string>` ignored executable names and `List<AppLayoutProfile>` profiles.
- [ ] **Step 4: Run** the same tests; expected PASS.
- [ ] **Step 5: Stage** Core models/calculators/tests.

### Task 3: Topology, identity, and coordinator safety wiring

**Files:**
- Modify: `src/DuoCompanion.Contracts/Services/IHingeTopologyService.cs`, `IWindowTrackerService.cs`, `IAutoSpanCoordinatorService.cs`
- Create: `src/DuoCompanion.Contracts/Services/IWindowIdentityService.cs`
- Modify: `src/DuoCompanion.Services/Snap/HingeTopologyService.cs`, `WindowTrackerService.cs`, `AutoSpanCoordinatorService.cs`
- Create: `src/DuoCompanion.Services/Snap/WindowIdentityService.cs`
- Modify: `src/DuoCompanion.Services/Win32/NativeMethods.cs`
- Create: `tests/DuoCompanion.Tests/Snap/AutoSpanCoordinatorPolicyTests.cs`

**Produces:** `CurrentTopology`, tracker events containing `ProcessName`, and idempotent coordinator start/stop that cancels preview for topology/settings changes and applies configured dwell/restore/ignore policy.

- [ ] **Step 1: Add failing coordinator tests** using substitutes: no span for ambiguous topology or ignored executable; no span until dwell elapsed; disable event emits candidate exit; `Never` does not restore on drag start.
- [ ] **Step 2: Run** coordinator tests; expected missing-interface/type failures.
- [ ] **Step 3: Implement** `GetWindowThreadProcessId`, `Process.GetProcessById(...).ProcessName` with failure fallback `string.Empty`, `IHingeTopologyService.CurrentTopology`, `TopologyChanged`, and coordinator state keyed to HWND plus `Stopwatch.GetTimestamp()` dwell tracking. Ensure start/stop subscriptions are idempotent and `Stop` always emits an exit if needed.
- [ ] **Step 4: Run** coordinator tests; expected PASS.
- [ ] **Step 5: Stage** task files.

### Task 4: Preview animation and live settings application

**Files:**
- Modify: `src/DuoCompanion.Contracts/Services/IWindowManagerService.cs`
- Modify: `src/DuoCompanion.Services/Window/WindowManagerService.cs`, `src/DuoCompanion.Services/Win32/NativeMethods.cs`
- Modify: `src/DuoCompanion.App/SpanOverlayWindow.xaml`, `SpanOverlayWindow.xaml.cs`, `App.xaml.cs`
- Create: `src/DuoCompanion.Contracts/Services/IDuoSnapSettingsMonitor.cs`
- Create: `src/DuoCompanion.Services/Snap/DuoSnapSettingsMonitor.cs`

**Produces:** an evented settings monitor and `SpanOverlayWindow.ShowAt(target, opacity, fadeDuration)`/`HideOverlay(fadeDuration)`.

- [ ] **Step 1: Add failing tests** for `DuoSnapSettings.Normalize` retaining defaults and clamping persisted invalid values.
- [ ] **Step 2: Run** these Core tests; expected failure before normalization exists.
- [ ] **Step 3: Implement** `SetLayeredWindowAttributes` alpha updates and a dispatcher-driven `DispatcherQueueTimer` animation that cancels/restarts on state changes. The monitor raises `Changed` only after settings save/reset. App subscriptions read current settings at event time and hide immediately when Auto Span turns off.
- [ ] **Step 4: Run** the Core test command; expected PASS; manually inspect overlay is click-through and no timer survives hide/quit.
- [ ] **Step 5: Stage** task files.

### Task 5: Snap modes and restore lifecycle

**Files:**
- Modify: `src/DuoCompanion.Contracts/Services/IWindowSpanService.cs`
- Modify: `src/DuoCompanion.Services/Snap/WindowSpanService.cs`, `AutoSpanCoordinatorService.cs`
- Create: `src/DuoCompanion.Contracts/Services/IWindowsSnapIntegrationService.cs`
- Create: `src/DuoCompanion.Services/Snap/WindowsSnapIntegrationService.cs`
- Modify: `src/DuoCompanion.Services/Win32/NativeMethods.cs`, `src/DuoCompanion.App/App.xaml.cs`

**Produces:** `ApplyLayout(IntPtr, WindowLayoutTarget)`, `ForgetWindow(IntPtr)`, and integration mode lifecycle service.

- [ ] **Step 1: Add failing pure policy tests** proving Extend permits only hinge spans, Replace permits layout commands, and manually-disabled mode requires explicit user selection.
- [ ] **Step 2: Run** policy tests; expected failure.
- [ ] **Step 3: Implement** `ExtendWindowsSnap` as no OS setting mutation; `ReplaceWindowsSnap` as custom-command enablement only; `WindowsSnapDisabledManually` as an informational user-selected mode with no OS-setting mutation. Do not modify Windows Snap settings. Register `EVENT_OBJECT_DESTROY` to discard stored window rectangles.
- [ ] **Step 4: Run** policy tests; expected PASS.
- [ ] **Step 5: Stage** task files.

### Task 6: Per-app profiles, layouts, and suggestions

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/IAppLayoutProfileService.cs`, `ILayoutSuggestionService.cs`
- Create: `src/DuoCompanion.Services/Snap/AppLayoutProfileService.cs`, `LayoutSuggestionService.cs`
- Modify: `src/DuoCompanion.Services/Snap/AutoSpanCoordinatorService.cs`
- Create: `tests/DuoCompanion.Tests/Snap/AppLayoutProfileServiceTests.cs`, `LayoutSuggestionServiceTests.cs`

**Produces:** profile resolution and `LayoutSuggested` event that requires explicit `ApplySuggestedLayout` confirmation.

- [ ] **Step 1: Add failing tests** for exact executable profiles overriding suggestions, ignored profiles blocking all layouts, browser/document/file-manager names suggesting Span, and unknown apps producing no suggestion.
- [ ] **Step 2: Run** the test classes; expected compile failure.
- [ ] **Step 3: Implement** case-insensitive executable matching; never apply a profile more than once per foreground/drag completion; restrict suggestions to `msedge`, `chrome`, `firefox`, `explorer`, `acrord32`, and `sumatrapdf` names; suggestions only emit events.
- [ ] **Step 4: Run** the tests; expected PASS.
- [ ] **Step 5: Stage** task files.

### Task 7: Global hotkeys, title-bar dwell gesture, and startup registration

**Files:**
- Create: `src/DuoCompanion.Contracts/Services/IGlobalHotkeyService.cs`, `IStartupRegistrationService.cs`
- Create: `src/DuoCompanion.Services/Snap/GlobalHotkeyService.cs`, `StartupRegistrationService.cs`
- Modify: `src/DuoCompanion.Services/Win32/NativeMethods.cs`, `src/DuoCompanion.App/App.xaml.cs`, `AutoSpanCoordinatorService.cs`
- Modify: `src/DuoCompanion.Core/Models/DuoSnapSettings.cs`

**Produces:** hotkey registration status/errors, actions for Left/Right/Span/70-30/30-70, and per-user startup registration.

- [ ] **Step 1: Add failing Core parsing tests** for valid `Ctrl+Alt+S`, duplicate binding rejection, and unknown key rejection.
- [ ] **Step 2: Run** the parsing tests; expected failure.
- [ ] **Step 3: Implement** immutable `HotkeyBinding.Parse`, P/Invokes `RegisterHotKey`/`UnregisterHotKey`, a hidden-window message bridge owned by App, and run-key registration in `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`. Validate registration result, log failures, and unregister/remove on shutdown only when DuoCompanion created the value. Treat a title-bar hinge hold lasting configured dwell as a confirmed span gesture.
- [ ] **Step 4: Run** Core tests; expected PASS. On Windows manually verify each registration/unregistration and startup value cleanup.
- [ ] **Step 5: Stage** task files.

### Task 8: Complete Settings UI

**Files:**
- Modify: `src/DuoCompanion.App/Pages/SettingsPage.xaml`, `SettingsPage.xaml.cs`
- Modify: `src/DuoCompanion.Core/Models/AppSettings.cs`
- Modify: `tests/DuoCompanion.Tests/Services/SettingsServiceTests.cs`

**Produces:** controls for every `DuoSnapSettings` property, profile/ignore list management, validation feedback, Snap-mode confirmation, and hotkey conflict status.

- [ ] **Step 1: Add failing settings tests** that defaults contain the specified DuoSnap defaults and JSON deserialization of old settings retains them.
- [ ] **Step 2: Run** settings tests; expected failure.
- [ ] **Step 3: Implement** numeric controls with the Task 2 clamp ranges; ComboBoxes for restore and integration modes; editable list controls for executable names/profiles; a confirmation dialog before Disable mode; and handlers that save, then notify the settings monitor. Bind nothing directly to Win32 services from code-behind.
- [ ] **Step 4: Run** settings tests; expected PASS. On Windows manually open/reset/save/relaunch the Settings page.
- [ ] **Step 5: Stage** task files.

### Task 9: Integration, documentation, and Windows acceptance checklist

**Files:**
- Modify: `src/DuoCompanion.App/App.xaml.cs`, `README.md`
- Modify: `docs/snap-development-plan.md`
- Create: `docs/duosnap-windows-validation.md`

- [ ] **Step 1: Add a failing acceptance test** at the latest pure boundary: disabling Auto Span during candidacy produces exactly one exit and never calls `Span`.
- [ ] **Step 2: Run** the affected coordinator test; expected failure if the state cleanup is absent.
- [ ] **Step 3: Wire** all services into DI and deterministic launch/quit order: settings monitor, topology, tracker, hotkeys, profile service, overlay; shutdown reverses that order and hides overlay before dispatcher exit. Update README with implemented modes/settings/limitations and document Windows acceptance cases.
- [ ] **Step 4: Run on Windows:** `dotnet test DuoCompanion.sln`; `dotnet build DuoCompanion.sln -c Release`. Expected: zero failures/errors.
- [ ] **Step 5: Manually validate** mouse/touch/pen spans; vertical/horizontal/reverse orientations; one, two, three-monitor topology; each restore mode; ignore/profile behavior; all layout commands/hotkeys; suggestion confirmation; overlay fade; Disable-mode confirmation and quit cleanup; startup registration.
- [ ] **Step 6: Stage** all integration/docs files and verify `git diff --check`.

## Plan self-review

- Topology safety: Tasks 1 and 3.
- Input, preview, restore: Tasks 3-5 and 7.
- Settings, modes, startup: Tasks 2, 4, 5, 7, and 8.
- Layouts, profiles, hotkeys, gestures, suggestions: Tasks 2, 6, and 7.
- Documentation and Windows-only verification: Task 9.
