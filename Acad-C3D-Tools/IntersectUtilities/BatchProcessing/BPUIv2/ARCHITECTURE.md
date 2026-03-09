<section>
# BPUIv2 Architecture

<overview>
BPUIv2 is a composable batch processing framework for AutoCAD/Civil 3D.

- **Operations** are atomic, code-defined units of work (e.g., freeze a layer, attach an Xref, set a profile style).
- **Sequences** are user-composed ordered lists of operations, each with its own parameter values.
- **Three storage levels**: Predefined (hard-coded), User (`%AppData%`), Shared (network path with file-lock).
- **Drawing lists** are independent from sequences and managed separately.
</overview>
</section>

---

<section>
# Key Concepts

<overview>

## Operation

Every operation implements `IOperation`.

- `TypeId` — unique string identifier used for serialization and registry lookup.
- `Parameters` — list of `IOperationParameter` descriptors (dropdowns, text fields, booleans, filters, etc.).
- `Execute(OperationContext ctx)` — performs the work inside an open Transaction.

## Sequence

A sequence is an ordered list of `OperationStep` entries. Each step references an operation by `TypeId` and stores concrete parameter values. Sequences are serializable to JSON.

## Dataflow Model

Operations declare typed **inputs** (parameters) and **outputs**. Outputs from one step can be bound to inputs of a later step via `OutputBinding` (references a `SourceStepId` + `OutputName`). Unbound inputs are collected from the user at runtime via the **Inputs** dialog.

- **OutputDescriptor** — declares an output's name, display name, and type.
- **OutputBinding** — stored on `ParameterValue.Binding`, references a source step's output by StepId.
- **StepOutputs** — `OperationContext.StepOutputs` dictionary, populated by operations calling `SetOutput()`. The `BatchRunner` captures these after each step and uses them to resolve bindings for subsequent steps.
- **Hierarchy rule** — data flows strictly downward. An input can only bind to an output from an earlier step in the sequence.

## SharedState

A `Dictionary<string, object>` carried across drawings. Only used for **Counter** (cross-drawing accumulator). All inter-operation communication within a drawing uses the dataflow model above.

## OperationContext

Provided to every `Execute` call:

| Property | Description |
|---|---|
| `Database` | The currently open drawing's `Database` |
| `Transaction` | Active `Transaction` |
| `CivilDocument` | Civil 3D `CivilDocument` wrapper |
| `SharedState` | Cross-drawing state (Counter only) |
| `StepOutputs` | Per-step output dictionary — operations write here via `SetOutput()` |
| `DataReferences` | Optional data references options |

## Drawing List

A flat list of drawing file paths to process. Managed independently from sequences — the same drawing list can be reused across different sequences and vice versa.
</overview>
</section>

---

<section>
# Folder Structure

<overview>

```
BPUIv2/
├── Core/           Interfaces, base classes, parameter types, filter groups
├── Operations/     Organized by category:
│   ├── Layer/
│   ├── Xref/
│   ├── Alignment/
│   ├── Profile/
│   ├── ViewFrame/
│   ├── Viewport/
│   ├── Block/
│   ├── Detailing/
│   ├── DataShortcut/
│   └── Style/
├── Sequences/      Sequence model, storage service, predefined sequences
├── Registry/       Operation discovery and catalog
├── DrawingList/    Drawing list management
├── Execution/      Batch runner engine
├── Sampling/       Drawing sampling for parameter dropdowns
└── UI/             WPF controls and ViewModels
```
</overview>
</section>

---

<section>
# UI Architecture

<overview>

## Host: NsCmdPaletteSet

`NsCmdPaletteSet` lives in `CmdUI/UI/` (outside BPUIv2). It is the central AutoCAD palette set that hosts multiple tabs. BPUIv2 plugs into it as one tab.

## Controls

| Control | Type | Purpose |
|---|---|---|
| `BatchProcessingControl` | `UserControl` | Main tab inside the palette set. Shows the active sequence, drawing list summary, and run button. |
| `SequenceComposerWindow` | Modeless `Window` | Popup for composing/editing sequences: add operations, wire outputs→inputs, configure parameters. |
| `InputsDialog` | Modal `Window` | Runtime input collection: shows all sequence parameters, bound ones read-only, unbound ones editable. |
| `DrawingListDialog` | Modal `Window` | Select and manage the list of drawings to process. |
| `FilterEditorDialog` | Modal `Window` | Edit filter groups used by filter-type parameters. |

## Theming

