# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

This repo is a collection of AutoCAD / Civil 3D 2025 plugins (and supporting console tools) for district-heating design work. All source lives under `Acad-C3D-Tools/`, organized as one solution (`Acad-C3D-Tools.sln`) containing ~25 projects. The primary plugin is `IntersectUtilities/` — most other top-level folders are either supporting libraries (`*SHARED` projects, `PipelineSHARED`, `WpfSHARED`, `UtilitiesCommonSHARED`), companion plugins (`Dimensionering`, `NTRExport`, `ExportShapeFiles`, `NSLOAD`, `SheetCreationAutomation`, etc.), or standalone tools (`AcCoreConsoleAutomation`, `LERImporter`, `ReverseLayoutProfileLANDXML`).

Within `IntersectUtilities/`, code is grouped by feature area (e.g. `MPE/`, `LongitudinalProfiles/`, `PipelineNetworkSystem/`, `PipeScheduleV2/`, `FjernvarmeFremtidig/`, `LER2.0/`, `PlanDetailing/`, `PlanProduction/`, `GraphClasses/`, `Dimensionering/`). New commands typically live in a partial class named **`IntersectUtilites`** (note: this is the established spelling — keep it consistent when adding partials, e.g. `public partial class IntersectUtilites` in `MPE/PipePlan/PipePlanCommands.cs`).

## Building

**Use the provided `build-*.bat` scripts at the repo root for normal local builds.** `dotnet build` cannot resolve the COM references used by some projects, so these scripts shell out to MSBuild from VS 2022 Community:

| Script | Project | Configuration |
|---|---|---|
| `build-intersectutilities-debug.bat` | IntersectUtilities | Debug |
| `build-intersectutilities-release.bat` | IntersectUtilities | Release |
| `build-ntrexport-debug.bat` | NTRExport | Debug |
| `build-dimensioneringv2-debug.bat` | DimensioneringV2 | Debug |
| `build-dimensionering-debug.bat` | Dimensionering | Debug |
| `build-exportshapefiles-debug.bat` | ExportShapeFiles | Debug |
| `build-nstbl-debug.bat` | NSTBL | Debug |
| `build-sheetcreationautomation-debug.bat` | SheetCreationAutomation | Debug |
| `build-ntrexport-debug.bat` | NTRExport | Debug |

The .bat files invoke `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` with `/clp:ErrorsOnly;Summary`. Note: the paths inside the .bat files are hard-coded to `X:\GitHub\...` — if you're running from `C:\Users\mpe\GitHub\...`, edit the `PROJECT_PATH` line locally or invoke MSBuild directly.

When a project *does* build cleanly with `dotnet build` (no COM refs), always pass `--nologo -v q` and target a single `.csproj` so output is tractable:

```powershell
dotnet build ProjectName.csproj --nologo -v q
```

To filter to errors only: `... 2>&1 | Select-String -Pattern "error"` (PowerShell).

There is no test project / test suite in this repo. `NTRExport.ConsoleTests/` is a console harness, not unit tests.

## Project conventions

- **Target framework**: `net8.0-windows10.0.26100.0`, `x64`, `Nullable=enable`, `ImplicitUsings=enable`. AutoCAD 2025 (`R25.0`) and Civil 3D references are taken from `C:\Program Files\Autodesk\AutoCAD 2025\` with `<Private>False</Private>` so they aren't copied to output.
- **Indentation**: four spaces. PascalCase public types, camelCase locals. Avoid abbreviations not already used in the surrounding code.
- **Command class names** should match the AutoCAD command keyword (`ApplyDimCommand` for `APPLYDIM`, etc.).
- `.editorconfig` disables XML doc warnings (`CS1591`). Keep inline comments minimal — only when intent is non-obvious.
- New IntersectUtilities commands go in `partial class IntersectUtilites` (the misspelling is intentional and preserved across the codebase).
- Avoid `2>&1` on native executables from PowerShell — it wraps each stderr line in an ErrorRecord and sets `$?` to `$false` even on success.

## AutoCAD plugin deployment

Plugins are deployed as Autoloader `.bundle` packages (not via `NETLOAD`). The full pattern — `PackageContents.xml`, `Contents/Win64/` layout, which DLLs to bundle vs exclude (AutoCAD-shipped DLLs must never be bundled), and the MSBuild target that creates the bundle on Release — is documented in `docs/autocad-bundle-guide.md`. `NSLOAD.csproj` is the working reference implementation; it emits `Deploy/NSLOAD.bundle/` on Release builds.

Bundle DLLs are deployed to `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` (all users) or `%APPDATA%\Autodesk\ApplicationPlugins\` (current user). `SeriesMin`/`SeriesMax = R25.0` in `PackageContents.xml` is mandatory — without it AutoCAD may try to load the .NET 8 DLL into an older .NET Framework host.

For `IExtensionApplication.Initialize()` — defer any UI work (ribbon, palette) to `Application.Idle` because no document exists yet in Application context.

## Domain context

This is district-heating ("fjernvarme") CAD tooling. Two pipe-system types appear throughout the code:

- **Bonded pipes** — exist in all dimensions; supply and return are two distinct steel pipes each with their own jacket and insulation, drawn as **two distinct polylines**.
- **Twin pipes** — exist up to DN 200 (occasionally 250); two steel pipes share one plastic jacket with the return pipe above the supply pipe, drawn as **one polyline**.

When working with pipe drawing/sizing code, know which type is in scope — they have different polyline counts, different size limits, and different layer conventions.

## Multi-agent / worktree workflow

`MULTI_AGENT_WORKTREES_GUIDE.md` covers running multiple Claude Code sessions in parallel via `git worktree`. Worktrees live as sibling directories (`../Autocad-Civil3d-Tools-feature-x`). Each new worktree needs `dotnet restore` independently. `.cursor/worktrees.json` tracks worktrees for tooling integration.
