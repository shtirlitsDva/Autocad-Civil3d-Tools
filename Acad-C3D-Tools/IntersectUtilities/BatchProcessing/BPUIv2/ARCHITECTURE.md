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

## SharedState

A `Dictionary<string, object>` carried across all operation executions within a single drawing. Used for inter-operation communication:

- **Counter** — accumulate counts across operations.
- **DRO** — pass data-referenced object handles.
- **Detach/Attach flow** — one operation detaches Xrefs and stores paths; a later operation re-attaches them.

## OperationContext

Provided to every `Execute` call:

| Property | Description |
|---|---|
| `Database` | The currently open drawing's `Database` |
| `Transaction` | Active `Transaction` |
| `CivilDocument` | Civil 3D `CivilDocument` wrapper |
| `SharedState` | The per-drawing shared dictionary |

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
| `SequenceComposerWindow` | Modeless `Window` | Popup for composing/editing sequences: drag-and-drop operations, configure parameters. |
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
   - Implement `Execute(OperationContext ctx)` with the actual logic.
3. **Register** — the `Registry/` discovery mechanism picks up all `IOperation` implementations automatically (reflection-based scan).
4. **No UI changes needed** — the composer window dynamically renders parameter editors based on the parameter descriptors.

### Conventions

- Keep operations atomic: one logical action per operation.
- Use `SharedState` only when cross-operation communication is genuinely needed.
- Prefer parameter descriptors over hard-coded values so users can configure behavior in the composer.
</example>
</overview>
</section>