`Theme.xaml` is sourced from the `DimensioneringV2` project and linked into BPUIv2 via the `.csproj` file (shared resource dictionary, not duplicated).
</overview>
</section>

---

<section>
# Execution Model

<overview>
Batch processing runs **synchronously on the AutoCAD main thread**. This is required because the AutoCAD API is single-threaded and `Database`/`Transaction` objects cannot cross thread boundaries.

- `Application.DoEvents()` is called periodically to keep the UI responsive.
- Progress is reported via the AutoCAD progress bar and `prdDbg` console output.
- Each drawing is opened, processed through every step in the sequence, then saved/closed before moving to the next.
</overview>
</section>

---

<section>
# Storage

<overview>

## Predefined Sequences

Hard-coded in `Sequences/` as static definitions. Cannot be modified by users.

## User Sequences

Stored as JSON files under `%AppData%`. Private to the current user and machine.

## Shared Sequences

Stored on a network path. Concurrent access is managed with file-level locking. A **50-backup rotation** keeps the last 50 versions of each shared sequence file to guard against corruption or accidental overwrites.
</overview>
</section>

---

<section>
# Adding New Operations

<overview>

<example>
### Step-by-step

1. **Create a class** in the appropriate `Operations/<Category>/` folder.
2. **Implement `IOperation`**:
   - Set a unique `TypeId` (e.g., `"Layer.FreezeByName"`).
   - Define `Parameters` — use built-in parameter types from `Core/` (string, dropdown, bool, filter, etc.).
   - Define `Outputs` — if the operation produces data for later steps, declare `OutputDescriptor` entries and call `SetOutput()` in Execute.
   - Implement `Execute(OperationContext ctx)` with the actual logic.
3. **Register** — the `Registry/` discovery mechanism picks up all `IOperation` implementations automatically (reflection-based scan).
4. **No UI changes needed** — the composer window dynamically renders parameter editors, output labels, and binding dropdowns based on descriptors.

### Conventions

- Keep operations atomic: one logical action per operation.
- Use the **dataflow model** (declared outputs + bindings) for inter-operation communication.
- `SharedState` is reserved for Counter only (cross-drawing accumulator).
- Prefer parameter descriptors over hard-coded values so users can configure behavior in the composer.
</example>
</overview>
</section>

---

<section>
# Implementation Status

<overview>

## Completed Phases

| Phase | Commit | What |
|-------|--------|------|
| 1+2 | `e2017a5c` | Core framework, 38 operations (10 categories), 30 predefined sequences |
| 3 | `43f4866c` | Execution engine (`BatchRunner`), `DrawingListService` |
| 4 | `b6117bdf` | `NsCmdPaletteSet` multi-tab hub, `BatchProcessingControl` palette tab |
| 5+6 | `9c4c7e43` | `DrawingListDialog` (modal), `SequenceComposerWindow` (modeless), converters, button wiring |
| 7 | `553438b` | Drawing sampling (`DrawingSampler`), user+shared sequence storage, filter editor |
| Dataflow | pending | Dataflow model: `OutputDescriptor`, `OutputBinding`, `StepOutputs`, binding UI in composer, `InputsDialog`, main UI restructure |

## Remaining Phases

### Phase 10: Polish + Migration
- Validation parity with v1
- Cancel cleanup, log section polish
- Deprecate old `Form1`/`ConfigPaletteSet`
- Run both v1 and v2 on same test drawings, compare results

</overview>
</section>

---

<section>
# Build & Technical Notes

<overview>

- **Build**: `dotnet build -p:WarningLevel=0` from `Acad-C3D-Tools/IntersectUtilities/`
- **ImplicitUsings**: `.csproj` has `<ImplicitUsings>enable</ImplicitUsings>` with `<Using Remove="System.Drawing"/>` and `<Using Remove="System.Windows.Forms"/>` to avoid WPF/WinForms type ambiguity
- **Namespace collision**: `Operations.Alignment` namespace collides with `Autodesk.Civil.DatabaseServices.Alignment` — use fully-qualified types
- **Theme.xaml paths**: From `UI/` depth use `../../../CmdUI/UI/Theme.xaml`; from `UI/SubFolder/` depth use `../../../../CmdUI/UI/Theme.xaml`
- **MVVM**: CommunityToolkit.Mvvm 8.4.0 — `[ObservableProperty]` on camelCase fields, `[RelayCommand]` on methods
- **Result type**: `using Result = IntersectUtilities.UtilsCommon.Result;` from `UtilitiesCommonSHARED/UtilsCommon.cs:4677-4730`
- **File-scoped namespaces** (C# 10+) throughout

</overview>
</section>
