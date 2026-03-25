# Repo Method Map

Source basis: inspected repository source on 2026-03-25.
Scope of analysis: read-only scan of projects outside `MPE`; this file is written inside `MPE`.
Purpose: quick lookup for existing reusable methods, classes, and command entry points before building new tools.

## 1. Solution shape

Main solution file:
- `Acad-C3D-Tools/Acad-C3D-Tools.sln`

Main executable/plugin projects:
- `IntersectUtilities`
- `NTRExport`
- `ExportShapeFiles`
- `SheetCreationAutomation`
- `Dimensionering`
- `NSLOAD`
- `NSTBL`
- `IsoTools`
- `LERImporter`
- `Ler2PolygonSplitting`
- `AcadOverrules`
- `Net-Reload`

Shared code projects with the highest reuse potential:
- `AutoCADCommandsSHARED`
- `UtilitiesCommonSHARED`
- `PipelineSHARED`
- `FormsSHARED`
- `WpfSHARED`

## 2. Command hotspots

These files contain the highest number of `[CommandMethod(...)]` entry points and are the fastest places to inspect for existing tool behavior:

| Command count | File | Notes |
|---|---|---|
| 95 | `Acad-C3D-Tools/IntersectUtilities/Intersect.cs` | General command kitchen-sink; many legacy and utility commands |
| 60 | `Acad-C3D-Tools/AutoCADCommandsSHARED/Test.cs` | Test and utility commands; useful examples of API usage |
| 38 | `Acad-C3D-Tools/IntersectUtilities/LongitudinalProfiles/LongitudinalProfileTools.cs` | Profile generation and detailing workflows |
| 25 | `Acad-C3D-Tools/IntersectUtilities/LER2.0/LER2.0.cs` | LER processing and cleanup |
| 21 | `Acad-C3D-Tools/IntersectUtilities/PlanDetailing/PlanDetailing.cs` | Detailing placement commands |
| 21 | `Acad-C3D-Tools/Dimensionering/Dimensionering.cs` | District-heating/dimensioning workflows |
| 14 | `Acad-C3D-Tools/IntersectUtilities/PlanProduction/PlanProduction.cs` | Sheet/view frame/export work |
| 12 | `Acad-C3D-Tools/IntersectUtilities/LongitudinalProfiles/AutoProfile/Auto Profile.cs` | AutoProfile pipeline |
| 10 | `Acad-C3D-Tools/IntersectUtilities/PipeSettingsCommands.cs` | Pipe settings CRUD/validation |
| 9 | `Acad-C3D-Tools/IntersectUtilities/PipelineNetworkSystem/PipelineNetworkCommands.cs` | Graph and cut-length workflows |

If you want to know whether a command already exists, search these files first.

## 3. Highest-value reusable APIs

### 3.1 AutoCAD interaction, selection, and document I/O

File:
- `Acad-C3D-Tools/AutoCADCommandsSHARED/Interaction.cs`

High-value methods:
- `Write(...)`, `WriteLine(...)`
- `GetString(...)`, `GetKeywords(...)`, `GetValue(...)`, `GetDistance(...)`, `GetInteger(...)`, `GetAngle(...)`
- `GetPoint(...)`, `GetLineEndPoint(...)`, `GetCorner(...)`, `GetExtents(...)`
- `GetEntity(...)`, `GetPick(...)`, `GetSelection(...)`, `GetWindowSelection(...)`, `GetCrossingSelection(...)`
- `GetPickSet()`, `SetPickSet(...)`
- `SetCurrentLayer(...)`
- `Command(...)`, `StartCommand(...)`
- `HighlightObjects(...)`, `UnhighlightObjects(...)`, `ZoomObjects(...)`, `ZoomView(...)`, `ZoomExtents()`
- `InsertEntity(...)`, `InsertScalingEntity(...)`, `InsertRotationEntity(...)`
- `SaveFileDialogBySystem(...)`, `OpenFileDialogBySystem(...)`, `FolderDialog(...)`
- `GetPromptPolyline(...)`
- `StartDrag(...)`

Use when:
- a new tool needs prompting, selection, zooming, highlighting, command dispatch, or simple entity insertion.

File:
- `Acad-C3D-Tools/AutoCADCommandsSHARED/QuickSelection.cs`

