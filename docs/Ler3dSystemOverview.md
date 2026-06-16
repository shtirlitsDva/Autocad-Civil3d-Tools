<overview>
This document describes the existing **LER utility cross-section system**: how raw utility-register data becomes two deliverable AutoCAD drawings (a 2D plan underlay and a 3D model), and how the 3D drawing is consumed at runtime to draw utility crossings as cross sections in Civil 3D longitudinal profile views.

It is written contract-first. The **deliverable is the pair of DWG artifacts** (section "The artifact contract"). The runtime/consumer chain is documented only to justify *why* the artifact must look the way it does. The Danish importer that produces the artifacts today is described in two sentences at the end — it is one reference producer, not part of the contract.

Audience: an engineer/agent who must produce these artifacts from a new data source. Everything that source must satisfy lives in "The artifact contract" and "The Krydsninger hub".
</overview>

<the-big-picture>
```
 source utility data ──► [producer] ──► 2D DWG  (plan underlay, visual only)
                                   └──► 3D DWG  (the runtime data source)
                                              │
 project drawings ── Stier.csv ── DataManager.Ler() ── reads 3D DWG as side-database
                                              │
                                    Ler3dManager (file or folder mode)
                                              │
                              alignment ✕ 3D polylines = crossings
                                              │
                    CogoPoints + profile-view cross-section symbols + labels
```

Two drawings are produced from the **same source data**:

- **2D DWG** — flattened plan-view representation, used purely as a **visual underlay** when producing plan sheet sets. It is *never read programmatically*. Only its layers (→ colour/linetype) and geometry placement matter. It is **not** referenced by `Stier.csv` and is not loaded as a side-database.
- **3D DWG** — the runtime data source. Utilities are `Polyline3d` entities. This is the drawing every consumer command reads.

Everything below concerns the **3D DWG** unless stated otherwise.
</the-big-picture>

<the-artifact-contract>
A valid **3D DWG** must satisfy all of the following. This is the complete, non-negotiable contract.

<geometry>
- Each utility run is one **`Polyline3d`** entity in model space.
- Coordinates are taken **at face value** at runtime — there is no reprojection, transform, or georeferencing when the DWG is loaded (`DataManager.getDatabases` just `ReadDwgFile`s it read-only). Therefore the DWG **must already be in the project coordinate system**: EPSG:25832 (UTM32N / ETRS89), metres, shared origin with the project drawings.
- The Z of each vertex matters only for `-3D` layers (see elevation regimes).
</geometry>

<layer-naming-the-universal-key>
**The layer name of each `Polyline3d` is the single join key for the entire system.** Every other property is a CSV lookup keyed by that layer name. Two hard rules:

1. **Every layer must be registered as a `Navn` row in `Krydsninger.v2.csv`** (`X:\AutoCAD DRI - 01 Civil 3D\Conf\Krydsninger.v2.csv`). If a crossing polyline's layer is missing from the CSV (or its row lacks `Type`/`Description`), `CREATELERDATAPSS` aborts the whole run (`LongitudinalProfileTools.cs:740-752, 785-796`).
2. **Layer names carry an elevation-quality suffix**: `-3D` or `-2D`.
   - **`-3D`** = the utility has trustworthy surveyed elevation. Its `Krydsninger` row has `Type == "3D"`. At runtime the **real Z** of the polyline is read at the crossing point (`LongitudinalProfileTools.cs:837-862`) and shown as a `K: <elevation>` kote.
   - **`-2D`** = the utility has no trustworthy elevation. Its `Type` holds a depth-category instead. At runtime the polyline's Z is ignored; elevation = *surface elevation at XY minus a predefined depth* looked up from `Dybde.csv` by `Type`, and the label is tagged `"Kote Ukendt"` (`LongitudinalProfileTools.cs:825-836`, `755-759`).

   **The `-2D` layer suffix has nothing to do with the 2D underlay DWG.** Both `-2D` and `-3D` layers live in the *same* 3D DWG; the suffix only signals elevation-data quality.
</layer-naming-the-universal-key>

<property-sets-only-two-things-are-read>
Producers may attach any property sets they like, but the cross-section runtime reads **only two** kinds of value off a polyline:

