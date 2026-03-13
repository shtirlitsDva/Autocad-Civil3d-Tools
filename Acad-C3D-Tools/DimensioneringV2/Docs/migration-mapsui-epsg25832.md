<section>
<overview>

# Migration: Mapsui EPSG:3857 → EPSG:25832 (UTM32N)

## Motivation

The map currently runs in EPSG:3857 (Web Mercator) only because Mapsui defaults to it.
All source data (AutoCAD, elevation/WCS, BBR buildings) is natively EPSG:25832.
Every feature gets reprojected 25832→3857 on import and 3857→25832 on export — pure overhead
that also introduces floating-point precision errors.

Switching Mapsui to EPSG:25832 eliminates all reprojection, removes the dual geometry model,
and unlocks Danish Datafordeler basemaps (Skærmkortet) that only serve EPSG:25832 tiles.

</overview>
</section>

<section>
<overview>

## What Changes

### 1. Custom Tile Schema for Skærmkortet `View1` matrix
- Define `ITileSchema` with EPSG:25832, origin (120000, 6500000), 16 zoom levels
- Resolutions: 1638.4, 819.2, 409.6, ... down to 0.05 m/px
- Tile size: 256×256
- Used by both Skærmkortet and Ortofoto (EPSG:25832 variant)

### 2. Swap Tile Layers in `BaseMapLayerFactory.cs`
- **Remove** CartoDB Dark/Labels layers (CARTO requires Enterprise license for commercial use)
- **Add** Skærmkortet klassisk: `https://wmts.datafordeler.dk/Dkskaermkort/topo_skaermkort_wmts/1.0.0/wmts?apikey=...`
- **Add** Skærmkortet dæmpet (muted variant): `topo_skaermkort_daempet` layer
- **Switch** Ortofoto to EPSG:25832 endpoint: `https://services.datafordeler.dk/GeoDanmarkOrto/orto_foraar_wmts/1.0.0/WMTS` with `View1` tile matrix
- All tile layers use same `DATAFORDELER_APIKEY` from `Infrastructure`

### 3. Change MemoryProvider CRS (~3 files)
- `MainWindowViewModel_MapLifecycle.cs`: CRS `"EPSG:3857"` → `"EPSG:25832"` (lines 100, 144)
- `MainWindowViewModel_BBRLayers.cs`: CRS `"EPSG:3857"` → `"EPSG:25832"` (lines 60, 71)

### 4. Delete Reprojection Calls (6 call sites)
- `Commands.cs` (~line 1020): remove 25832→3857 reprojection on import
- `BBRLayerService.cs` (~line 139): remove 25832→3857 reprojection
- `GraphCreationService.cs` (~line 200): remove 3857→25832 reprojection
- `Write2Dwg.cs` (MapCommands, ~line 23): remove 3857→25832 export reprojection
- `Dim2ImportDims.cs` (~line 40): remove 3857→25832 export reprojection
- `ProjectionService.cs`: can be deleted entirely (or kept for edge cases)

### 5. Simplify AnalysisFeature Geometry Model
- Currently: `Geometry` (3857 for display) + `Geometry25832` (for calculations)
- After: single geometry in 25832 used for both display and calculations
- `AnalysisFeature.cs`: remove dual storage, `Geometry` IS the 25832 geometry
- `Length` property already uses `Geometry25832.Length` — just point to `Geometry`

### 6. Update Serialization (backward compatible)
- `AnalysisFeatureDto.cs`: stop writing dual coordinates, read old format gracefully
- `AnalysisFeatureMsgDto.cs`: same for MessagePack binary format
- Old files with both geometries still load (ignore the 3857 copy)

### 7. Update Coordinate Tolerance
- `CoordinateTolerance.cs`: default to EPSG:25832 tolerance (0.001m) instead of 3857 (0.01)
- Remove `SetForEpsg()` switching — always UTM32N

</overview>
</section>

<section>
<overview>

## Base Map Options After Migration

| Map Type | Layer | Endpoint |
|----------|-------|----------|
| **Skærmkortet** (default) | `topo_skaermkort` | `wmts.datafordeler.dk/Dkskaermkort/topo_skaermkort_wmts/...` |
| **Skærmkortet dæmpet** | `topo_skaermkort_daempet` | `wmts.datafordeler.dk/Dkskaermkort/topo_skaermkort_daempet/...` |
| **Ortofoto** | `orto_foraar` | `services.datafordeler.dk/GeoDanmarkOrto/orto_foraar_wmts/...` |
| **Hybrid** | Ortofoto + Skærmkortet labels | Layered |
| **Off** | None | Black background |

All use `DATAFORDELER_APIKEY` from `Infra.json` on X: drive.

</overview>
</section>

<section>
<overview>

## Key Files

| File | Change |
|------|--------|
| `Services/BaseMapLayerFactory.cs` | Rewrite: custom tile schema + new tile sources |
| `UI/MainWindowViewModel_MapLifecycle.cs` | CRS string change |
| `UI/MainWindowViewModel_BBRLayers.cs` | CRS string change |
| `GraphFeatures/AnalysisFeature.cs` | Remove dual geometry model |
| `Serialization/AnalysisFeatureDto.cs` | Simplify, keep backward compat |
| `Serialization/Binary/AnalysisFeatureMsgDto.cs` | Simplify, keep backward compat |
| `Services/ProjectionService.cs` | Delete or gut |
| `Services/BBRLayerService.cs` | Remove reprojection call |
| `Services/GraphCreationService.cs` | Remove reprojection call |
| `Commands.cs` | Remove reprojection on import |
| `MapCommands/Write2Dwg.cs` | Remove reprojection on export |
| `MapCommands/Dim2ImportDims.cs` | Remove reprojection on export |
| `Geometry/CoordinateTolerance.cs` | Simplify to UTM-only |
| `UI/MainWindowViewModel.cs` | Update BaseMapType enum if needed |

</overview>
</section>

<section>
<overview>

## Verification

1. Build with `dotnet build DimensioneringV2.csproj -p:WarningLevel=0`
2. Load an existing saved network (old format with dual geometries) — should still work
3. Verify Skærmkortet tiles render correctly at various zoom levels
4. Verify ortofoto tiles render (EPSG:25832 variant)
5. Import features from AutoCAD drawing — should appear at correct positions without reprojection
6. Export back to DWG — coordinates should match original
7. Elevation sampling — should work unchanged (already EPSG:25832)
8. BBR building overlay — should display at correct positions

</overview>
</section>