High-value methods:
- `QWhere(...)`, `QPick(...)`, `QSelect(...)`
- `QOpenForRead(...)`, `QOpenForWrite(...)`
- `QForEach(...)`
- `QCount(...)`, `QMin(...)`, `QMax(...)`, `QMinEntity(...)`, `QMaxEntity(...)`
- `SelectAll(...)`
- `FilterList.Create()`, `DxfType(...)`, `Layer(...)`, `Filter(...)`

Use when:
- you already have `ObjectId` collections and want concise read/write/filter operations.

File:
- `Acad-C3D-Tools/AutoCADCommandsSHARED/Layouts.cs`

High-value methods:
- `SetViewport(...)`
- `SetConfiguration(...)`
- `GetModelCoord(...)`
- `GetLayoutCoord(...)`

Use when:
- the tool touches paperspace/modelspace transforms or viewport setup.

File:
- `Acad-C3D-Tools/AutoCADCommandsSHARED/Internal/CustomDictionary.cs`

High-value methods:
- `GetValue(...)`, `SetValue(...)`
- `GetDictionaryNames()`, `GetEntryNames(...)`
- `RemoveEntry(...)`
- object-level overloads for `ObjectId`

Use when:
- the tool needs lightweight named metadata at drawing level or entity level.

### 3.2 Shared geometry, entity, and drawing utilities

File:
- `Acad-C3D-Tools/UtilitiesCommonSHARED/UtilsCommon.cs`

High-value methods by theme:

Coordinate conversion:
- `ToWGS84FromUtm32N(...)`
- `ToUtm32NFromWGS84(...)`

Geometry/entity helpers:
- `GetHorizontalLength(...)`
- `DistanceHorizontalTo(...)`
- `GetCoincidentIndexAtPoint(...)`
- `IntersectWithValidation(...)`
- `GetSamplePoints(...)`
- `GetDouglasPeukerReducedCopy(...)`
- `GroupConnected(...)`

Blocks/attributes:
- `SetAttributeStringValue(...)`
- `GetAttributeStringValue(...)`
- `CheckOrImportBlockRecord(...)`
- `CreateBlockWithAttributes(...)`
- `SynchronizeAttributes(...)`
- `ResetAttributesValues(...)`
- `ReadDynamicPropertyValue(...)`
- `GetNestedBlocksByName(...)`

Layers/database queries:
- `CheckOrCreateLayer(...)`
- `ListLayers(...)`
- `HashSetOfIds<T>(...)`
- `HashSetIdsOfType<T>(...)`
- `ListOfType<T>(...)`
- `HashSetOfType<T>(...)`
- `HashSetOfTypeWithPs<T>(...)`
- `GetFjvEntities(...)`
- `GetFjvBlocks(...)`
- `GetFjvPipes(...)`
- `GetBlockReferenceByName(...)`

Alignment/profile helpers:
- `SampleElevation(...)`
- `ToPolyline(this Profile, ProfileView)`
- `StationAtPoint(this Alignment, ...)`
- `GetPolyline(this Alignment)`

Use when:
- a tool needs common AutoCAD/Civil3D geometry, block, layer, or typed entity enumeration.

File:
- `Acad-C3D-Tools/IntersectUtilities/Utils.cs`

High-value methods:
- `GetPolyPoints(...)`
- `PolyClean_RemoveDuplicatedVertex(...)`
- `FilterForCrossingEntities(...)`
- `FindPropertySetParts(...)`
- `MapValueFromObject(...)`
- `AddToBlock(...)`
- `EraseBlock(...)`
- `CreateProfileFromPolyline(...)`
- `CreateDistTuples(...)`
- `GetFirstEntityOfType<T>(...)`
- `DisplayDynBlockProperties(...)`
- `SetDynBlockProperty(...)`
- `SetDynBlockPropertyObject(...)`
- `GetSortedQueue(...)`
- `RemoveColinearVerticesPolyline(...)`
- `RemoveColinearVertices3dPolyline(...)`
- `Union(...)`
- `PolylineFromConvexHull(...)`
- `ConstructStringFromPSByRecipe(...)`
- `ProcessDescription(...)`
- `GetOverlapStatus(...)`
- `ListOfType<T>(...)`
- `GetStationOffset(...)`
- `GetEntityPipeType(...)`
- `GetProfileViewAtStation(...)`
- `IsFullPath(...)`, `GetAbsolutePath(...)`
- viewport transform helpers:
  - `GetModelToPaperTransform(...)`
  - `GetPaperToModelTransform(...)`
  - `PaperToModel(...)`
  - `ModelToPaper(...)`