1. **Diameter** — the `Krydsninger.Diameter` column holds a pointer of the form `{PropertySetName:PropertyName}`. The runtime parses it (`FindPropertySetParts`, `Utils.cs:177`) and reads that PS field as an **integer in millimetres** (`PropertySetManager.ReadNonDefinedPropertySetInt`, called at `LongitudinalProfileTools.cs:914-934`). Used for the crossing symbol size and relocability.
2. **Label fields** — the `Krydsninger.Description` column is a template. `{ColumnName}` tokens expand to other `Krydsninger` columns; the resulting recipe may contain `{PSName:PropName}` tokens, each resolved by reading that PS field **as a string** off the entity (`ConstructStringFromPSByRecipe`, `Utils.cs:1057-1086`). This is the *only* path by which owner/material/type text reaches the drawing.

Everything else a producer stores in property sets (owner, status, dates, accuracy class, …) is **carried for reference but never read** by the profile-view pipeline. The mandatory PS surface is therefore tiny: only the fields some `Krydsninger` row actually points at.
</property-sets-only-two-things-are-read>

<single-file-vs-folder-tiling>
`Ler3dManagerFactory.LoadLer3d` (`Ler3dManager.cs:316-334`) branches on how many DWGs `Stier.csv`'s `Ler` column resolves to:

- **1 DWG → `Ler3dManagerFile`** — no boundary; every crossing is accepted (`IsPointWithinPolygon` always returns `true`, `Ler3dManager.cs:125`).
- **≥2 DWGs → `Ler3dManagerFolder`** — the project area is **tiled** into geographic sub-areas, one DWG per tile. **Each tile DWG must contain exactly one `MPolygon`** marking that tile's coverage (`Ler3dManager.cs:147-182`). The boundary is used to (a) skip tiles an alignment doesn't touch and (b) reject crossing points outside the tile, preventing double-counting / phantom crossings at tile seams. **A missing `MPolygon` in folder mode deliberately crashes Civil** (`Ler3dManager.cs:160-176`).
</single-file-vs-folder-tiling>

<the-2d-deliverable>
The 2D DWG is a separate, flattened projection of the same source. Contract is **visual only**: correct geometry on correct layers so that `Lag-Ler2.0.csv` (keyed by suffix-stripped layer name) gives the right colour/linetype under a plan. No property-set field is read; no command loads it.
</the-2d-deliverable>
</the-artifact-contract>

<the-krydsninger-hub>
`Krydsninger.v2.csv` is the configuration hub. One row per layer (`Navn`), semicolon-delimited, accessed via the static facade `Csv.Krydsninger` (`UtilitiesCommonSHARED/DataManager/CsvData/Krydsninger.cs`). Columns: `Navn;Layer;Type;Distance;Block;Description;Diameter;Material;System;Status;Kommentar;Temperatur;Tryk;Label`.

The columns actually consumed by the cross-section path, and what each fans out to:

| Column | Role | Special values / format |
|---|---|---|
| `Navn` | The layer name = join key | must equal the `Polyline3d` layer exactly |
| `Type` | Elevation regime | `"3D"` → read real Z; `"IGNORE"` → skip entirely; otherwise a depth-category that keys `Dybde.csv` |
| `Distance` | Utility **category token** (not a number) | e.g. `FJV`, `VAND`, `GAS`, `AFLØB`, `EL_04`, `EL_10`… → keys `Distances.csv` for numeric clearances **and** drives relocability (`LerTypeBuilder`, `Relocability/LerTypeResolution.cs:58-126`) |
| `Diameter` | `{PSName:PropName}` pointer to the diameter PS field (int, mm) | empty ⇒ no diameter read |
| `Description` | Label template; expands `{Column}` then `{PSName:PropName}` tokens | must be non-empty or the run aborts |
| `Block` | Which `ProfileViewSymbol` to draw at the crossing | — |

