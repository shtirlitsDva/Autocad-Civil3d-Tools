# SheetCreationAutomation Agent Guide

## Purpose
`SheetCreationAutomation` is an AutoCAD/Civil 3D plugin for sheet production workflows.

The `SCAUI` palette currently implements:
- **Step 1: View Frame automation**
- **Step 2: Sheet Creation automation**

Both workflows use:
- native in-process orchestration (no AHK process launch),
- Civil 3D/AutoCAD API for drawing/command lifecycle,
- Windows automation (`HWND`, `UIA`, `MSAA`) for wizard-only UI steps.

## Top-Level Structure
- `01 SheetCreationAutomation.cs`: AutoCAD command entry points and plugin lifecycle.
- `UI/`: WPF palette visuals, theme, AutoCAD UI context helpers.
- `ViewModels/`: MVVM state and command logic for palette tabs.
- `Procedures/ViewFrames/`: View Frame-specific automation orchestration and wizard driver.
- `Services/`: generic runtime infrastructure (waiters, window helpers, settings persistence, run log, overlays).
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
- Adds visual tab `SHEETS` using `SheetsAutomationControl` + `SheetsAutomationViewModel`.
- Adds visual tab `FINALIZE` using `FinalizeAutomationControl` + `FinalizeAutomationViewModel`.
- Adds visual tab `DEBUG` using `DebugVisualTreeControl` + `DebugVisualTreeViewModel`.

## View Frame Automation Flow (Current Runtime)
Implemented by `ViewModels/ViewFramesAutomationViewModel.cs` + `Procedures/ViewFrames/ViewFrameAutomationRunner.cs`.

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

## Sheet Creation Automation Flow (Current Runtime)
Implemented by `ViewModels/SheetsAutomationViewModel.cs` + `Procedures/Sheets/SheetAutomationRunner.cs`.

Run sequence:
1. Validate UI inputs.
2. Resolve drawing list from `fileList.txt`.
3. Execute runner in AutoCAD command context.
4. For each drawing:
- open drawing and activate it,
- send `_aecccreatesheets`,
- drive Create Sheets wizard with `Civil3dCreateSheetsUiDriver`,
- for profile mode: wait dynamic-input prompt and send profile origin coordinates,
- wait for command idle,
- close drawing without additional save.
5. Continue through all listed drawings.

Behavior constraints:
- retry poll interval: `200 ms`,
- per-step timeout: `1 minute`,
- dynamic-input wait timeout: `15 seconds`,
- max wizard retries per drawing (when dynamic-input prompt missing): `3`,
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
- `Models/SheetAutomationContext.cs`: immutable run input context for Step 2.
- `Models/CreateSheetsWizardRunOptions.cs`: per-drawing Create Sheets wizard options.
- `Models/SheetAutomationRunResult.cs`: Step 2 run outcome + failure payload.
- `Models/SheetsUiState.cs`: persisted tab-2 UI settings.
- `Models/FinalizeUiState.cs`: persisted tab-3 UI settings.
- `Models/AhkViewFrameSettings.cs`: legacy DTO from prior AHK-launch approach.

### Services
- `Services/AutomationWaiter.cs`: deterministic wait + timeout + overlay integration.
- `Services/IWaitOverlayPresenter.cs`: overlay abstraction.
- `Services/WaitOverlayPresenter.cs`: transparent status overlay during long waits.
- `Services/Win32WindowTools.cs`: Win32 enumeration/control helpers and `WindowMetadata`.
- `Services/MsaaTools.cs`: MSAA child-path value writes for custom MFC controls.
- `Services/AutomationSettingsStore.cs`: `%APPDATA%` persistence for palette state.
- `Services/AutomationRunLog.cs`: shared run log stream used by DEBUG tab and runtime services.
- `Services/AhkPathResolver.cs`: legacy helper from prior AHK-launch approach.

### Procedures/Common
- `Procedures/Common/DrawingAutomationRunnerBase.cs`: shared document activation + idle-wait mechanics.
- `Procedures/Common/WizardUiDriverBase.cs`: shared dialog waiting, title matching, button/edit helpers, selector trace logging.

### Procedures/ViewFrames
- `Procedures/ViewFrames/ViewFrameAutomationRunner.cs`: main per-drawing orchestration engine.
- `Procedures/ViewFrames/Civil3dWizardUiDriver.cs`: dialog/page automation via `HWND` + `UIA`.
- `Procedures/ViewFrames/ViewFrameCountService.cs`: direct DB traversal count of `ViewFrame`.

### Procedures/Sheets
- `Procedures/Sheets/SheetAutomationRunner.cs`: per-drawing orchestration for Create Sheets.
- `Procedures/Sheets/Civil3dCreateSheetsUiDriver.cs`: dialog/page automation for Create Sheets and profile wizard.

### ViewModels
- `ViewModels/ViewFramesAutomationViewModel.cs`: main tab state, commands, async start/cancel.
- `ViewModels/SheetsAutomationViewModel.cs`: tab 2 state and persistence for sheet settings.
- `ViewModels/FinalizeAutomationViewModel.cs`: tab 3 state and persistence for finalize settings.
- `ViewModels/DebugVisualTreeViewModel.cs`: live automation log projection + clear/copy commands.

### UI
- `UI/SheetAutomationPaletteSet.cs`: palette creation and visual tab registration.
- `UI/ViewFramesAutomationControl.xaml(.cs)`: Step 1 configuration and run controls.
- `UI/SheetsAutomationControl.xaml(.cs)`: tab 2 sheet settings UI.
- `UI/FinalizeAutomationControl.xaml(.cs)`: tab 3 finalize settings UI.
- `UI/DebugVisualTreeControl.xaml(.cs)`: shared runtime log tab.
- `UI/Theme.xaml`: shared style dictionary.
- `UI/PaletteExceptionGuard.cs`: catches unhandled WPF/task exceptions in AutoCAD host.
- `UI/AcContext.cs`: stores AutoCAD synchronization context for UI-thread command dispatch.

### Debug
- `Debug/DebugHelper.cs`: debug-time assembly resolver hook for local dev.

## Runtime Debug Tab Scope
`DEBUG` tab is the shared runtime log console:
- receives timestamped messages from automation services and viewmodels,
- supports clear and clipboard copy for fast diagnostics.

## State and Persistence
- UI values are stored by `AutomationSettingsStore` in:
`%APPDATA%\DRI\SheetCreationAutomation\viewframes-ui-state.json`
- `%APPDATA%\DRI\SheetCreationAutomation\sheets-ui-state.json`
- `%APPDATA%\DRI\SheetCreationAutomation\finalize-ui-state.json`

## Notes on Legacy Artifacts
The project still contains AHK-era classes (`AhkViewFrameSettings`, `AhkPathResolver`) for compatibility/history.
They are not the active execution path for `StartScript` in current Step 1 implementation.