Use when:
- the tool is district-heating specific, profile/view-frame specific, or needs existing FJV conventions.

### 3.3 Property sets and structured metadata

File:
- `Acad-C3D-Tools/UtilitiesCommonSHARED/PropertySets/PropertySetManager.cs`

High-value methods:
- `GetOrAttachPropertySet(...)`
- `ReadPropertyString(...)`, `ReadPropertyBool(...)`, `ReadPropertyDouble(...)`, `ReadPropertyInt(...)`
- `WritePropertyString(...)`, `WritePropertyObject(...)`, `WritePropertyInt(...)`
- `CopyAllProperties(...)`
- `TryReadProperty(...)`
- `ReadNonDefinedPropertySetObject(...)`
- `TryReadNonDefinedPropertySetObject(...)`
- `ReadNonDefinedPropertySetDouble(...)`
- `ReadNonDefinedPropertySetInt(...)`
- `ReadNonDefinedPropertySetString(...)`
- `WriteNonDefinedPropertySetString(...)`
- `WriteNonDefinedPropertySetDouble(...)`
- `AttachNonDefinedPropertySet(...)`
- `PopulateNonDefinedPropertySet(...)`
- `UpdatePropertySetDefinition(...)`
- `SelectByPsValue(...)`
- `ListUniquePsData(...)`
- `AllPropertyNames(...)`
- `AllPropertyNamesAndDataType(...)`
- `IsPropertySetAttached(...)`
- `DumpAllProperties(...)`
- `GetPropertySetNames(...)`
- `GetPropertyNamesAndDataTypes(...)`
- `DeleteAllPropertySets(...)`
- `GetAllPropertyValues(...)`
- `ConvertPropertyValuesToStrings(...)`
- `GetOrCreatePropertySetDefinition(...)`

Specialized helper:
- `PSM_Pipeline.BelongsToAlignment(...)`

Use when:
- the tool reads/writes AutoCAD Property Sets instead of custom dictionaries or loose attributes.

### 3.3.1 BBR-specific property-set support

Files:
- `Acad-C3D-Tools/UtilitiesCommonSHARED/PropertySets/PropertySetManager.cs`
- `Acad-C3D-Tools/UtilitiesCommonSHARED/Schema/DF BBR Bygning Class.cs`
- `Acad-C3D-Tools/UtilitiesCommonSHARED/Schema/DF BBR Enhed Class.cs`

Key BBR surfaces:
- `PSetDefs.BBR : PSetDef`
- `BBR : PropertySetManager`
- `DefinedSets.BBR`

What exists:
- a dedicated BBR property-set definition
- a BBR wrapper class around `BlockReference` entities
- schema/model classes for BBR building data and BBR unit data

Use when:
- the tool needs to read, write, clone, analyze, or export building-related BBR data already stored in drawings.

### 3.4 Project file/data resolution

File:
- `Acad-C3D-Tools/UtilitiesCommonSHARED/DataManager/DataManager.cs`

High-value methods:
- `IsValid()`
- `Fremtid()`
- `Surface()`
- `Alignments()`
- `Ler()`
- `Længdeprofiler()`
- `PathToFremtid()`
- `PathToSurface()`
- `PathToAlignments()`
- `PathToLer()`
- `PathToLængdeprofiler()`

Use when:
- a tool needs the repo/domain conventions for related DWG/data files per project and stage.

## 4. Pipeline and district-heating domain reuse

### 4.1 Pipe settings

File:
- `Acad-C3D-Tools/PipelineSHARED/PipeSettings.cs`

High-value methods:
- `PipeSettingsCollection.GetSettingsFileNameWithPath()`
- `PipeSettingsCollection.Load()`
- `PipeSettingsCollection.Load(string settingsFileName)`
- `PipeSettingsCollection.LoadWithValidation(...)`
- `ListSettings()`
- dictionary-style CRUD via `Add(...)`, `Remove(...)`, `TryGetValue(...)`

Use when:
- a tool should reuse configured pipe system/type/size settings rather than hardcoding dimensions.