Satellite CSVs, all keyed off values derived from the `Krydsninger` row (all under `X:\AutoCAD DRI - 01 Civil 3D\Conf\`):
- **`Dybde.csv`** — predefined burial depth per `Type` (used for `-2D` layers).
- **`Distances.csv`** — clearance offsets per `Distance` category.
- **`Lag-Ler2.0.v2.csv`** — colour/linetype per layer (suffix stripped before lookup, `LongitudinalProfileTools.cs:761-768`).

`Krydsninger`, `Lag-Ler` are **versioned** (active version from `ConfigurationManager`, e.g. `.v2.`); `Dybde`, `Distances`, `Stier` are not.
</the-krydsninger-hub>

<runtime-consumer-chain>
Documented to justify the contract; not part of the deliverable. Main entry points live in `LongitudinalProfiles/LongitudinalProfileTools.cs`.

1. **`CREATELERDATAPSS`** (primary, `:546+`) — for each alignment: `lman.GetIntersectingEntities(al)` returns the `Polyline3d`s that cross it (`Ler3dManager.cs:94-123 / 216-249`). For each crossing point it creates a Civil 3D **CogoPoint**, assigns elevation per the `-3D`/`-2D` regime, sets the point layer (suffix stripped), and writes a **`DriCrossingData`** property set on the point: `Diameter`, `Alignment`, `SourceEntityHandle` (a round-trip handle back to the source polyline), and `CanBeRelocated`.
2. **`POPULATEPROFILES`** (`:1276+`) — projects those CogoPoints onto the profile views as labels.
3. **`POPULATEDISTANCES`** (`:6495+`) — fetches the source polyline via `GetEntityByHandle`, reads `Distance` (clearance) and `Block`, and draws the cross-section symbol (arcs at clearance offsets) sized by the stored diameter, via `ProfileViewSymbolFactory` (`Detailing/ProfileViewSymbol/`).
4. **`LEROPENANDSELECT`** (`:7848+`) — uses `GetDatabaseByIdString` + `SourceEntityHandle` to open the source DWG and zoom to the originating polyline.

**Relocability**: each crossing computes `IsRelocatable(FjvPipe, LerKrydsning)` from the project pipe size at that station and the crossing's `LerType` (category + spatial), where `LerType` is built entirely from the `Krydsninger` `Distance` + `Type` columns — no extra classification is needed on the artifact.

The `ILer3dManager` contract (`Ler3dManager.cs:22-42`) is the seam between artifact and runtime: `GetIntersectingEntities`, `GetEntityByHandle`, `IsPointWithinPolygon`, `GetHandle`, `GetDatabaseBy*`.
</runtime-consumer-chain>

<loading-glue>
- **`Stier.csv`** (`UtilitiesCommonSHARED/DataManager/CsvData/Stier.cs`) maps `(PrjId, Etape)` → file paths. Columns: `PrjId;Etape;Ler;Surface;Alignments;Fremtid;Længdeprofiler`. The **`Ler`** column points at either a single 3D DWG or a directory of `*_3DLER.dwg` tiles.
- **`DataManager`** (`UtilitiesCommonSHARED/DataManager/DataManager.cs`) resolves those paths and opens each DWG **read-only, shared** (`new Database(false, true)` + `ReadDwgFile(..., OpenForReadAndAllShare, ...)`). `Ler()` returns the database list that `Ler3dManagerFactory` consumes. No coordinate transform is applied here.
</loading-glue>

<reference-producer-danish-lerimporter>
Today the artifacts are produced by **`LERImporter`** (`Acad-C3D-Tools/LERImporter/`), which deserializes the Danish national register's GML 3.2 export (LER 2.0 schema), draws each utility as a `Polyline3d` (3D) and `Polyline` (2D) on `Krydsninger`-registered layers, attaches reflection-built property sets, and emits one `2DLER.dwg` plus per-area `*_3DLER.dwg` files (with `MPolygon` tiles). It is **one reference producer**; nothing downstream depends on *how* the artifacts were made, only that they satisfy the contract above.
</reference-producer-danish-lerimporter>

<key-files>
| Concern | File |
|---|---|
| Runtime manager (artifact↔runtime seam) | `IntersectUtilities/LongitudinalProfiles/Ler3dManager/Ler3dManager.cs` |
| Crossing creation + elevation + PS read | `IntersectUtilities/LongitudinalProfiles/LongitudinalProfileTools.cs` |
| Relocability / LerType from Krydsninger | `IntersectUtilities/LongitudinalProfiles/Relocability/LerTypeResolution.cs` |
| Krydsninger CSV access | `UtilitiesCommonSHARED/DataManager/CsvData/Krydsninger.cs` |
| Path resolution per project/etape | `UtilitiesCommonSHARED/DataManager/CsvData/Stier.cs`, `DataManager/DataManager.cs` |
| PS read/write + definitions | `UtilitiesCommonSHARED/PropertySets/PropertySetManager.cs` |
| Description/diameter token parsing | `IntersectUtilities/Utils.cs` (`ProcessDescription`, `ConstructStringFromPSByRecipe`, `FindPropertySetParts`) |
| Reference producer | `Acad-C3D-Tools/LERImporter/` |
</key-files>
