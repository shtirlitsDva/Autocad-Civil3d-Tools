# PipePlan

PipePlan is a constrained plan-view pipe-drafting feature inside the `IntersectUtilities` AutoCAD / Civil 3D 2025 plugin. It collects plan points, validates them against per-DN bend radii, and bakes the result as a metadata-enabled polyline that can later be edited, continued, split, or width-updated through the NSPalette workflow.

## Commands

| Command | Purpose |
|---|---|
| `PPDRAW` | Start a new draft, or continue from an existing PipePlan polyline. The active FJV layer determines the pipe system, type, and DN. The per-DN bending radius comes from `PPSETTINGS`. |
| `PPCONVERT` | Convert an existing polyline on a recognised FJV layer into a PipePlan-managed object. Sharp interior corners are filleted at the project minimum bending radius; preview circles are shown before the conversion proceeds. |
| `PPSPLIT` | Split a metadata-enabled PipePlan polyline at a chosen straight portion into two independent PipePlan polylines. Arc regions and invalid split positions are rejected. |
| `PPCOLLAPSE` | Remove negligible bends. Any fillet whose **sagitta** — the distance from the arc midpoint to the midpoint of the chord between its tangent points, equal to `R·(1 − cos(δ/2))` — is at or below a threshold (default `0.01`) is collapsed by deleting its control vertex. A live preview shows the resulting pipe (green) and red markers on the vertices to be removed; Enter confirms, a new value re-previews, Esc cancels. |
| `PPEDIT` | Move control vertices or segment handles with live constraint preview; the `Radius` keyword at the drag prompt changes the bend radius of the selected vertex. The top-level prompt also offers two modes: **`Add`** (prompts for the bend radius — per-DN default or custom — then previews the new vertex following the cursor on the nearest segment; click to place, `Back` to return) and **`Delete`** (hover a vertex to preview the pipe with it removed; click to delete, `Back` to return). Infeasible edits are rejected. |
| `PPSETTINGS` | Palette for editing the per-DN bending radii (ProjekteringsRadius) and the straight-snap tolerance. Overrides are saved into the active drawing. |

## Drawing model

- **Width**: PPDRAW preview and bake set `ConstantWidth = GetPipeKOd(layer, S2) / 1000`. Series S2 is pinned at draw time. To render at S1 or S3, change the NSPalette series and click `Polylinjer bredde opdater` — same lookup, different series argument.
- **Preview colours**: green when the selected radius fits, red when one or more bends do not fit, blue when Ctrl is actively snapping to the previous straight segment.
- **Bake output**: polyline with arc bulges on the active FJV layer. Two metadata records are attached:
  - `pipeTag` XData (system, type, DN) — picked up by Opdater and other layer-aware tooling.
  - `pipeGeometryData` Xrecord in the polyline's extension dictionary — control points, per-vertex bend radii, and the straight-snap tolerance. Required for `PPEDIT`, `PPSPLIT`, and PPDRAW continue.
- **Snap tolerance**: defaults to `5`, edited at the top of `PPSETTINGS`.

## Build

Build via the repo-root batch scripts:

```text
build-intersectutilities-debug.bat
build-intersectutilities-release.bat
```

These invoke MSBuild against `Acad-C3D-Tools/IntersectUtilities/IntersectUtilities.csproj`. The `PROJECT_PATH` line in the `.bat` files is hard-coded to `X:\GitHub\...` — edit it locally, or call MSBuild directly:

```text
"C:\Program Files\Microsoft Visual Studio\<edition>\MSBuild\Current\Bin\MSBuild.exe" `
    Acad-C3D-Tools\IntersectUtilities\IntersectUtilities.csproj `
    /t:Build /p:Configuration=Debug /clp:ErrorsOnly;Summary
```

`dotnet build` cannot resolve the COM references and is not supported.

## Load

`IntersectUtilities` deploys as an AutoCAD Autoloader `.bundle` package, not via `NETLOAD`. Deploy under either:

- `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` (all users), or
- `%APPDATA%\Autodesk\ApplicationPlugins\` (current user).

See `docs/autocad-bundle-guide.md` for the bundle layout. AutoCAD 2025 picks up the bundle on next launch.

## Typical workflow

1. Open NSPalette and activate a size (`FJV-TWIN-DN65`, `FJV-TWIN-ALUPEX50`, …) — this sets the active FJV layer.
2. Run `PPDRAW`, place points, bake.
3. (Optional) To draw at a series other than S2, change the NSPalette series and click `Polylinjer bredde opdater` to re-apply widths.
4. Edit later with `PPEDIT`; split with `PPSPLIT`; convert a pre-existing hand-drawn FJV polyline with `PPCONVERT`.

## Notes / limitations

- `PPEDIT` and `PPSPLIT` require metadata-enabled PipePlan polylines. For pre-metadata polylines or polylines drawn outside PipePlan, run `PPCONVERT` first.
- Closed polylines are not supported.
- `Enkelt` steel (single-pipe steel) is not currently supported. Accepted combinations: `Stål Twin`, `AluPex Twin`, `AluPex Frem`, `AluPex Retur`.
- Open metadata-drift items (see `TODO.md`): vertex deletion outside PipePlan can desync metadata from geometry and break `PPEDIT` / PPDRAW continue.