### 4.2 Pipeline network and graph workflow

File:
- `Acad-C3D-Tools/PipelineSHARED/PipelineNetwork.cs`

High-value methods:
- `CreatePipelineNetwork(...)`
- `CreatePipelineGraph()`
- `GetPipeline(...)`
- `PipelineGraphsToDot()`
- `SegmentGraphsToDot()`
- `AutoReversePolylines()`
- `AutoCorrectLengths()`
- `CreateSizeArrays()`
- `GetAllSizeArrays(...)`
- `PrintSizeArrays()`
- graph worker methods:
  - `BuildPipelineGraphs(...)`
  - `AutoReversePolylines(...)`
  - `CorrectPipesToCutLengths(...)`

File:
- `Acad-C3D-Tools/PipelineSHARED/PipelineV2.cs`

High-value methods:
- `GetMaxDN()`
- `CreateSizeArray()`
- `AutoReversePolylines(...)`
- `GetLocationForMaxDN()`
- `DetermineUnconnectedEndPoint(...)`
- `CorrectPipesToCutLengths(...)`
- `GetDistanceToPoint(...)`
- `GetPolylines()`
- `PopulateSegments(...)`
- factory:
  - `PipelineV2Factory.Create(...)`

Use when:
- the tool needs graph-based pipe traversal, connectivity, reverse-direction correction, or cut-length correction.

## 5. Batch-processing framework for future tools

This is one of the best extension points in the repo.

File:
- `Acad-C3D-Tools/IntersectUtilities/BatchProcessing/BPUIv2/Registry/OperationRegistry.cs`

Key behavior:
- reflection-based discovery of all `IOperation` implementations in the assembly
- lookup via `GetOperation(string typeId)`
- catalog access via `Catalog`

File:
- `Acad-C3D-Tools/IntersectUtilities/BatchProcessing/BPUIv2/Core/OperationBase.cs`

Key extensibility contract:
- implement:
  - `TypeId`
  - `DisplayName`
  - `Description`
  - `Category`
  - `Parameters`
  - `Execute(OperationContext context, IReadOnlyDictionary<string, object> parameterValues)`
- helpers:
  - `GetParam<T>(...)`
  - `GetParamOrDefault<T>(...)`
  - `GetStringParam(...)`
  - `GetIntParam(...)`
  - `SetOutput(...)`
  - `GetCounter(...)`

Useful surrounding services:

File:
- `Acad-C3D-Tools/IntersectUtilities/BatchProcessing/BPUIv2/Execution/BatchRunner.cs`

Method:
- `Run(...)`

File:
- `Acad-C3D-Tools/IntersectUtilities/BatchProcessing/BPUIv2/Sequences/SequenceStorageService.cs`

Methods:
- `LoadAll()`
- `LoadUserSequences()`
- `LoadSharedSequences()`
- `SaveUserSequence(...)`
- `DeleteUserSequence(...)`
- `SaveSharedSequence(...)`
- `DeleteSharedSequence(...)`

File:
- `Acad-C3D-Tools/IntersectUtilities/BatchProcessing/BPUIv2/Sequences/SharedSequenceFileManager.cs`

Methods:
- `Read(...)`
- `Write(...)`
- `Delete(...)`

File:
- `Acad-C3D-Tools/IntersectUtilities/BatchProcessing/BPUIv2/DrawingList/DrawingListService.cs`

Methods:
- `LoadFromFolder(...)`
- `LoadFromTextFile(...)`
- `AddFiles(...)`
- `RemoveFile(...)`
- `Clear()`
- `SetAllIncluded(...)`
- `GetActiveItems()`
- `GetSummary()`

File:
- `Acad-C3D-Tools/IntersectUtilities/BatchProcessing/BPUIv2/Sampling/DrawingSampler.cs`

Method:
- `SampleFromDrawing(string dwgPath)`

Already-implemented operation families:
- `Operations/Alignment`
- `Operations/Block`
- `Operations/DataShortcut`
- `Operations/Detailing`
- `Operations/Layer`
- `Operations/Profile`
- `Operations/Style`
- `Operations/ViewFrame`
- `Operations/Viewport`
- `Operations/Xref`

Interpretation:
- if the next tool is batchable or sequence-based, prefer adding a new `IOperation` instead of another standalone command.

## 6. GeoJSON, shape export, and external data conversion

