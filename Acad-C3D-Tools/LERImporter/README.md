# LERImporter

Imports LER 2.0 packages (Ledningspakke zips, GML) into a 2D drawing (`2DLER.dwg`) and one
3D drawing per package, with an AEC property set on every entity. Command: `IGMLBATCH` (alias
`IMPORTCONSOLIDATEDGMLBATCH`), which imports every zip in a folder.

Runs on Civil 3D 2025 and BricsCAD V26. General BricsCAD porting facts, including how the
property sets are written without the AEC API, are in
`X:\AutoCAD DRI - 01 Civil 3D\Dev\00 Bricscad porting\bricscad-porting.md`
(`<aec-property-sets>`). This file keeps only what is specific to LERImporter.

## Two hosts, one csproj

`NorsynHost` (`Directory.Build.props`, default `AutoCAD`) selects the host:

| | AutoCAD (Civil 3D) | BricsCAD |
|---|---|---|
| Build | MSBuild, as before | `-p:NorsynHost=BricsCAD` |
| Output | `bin\Debug\`, `bin\Release\` | `bin\BricsCAD\<Configuration>\` |
| `obj` | `obj\` | `obj\BricsCAD\` |
| Host files | `Host\Acad\` | `Host\Brx\` |
| UtilitiesCommonSHARED | imported | not imported (it needs Civil); its `DataManager\CsvData` is linked |

DevReload (BricsCAD): plugin `LERImporter`, prefix `LER`, `msbuildProperties`
`["NorsynHost=BricsCAD"]`, Debug.

## Host differences

- **Property sets.** `ConsolidatedCreator` calls `LerPropertySets.Define` / `Attach` /
  `ReadHatchLayerSets`, aliased to `Host\Acad` (AEC API) or `Host\Brx` (writes the AEC objects
  through `DwgIn`, codec in `AecStream.cs`). Both build their definition from
  `LerSetDef.FromType`: every `[PsInclude]` property, then GmlBemærkning and LerNummer.
  Only manual Integer / Real / Text / True-False properties exist.
- **Graveforespørgsel polygon.** Civil draws an `MPolygon` on `GraveforespPolygon`; BricsCAD has
  no MPolygon, so it draws a closed `Polyline` with the same vertices.
- **Layer colour.** A layer with no colour in the config gets ACI 0 on Civil and ACI 7 on
  BricsCAD, which refuses ByBlock on a layer (`ConsolidatedCreator.NoLayerColor`).
- **Hatches** are created with `Associative = false` on both hosts (BricsCAD defaults to true).
- **Folder prompt.** With `FILEDIA` 0, `IGMLBATCH` asks for the folder on the command line;
  otherwise it shows the folder dialog. Used for unattended runs on both hosts.
- `LerHatchLayers.BuildPlan` takes the set reader as a parameter, so IntersectUtilities keeps
  using the AEC API and LERImporter passes the host's reader.

## Tests

`LERImporter.Tests` (xUnit, no CAD): the AEC stream codec against streams Civil wrote
(`Golden\`, see its README), and the schema's value conversion.

```
dotnet test Acad-C3D-Tools/LERImporter.Tests/LERImporter.Tests.csproj
```

## How the port was verified (2026-10-10)

The 10 Svogerslev packages (`175-1579`, `06 LER\LER 2.0\behandling`) were imported on
BricsCAD V26.2 and on Civil 3D 2025 (accoreconsole). A Civil-side reader listed every
model-space entity with a geometry fingerprint and every property-set value. The two runs
were then compared per entity, independent of handles:
- 11 drawings, 75,767 entities and 1,708,597 values were identical;
- the only difference was the polygon (`MPolygon` vs closed `Polyline`);
- Civil's AUDIT found the same 8 errors in every drawing of both runs (template
  `AeccDbRootSettingsNode`);
- a cold BricsCAD session, with the AEC classes not yet registered, gave the same result.
