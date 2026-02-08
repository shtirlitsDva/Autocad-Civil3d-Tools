# SheetCreationAutomation Agent Guide

## Purpose
`SheetCreationAutomation` is an AutoCAD/Civil 3D plugin for sheet production workflows.

The `SCAUI` palette currently implements **Step 1: View Frame automation** with:
- native in-process orchestration (no AHK process launch),
- Civil 3D/AutoCAD API for drawing/command lifecycle,
- Windows automation (`HWND` + `UIA`) for wizard-only UI steps.

## Top-Level Structure
- `01 SheetCreationAutomation.cs`: AutoCAD command entry points and plugin lifecycle.
- `UI/`: WPF palette visuals, theme, AutoCAD UI context helpers.
- `ViewModels/`: MVVM state and command logic for palette tabs.
- `Services/`: automation runtime engine, wizard UI driver, waiting/retry, counting, inspection helpers.
- `Models/`: context/options/results/state DTOs used by viewmodels and services.
- `Debug/`: debug assembly loader used in debug builds.

## Command Entry Points
Defined in `01 SheetCreationAutomation.cs`:
- `CREATEREFERENCETOALLALIGNMENTSHORTCUTS`
- `LISTNUMBEROFVIEWFRAMES` (command-line count output)
- `CREATEREFERENCETOPROFILES`
- `SCAUI` (opens palette with automation tabs)

`SCAUI` also caches AutoCAD synchronization context into `UI/AcContext.cs`.

## Palette + MVVM Composition
`UI/SheetAutomationPaletteSet.cs`:
- Adds visual tab `VIEW FRAMES` using `ViewFramesAutomationControl` + `ViewFramesAutomationViewModel`.
- Adds visual tab `DEBUG` using `DebugVisualTreeControl` + `DebugVisualTreeViewModel`.

## View Frame Automation Flow (Current Runtime)
Implemented by `ViewModels/ViewFramesAutomationViewModel.cs` + `Services/ViewFrameAutomationRunner.cs`.

Run sequence:
1. Validate UI inputs.
2. Resolve drawing list from `fileList.txt`.
3. Execute runner in AutoCAD command context.
4. For each drawing:
- open drawing,
- pre-count existing `ViewFrame` objects,
- fail if pre-count > 0,
- send `_aecccreateviewframes`,
- drive wizard with `Civil3dWizardUiDriver`,
- wait for command idle,
- post-count `ViewFrame`,
- increment next counter by delta,
- save and close drawing.
5. Update `VfNumber` in UI with final next counter.

Behavior constraints:
- retry poll interval: `200 ms`,
- per-step timeout: `1 minute`,
- wait overlay appears after `5 seconds`,
- cancellation supported from UI.

## Class Inventory and Responsibilities

### Models
- `Models/ViewFrameAutomationContext.cs`: immutable run input context.
- `Models/WizardRunOptions.cs`: per-drawing wizard options.
- `Models/WaitPolicy.cs`: polling/timeout/overlay thresholds.
- `Models/AutomationFailureInfo.cs`: structured failure diagnostics.
- `Models/AutomationRunResult.cs`: run outcome + final counter.
- `Models/ViewFramesUiState.cs`: persisted palette UI settings.
- `Models/AhkViewFrameSettings.cs`: legacy DTO from prior AHK-launch approach.

### Services
- `Services/IViewFrameAutomationRunner.cs`: runner contract.
- `Services/ViewFrameAutomationRunner.cs`: main orchestration engine.
- `Services/IWizardUiDriver.cs`: wizard-driving contract.
- `Services/Civil3dWizardUiDriver.cs`: dialog/page automation via `HWND` + `UIA`.
- `Services/IViewFrameCountService.cs`: counting abstraction.
- `Services/ViewFrameCountService.cs`: direct DB traversal count of `ViewFrame`.
- `Services/AutomationWaiter.cs`: deterministic wait + timeout + overlay integration.
- `Services/IWaitOverlayPresenter.cs`: overlay abstraction.
- `Services/WaitOverlayPresenter.cs`: transparent status overlay during long waits.
- `Services/Win32WindowTools.cs`: Win32 enumeration/control helpers and `WindowMetadata`.
- `Services/AutomationSettingsStore.cs`: `%APPDATA%` persistence for palette state.
- `Services/AhkPathResolver.cs`: legacy helper from prior AHK-launch approach.

### ViewModels
- `ViewModels/ViewFramesAutomationViewModel.cs`: main tab state, commands, async start/cancel.
- `ViewModels/DebugVisualTreeViewModel.cs`: runtime window inspector data source (`HWND` tree + UIA details).
- `ViewModels/WindowNodeViewModel` (same file): debug node state for tree binding.

### UI
- `UI/SheetAutomationPaletteSet.cs`: palette creation and visual tab registration.
- `UI/ViewFramesAutomationControl.xaml(.cs)`: Step 1 configuration and run controls.
- `UI/DebugVisualTreeControl.xaml(.cs)`: runtime inspector tab.
- `UI/Theme.xaml`: shared style dictionary.
- `UI/PaletteExceptionGuard.cs`: catches unhandled WPF/task exceptions in AutoCAD host.
- `UI/AcContext.cs`: stores AutoCAD synchronization context for UI-thread command dispatch.

### Debug
- `Debug/DebugHelper.cs`: debug-time assembly resolver hook for local dev.

## Runtime Debug Tab Scope
`DEBUG` tab is for **Civil 3D runtime window inspection**, not WPF tree inspection:
- shows `HWND` hierarchy,
- displays class/title/process/thread/visibility/enabled state,
- displays UIA metadata and supported patterns for selected node.

## State and Persistence
- UI values are stored by `AutomationSettingsStore` in:
`%APPDATA%\DRI\SheetCreationAutomation\viewframes-ui-state.json`

## Notes on Legacy Artifacts
The project still contains AHK-era classes (`AhkViewFrameSettings`, `AhkPathResolver`) for compatibility/history.
They are not the active execution path for `StartScript` in current Step 1 implementation.