### 6.1 IntersectUtilities GeoJSON converters

Files:
- `Acad-C3D-Tools/IntersectUtilities/GeoJsonConverters/AutoCadVFToGeoJsonConverter.cs`
- `Acad-C3D-Tools/IntersectUtilities/GeoJsonConverters/AutoCadFjvToGeoJsonConverter.cs`

High-value methods:
- `ViewFrameToGeoJsonLineStringConverter.Convert(...)`
- `ViewFrameToGeoJsonConverterFactory.CreateConverter(...)`
- `PolylineFjvToGeoJsonPolygonConverter.Convert(...)`
- `BlockFjvToGeoJsonConverter.Convert(...)`
- `FjvToGeoJsonConverterFactory.CreateConverter(...)`

Use when:
- the tool exports Civil3D/AutoCAD entities to GeoJSON and should stay consistent with existing schema and FJV handling.

### 6.2 ExportShapeFiles project

Files:
- `Acad-C3D-Tools/ExportShapeFiles/AutoCadFjvToPolygonGeoJsonConverter.cs`
- `Acad-C3D-Tools/ExportShapeFiles/AutoCadFjvToLineStringShapeConverter.cs`
- `Acad-C3D-Tools/ExportShapeFiles/BlockRefWithPsToShapePointConverter.cs`
- `Acad-C3D-Tools/ExportShapeFiles/Utils.cs`

High-value methods:
- polygon/feature conversion `Convert(...)`
- line-string conversion `Convert(...)`
- point conversion `Convert(...)`
- `GetFjvComponents()`

Use when:
- the tool exports SHP-like or GeoJSON-like representations and existing export semantics should be preserved.

## 7. Plugin loading and modular extension

This is the main dynamic loading system.

Files:
- `Acad-C3D-Tools/NSLOAD/PluginManager.cs`
- `Acad-C3D-Tools/NSLOAD/PluginHost.cs`
- `Acad-C3D-Tools/NSLOAD/CommandRegistrar.cs`
- `Acad-C3D-Tools/NSLOAD/NsLoadConfig.cs`
- `Acad-C3D-Tools/NSLOAD/SharedAssembliesConfig.cs`

High-value methods:
- `PluginManager.Register(...)`
- `PluginManager.Load(...)`
- `PluginManager.Unload(...)`
- `PluginManager.UnloadAll()`
- `PluginManager.GetRegisteredPluginNames()`
- `PluginManager.IsRegistered(...)`
- `PluginManager.IsLoaded(...)`
- `PluginManager.Unregister(...)`
- `PluginRegistrationBuilder.WithDllPath(...)`
- `PluginRegistrationBuilder.WithCommands()`
- `PluginRegistrationBuilder.WithSharedAssemblies(...)`
- `PluginRegistrationBuilder.Commit()`
- `PluginHost<TPlugin>.Load(...)`
- `PluginHost<TPlugin>.Unload()`
- `CommandRegistrar.RegisterFromAssembly(...)`
- `CommandRegistrar.UnregisterAll()`
- `NsLoadConfigLoader.GetConfigPath()`
- `NsLoadConfigLoader.Load()`
- `NsLoadConfigLoader.Save(...)`
- `NsLoadConfigLoader.MergeWithCsv(...)`
- `SharedAssembliesConfigLoader.Load(...)`
- `CsvLoader.Load(...)`

Use when:
- the future tool should live as a separately loadable plugin rather than another monolith command in `IntersectUtilities`.

## 8. Additional project-specific surfaces worth checking

### BBR workflows

BBR is a first-class concept in this repo and already has import, export, reporting, and analysis support.

Primary files:
- `Acad-C3D-Tools/Dimensionering/Dimensionering.cs`
- `Acad-C3D-Tools/IntersectUtilities/DataScience/DataScience.cs`
- `Acad-C3D-Tools/IntersectUtilities/Intersect.cs`
- `Acad-C3D-Tools/ExportShapeFiles/ExportShapeFiles.cs`
- `Acad-C3D-Tools/Dimensionering/FeatureCollection.cs`
- `Acad-C3D-Tools/UtilitiesCommonSHARED/PropertySets/PropertySetManager.cs`
- `Acad-C3D-Tools/UtilitiesCommonSHARED/Schema/DF BBR Bygning Class.cs`
- `Acad-C3D-Tools/UtilitiesCommonSHARED/Schema/DF BBR Enhed Class.cs`

Important commands:
- `DIMIMPORTBBRBLOCKS`
- `DIMINTERSECTAREAS`
- `DIMWRITEEXCEL`
- `DIMANALYZEDUPLICATEADDR`
- `DIMENHEDERLIST`
- `DIMENHEDERANALYZE`
- `EXPORTBBRTOSHAPE`
- `BBRFROMPTSDE`
- `BBRFROMPTS`
- `DSCLONEBBRFROMPOINT`

Typical existing BBR capabilities:
- import BBR GeoJSON into drawing blocks with BBR property sets
- assign district/area data to BBR blocks
- export BBR blocks to shapefiles
- generate CSV/Excel/report outputs from BBR data
- analyze duplicate addresses
- analyze BBR building-unit datasets (`BBR_bygning.json`, `BBR_enhed.json`)
- clone or synthesize BBR blocks from points/templates

Check these first if a new tool touches:
- buildings
- addresses
- district assignment
- estimated heat demand
- BBR unit/building analysis

### NTR export

File:
- `Acad-C3D-Tools/NTRExport/Commands.cs`

Known entry points:
- `dxfexport()`
- `ntrexport()`
- `ntrtest()`
- `ntrdot()`
- `ntrcreateps()`
- `ntrmarkverticals()`

Note:
- if the new tool concerns routing, NTR format generation, or topology-to-export conversion, inspect `NTRExport` before writing new logic.

### Sheet creation automation

Files to inspect first:
- `Acad-C3D-Tools/SheetCreationAutomation/01 SheetCreationAutomation.cs`
- `Acad-C3D-Tools/SheetCreationAutomation/Procedures/*`
- `Acad-C3D-Tools/SheetCreationAutomation/Services/*`

Reason:
- existing workflow automation, wait/retry logic, and UI-driving code already exists.

### Longitudinal profiles

Files to inspect first:
- `Acad-C3D-Tools/IntersectUtilities/LongitudinalProfiles/LongitudinalProfileTools.cs`
- `Acad-C3D-Tools/IntersectUtilities/LongitudinalProfiles/AutoProfile/*`
- `Acad-C3D-Tools/IntersectUtilities/LongitudinalProfiles/Detailing/*`

Reason:
- profile creation, profile view creation, detailing, symbols, and AutoProfile all already exist and are command-heavy.

## 9. Fast search recipes

Use these before building new code:

```powershell
rg -n "\[CommandMethod\(" Acad-C3D-Tools -g "*.cs"
```

```powershell
rg -n "public .*\\(" Acad-C3D-Tools\\UtilitiesCommonSHARED Acad-C3D-Tools\\AutoCADCommandsSHARED Acad-C3D-Tools\\PipelineSHARED -g "*.cs"
```

```powershell
rg -n "class .*: OperationBase" Acad-C3D-Tools\\IntersectUtilities\\BatchProcessing\\BPUIv2 -g "*.cs"
```

```powershell
rg -n "CreateConverter|Convert\\(" Acad-C3D-Tools\\IntersectUtilities\\GeoJsonConverters Acad-C3D-Tools\\ExportShapeFiles -g "*.cs"
```

```powershell
rg -n "PropertySet|ReadProperty|WriteProperty" Acad-C3D-Tools\\UtilitiesCommonSHARED -g "*.cs"
```

## 10. Recommended decision order for future tools

Before adding a new tool, check in this order:

1. Existing command entry points in `Intersect.cs`, `LongitudinalProfileTools.cs`, `PlanDetailing.cs`, `Dimensionering.cs`, and `NTRExport/Commands.cs`.
2. Shared helper layers in `AutoCADCommandsSHARED`, `UtilitiesCommonSHARED`, and `PipelineSHARED`.
3. `PropertySetManager` and `DataManager` for metadata/file-lookup needs.
4. `BatchProcessing/BPUIv2` if the workflow can be expressed as a reusable operation.
5. `NSLOAD` if the tool should be independently loadable.

## 11. Current limits of this map

This file is curated, not exhaustive.
- It covers the highest-yield reusable surfaces found during source inspection.
- It does not list every private/internal method in the repo.
- It should be updated when new shared helper classes or operation families are added.
