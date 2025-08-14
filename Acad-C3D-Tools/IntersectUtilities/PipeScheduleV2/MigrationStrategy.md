## PipeScheduleV2 migration: DataTable → POCO + LINQ (with tests)

### Goals
- Replace DataTable.Select usage with strongly-typed POCOs and LINQ.
- Keep the public API of `PipeScheduleV2` and `IPipeType`/`IPipeRadiusData` stable.
- Validate behavior via unit tests before/after the refactor.

### Scope and constraints
- Use the original CSV inputs (not fixtures):
  - Schedule: `X:\AutoCAD DRI - 01 Civil 3D\PipeSchedule\Schedule\`
  - Radier: `X:\AutoCAD DRI - 01 Civil 3D\PipeSchedule\Radier\`
- All enums are defined in `UtilitiesCommonSHARED/Enums.cs` (e.g., `PipeTypeEnum`, `PipeSeriesEnum`). The `PipeType` and `PipeSeries` columns in the CSVs are enums and must be parsed as such.
- Preserve existing numeric/equality tolerances and defaults.

### Example .csv data
#### Schedule
DN;PipeType;PipeSeries;pOd;pThk;kOd;tWdth;minElasticRadii;VertFactor;color;DefaultL;OffsetUnder7_5
20;Enkelt;S1;26.9;2.6;90;780;12.9;1;253;12;0.7
25;Enkelt;S1;33.7;2.6;90;780;16.9;1;180;12;0.7
32;Enkelt;S1;42.4;2.6;110;820;20.9;1;70;12;0.7
40;Enkelt;S1;48.3;2.6;110;820;23.9;1;240;12;0.7

#### Radii
DN;PipeType;PipeLength;BRpmin;ERpmin
20;Enkelt;12;8.4;13
25;Enkelt;12;12.8;17
32;Enkelt;12;13.4;21

### Current state (summary)
- CSVs are loaded into `DataTable`s via `ReadCsvToDataTable`.
- Each `PipeType*` class derives from `PipeTypeBase` which stores a `DataTable _data` and answers queries using `DataTable.Select(...)` and row casting.
- Radii data also uses `DataTable` with `.Select`.

### Target state
- Introduce POCO records for Schedule and Radier rows.
- Load CSVs into `List<TRecord>` using CsvHelper.
- Swap `PipeTypeBase`/`PipeRadiusData` internals to LINQ over lists (with optional prebuilt indexes for performance).
- Keep all public methods and behaviors the same.

### Data model
```csharp
// Enums from IntersectUtilities.UtilsCommon.Utils
// using static IntersectUtilities.UtilsCommon.Utils;

public sealed class PipeTypeRecord {
    public int DN { get; set; }
    public PipeTypeEnum PipeType { get; set; }            // CSV: PipeType (enum)
    public PipeSeriesEnum PipeSeries { get; set; }        // CSV: PipeSeries (enum)
    public double pOd { get; set; }
    public double pThk { get; set; }
    public double kOd { get; set; }
    public double tWdth { get; set; }
    public double minElasticRadii { get; set; }
    public double VertFactor { get; set; }
    public short color { get; set; }
    public double DefaultL { get; set; }
    public double OffsetUnder7_5 { get; set; }
}

public sealed class PipeRadiusRecord {
    public int DN { get; set; }
    public PipeTypeEnum PipeType { get; set; }   // CSV includes this column in Radier
    public int PipeLength { get; set; }
    public double BRpmin { get; set; }
    public double ERpmin { get; set; }
}
```

### CSV loading (CsvHelper)
- Use `CsvHelper` with settings:
  - `Delimiter = ";"`
  - `Culture = CultureInfo.InvariantCulture` (decimal point ".")
  - Case-insensitive header matching
  - Enum converters for `PipeTypeEnum` and `PipeSeriesEnum` (case-insensitive)

```csharp
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

sealed class PipeTypeRecordMap : ClassMap<PipeTypeRecord> {
    public PipeTypeRecordMap() {
        Map(m => m.DN).Name("DN");
        Map(m => m.PipeType).Name("PipeType");              // enum
        Map(m => m.PipeSeries).Name("PipeSeries");          // enum
        Map(m => m.pOd).Name("pOd");
        Map(m => m.pThk).Name("pThk");
        Map(m => m.kOd).Name("kOd");
        Map(m => m.tWdth).Name("tWdth");
        Map(m => m.minElasticRadii).Name("minElasticRadii");
        Map(m => m.VertFactor).Name("VertFactor");
        Map(m => m.color).Name("color");
        Map(m => m.DefaultL).Name("DefaultL");
        Map(m => m.OffsetUnder7_5).Name("OffsetUnder7_5");
    }
}

sealed class PipeRadiusRecordMap : ClassMap<PipeRadiusRecord> {
    public PipeRadiusRecordMap() {
        Map(m => m.DN).Name("DN");
        Map(m => m.PipeType).Name("PipeType");              // enum
        Map(m => m.PipeLength).Name("PipeLength");
        Map(m => m.BRpmin).Name("BRpmin");
        Map(m => m.ERpmin).Name("ERpmin");
    }
}

static List<T> LoadRecords<T, TMap>(string path) where TMap : ClassMap<T>, new() {
    var cfg = new CsvConfiguration(CultureInfo.InvariantCulture) {
        Delimiter = ";",
        PrepareHeaderForMatch = args => args.Header.Trim(),
        MissingFieldFound = null,
        HeaderValidated = null,
        IgnoreBlankLines = true
    };
    using var reader = new StreamReader(path);
    using var csv = new CsvReader(reader, cfg);
    csv.Context.TypeConverterCache.AddConverter(typeof(PipeTypeEnum), new CsvHelper.TypeConversion.EnumConverter(typeof(PipeTypeEnum)));
    csv.Context.TypeConverterCache.AddConverter(typeof(PipeSeriesEnum), new CsvHelper.TypeConversion.EnumConverter(typeof(PipeSeriesEnum)));
    csv.Context.RegisterClassMap<TMap>();
    return csv.GetRecords<T>().ToList();
}
```

### Internal repositories
- Replace `PipeTypeBase._data: DataTable` with `_rows: List<PipeTypeRecord>` and prebuilt indexes:
  - `_byDn: ILookup<int, PipeTypeRecord>`
  - `_byDnType: ILookup<(int dn, PipeTypeEnum type), PipeTypeRecord>`
  - `_byDnTypeSeries: Dictionary<(int dn, PipeTypeEnum type, PipeSeriesEnum series), PipeTypeRecord>`
- Replace `PipeRadiusData._data` with `_rows: List<PipeRadiusRecord>` and an index by `(DN, PipeLength)`.

#### Lazy loading (on-demand, per type/company)
- Repositories MUST lazy-load CSV data so we only read what is needed on first access:
  - For schedule data: hold a `Dictionary<string, Lazy<IPipeType>>` keyed by CSV filename/type key (e.g., `"DN"`, `"ALUPEX"`, ...). The `Lazy` factory loads a single CSV from `Schedule` for that key, builds rows and indexes, then caches the ready `IPipeType`.
  - For radii data: hold a `Dictionary<string, Lazy<IPipeRadiusData>>` keyed by company (e.g., `"Logstor"`, `"Isoplus"`). Load the single CSV from `Radier` lazily.
- Public calls like `_repository.GetPipeType(key)` and `_radiiRepo.GetPipeRadiusData(company)` should trigger `Lazy.Value` evaluation.
- Add optional cache-busting hooks if needed later (not required for migration).

### Public API kept stable
- Do not change signatures of `PipeScheduleV2` public methods or the interfaces `IPipeType`, `IPipeTypeRepository`, `IPipeRadiusData`, `IPipeRadiusDataRepository`.
- Add temporary overloads for `Initialize(IEnumerable<PipeTypeRecord> rows, PipeSystemEnum system)` and `Initialize(IEnumerable<PipeRadiusRecord> rows, CompanyEnum comp)`; remove DataTable-based overloads after migration.

### Unit testing plan (pre-refactor)
- Use real CSVs from the specified folders to initialize repositories in tests.
- Discover test targets via reflection to avoid omissions:
  - Enumerate all public static methods on `PipeScheduleV2` and assert they have at least one test invocation (method name whitelist/blacklist where appropriate).
  - Enumerate all public methods on `IPipeType` and each concrete `PipeType*` implementation; generate parameterized tests using values from CSVs (DNs, series, types) where feasible.
  - Enumerate public methods on `IPipeRadiusData` implementations and cover via CSV samples.
- Cover the following behaviors with representative values from the CSVs:
  - Pipe type computations:
    - `GetPipeOd`, `GetPipeId` (Id here is Inner Diameter, not Identity)
    - `GetPipeKOd(dn,type,series)`; include special-case overrides (`CU` S3→S2, single-series types)
    - `GetPipeSeriesV2` (ConstantWidth to kOd mapping, tolerance via `Equalz`)
    - `GetMinElasticRadius`, `GetFactorForVerticalElasticBending`
    - `GetPipeStdLength`, `GetDefaultLengthForDnAndType`
    - `GetLabel` across `PipeTypeDN`, `ALUPEX`, `CU`, `PRTFLEXL`, `AQTHRM11`, `PE`
    - `ListAllDnsForPipeType(Serie)`, `GetAvailableTypes`, `GetAvailableSeriesForType`
    - `GetPipeTypeByAvailability`
  - Radii:
    - `GetBuerorMinRadius` for both companies and multiple `PipeLength`
  - Facade helpers in `PipeScheduleV2`:
    - `GetPipeSystem`, `GetPipeType`, `GetPipeDN` parsing from layer strings
    - `GetTrenchWidth`, `GetOffset`, `GetLayerColor`, `GetSizeColor`
  - Edge cases:
    - Missing rows → zero/defaults, undefined series, invalid layer names

Note: Because the tests use original CSVs, they will reflect current production data. This matches the requested constraint and ensures high-fidelity verification.

### In-host test harness (no IntersectUtilities.dll load)
- Create a dedicated class library project: `PipeScheduleV2UnitTests` (TargetFramework `net8.0-windows10.0.26100.0`).
- Compile the code-under-test directly into the test dll:
  - Link `..\\IntersectUtilities\\PipeScheduleV2\\PipeScheduleV2.cs` into the test project (`<Compile Include="..\\IntersectUtilities\\PipeScheduleV2\\PipeScheduleV2.cs" Link="PipeScheduleV2\\PipeScheduleV2.cs" />`).
  - Import the shared project `..\\UtilitiesCommonSHARED\\UtilitiesCommonSHARED.projitems` (for enums/utilities).
  - Add Autodesk references required by `PipeScheduleV2.cs` (copy the same `<Reference>` entries and HintPaths used in `IntersectUtilities.csproj`).
- Do NOT reference `IntersectUtilities.csproj`. The goal is to netload only `PipeScheduleV2UnitTests.dll`.
- Inside `PipeScheduleV2UnitTests` implement a tiny test runner:
  - Define a custom attribute `[Ps2Test]` to mark test methods.
  - Add an AutoCAD command `[CommandMethod("RUNPS2TESTS")]` that discovers methods with `[Ps2Test]` via reflection (in the current assembly) and invokes them, reporting pass/fail to the AutoCAD editor.
  - Tests call `PipeScheduleV2` public APIs and use the real CSV directories.

Run tests via AcCoreConsole using a script, e.g. `run_tests.scr`:
```
(netload "X:\\path\\to\\PipeScheduleV2UnitTests.dll")
RUN_PS2_TESTS
QUIT Y
```

Then execute:
```
"C:\\Program Files\\Autodesk\\AutoCAD 2025\\AcCoreConsole.exe" /product C3D /language en-US /s "X:\\path\\run_tests.scr"
```

### Test DWG with sample entities (for Entity-accepting APIs)
- Some `PipeScheduleV2` APIs accept `Entity ent`. To test them, use a dedicated DWG with known entities and stable Handles.
- Create a drawing (e.g., `X:\\AutoCAD DRI - 01 Civil 3D\\PipeSchedule\\Tests\\PS2_TestData.dwg`) containing:
  - Polylines on layers representing different systems/types (so `GetPipeSystem`, `GetPipeType`, `GetPipeDN` work).
  - Polylines with `ConstantWidth` set to match `kOd/1000` where series detection is required (`GetPipeSeriesV2`).
  - Optional extra entities for label and color tests.
- For each entity-based method under test, document the expected entity type and record a stable Handle to use in tests.
  - Add an attribute on the test method to document this, e.g. `[Ps2EntityHandle("2A3B", typeof(Polyline))]`.
  - The test runner resolves the entity by Handle from the current drawing (opened by CoreConsole via `/i`).

Script adjustment to open the test DWG:
```
"C:\\Program Files\\Autodesk\\AutoCAD 2025\\AcCoreConsole.exe" \
  /product C3D /language en-US \
  /i "X:\\AutoCAD DRI - 01 Civil 3D\\PipeSchedule\\Tests\\PS2_TestData.dwg" \
  /s "X:\\path\\run_tests.scr"
```

Runner responsibilities:
- On start, assert an active document is present (DWG opened by CoreConsole).
- Resolve entities by Handle before invoking tests that declare `[Ps2EntityHandle]` (pass entity to helper methods inside the test).
- If a Handle is missing, report a failed test with a clear message.

### Reporting (HTML + minimal console)
- Each run must emit a timestamped HTML report (e.g., `PS2_TestReport_yyyyMMdd_HHmmss.html`) to a reports directory such as:
  `X:\\AutoCAD DRI - 01 Civil 3D\\PipeSchedule\\Tests\\Reports\\`
- Content requirements:
  - Header summary: start time, duration, totals (tests, passed, failed, errors, skipped)
  - Environment: AutoCAD version, .NET version, machine name, CSV folder paths, DWG path
  - Detailed table: Test Name, Status (Pass/Fail/Error/Skip), Duration, Message, StackTrace (collapsible), optional entity Handle used
  - Footer with overall result color (green/red) and file generation time
- Minimal console (editor) output:
  - Print in console a short progress line every N tests (e.g., `Processed 25/140...`)
  - Print in console only failures/errors inline as they occur (single-line: name + message)
  - Final one-line summary: `Tests: X, Passed: Y, Failed: Z, Errors: E, Skipped: S. Report: <path>`
- Implementation sketch:
  - Capture per-test start/stop times, exceptions (`ex.GetBaseException()`), and stack traces
  - Accumulate results in a list; at the end, build an HTML string (basic CSS) and write to file using `File.WriteAllText`
  - Ensure the reports directory exists; if not, create it
  - Write also a raw log file alongside the HTML (same basename with `.log`) containing console output
  - For output to console use method prdDbg(object obj). It will call .ToString() on the object and end with a new line.

### Migration steps
0. Git workflow:
   - Create unit test branch `UnitTestForPS2` and create unit tests in it. Open a PR against `master` when ready.
   - Create a feature branch `PS2Migration` for the refactor work. Open a PR against `master` when ready.
1. Add POCOs and CsvHelper maps/loaders (no behavior change yet).
2. Extend `IPipeType`/`PipeTypeBase` and `IPipeRadiusData`/`PipeRadiusData` with new `Initialize(IEnumerable<...>)` overloads that build `_rows` and indexes.
3. Adjust repositories (`PipeTypeRepository`, `PipeRadiusDataRepository`) to use LAZY loading per type/company and call the new overloads when loading from CSV paths.
4. Method-by-method port inside `PipeTypeBase` and derived types:
   - Replace each `DataTable.Select` with LINQ or dictionary lookups on `_rows`.
   - Keep special-casing intact (e.g., `CU` S3→S2; single-series types ignore series).
   - Preserve tolerance comparisons (`Equalz`) for matching kOd.
5. Port radii computations to use `List<PipeRadiusRecord>`.
6. Run the full test suite; fix any discrepancies.
7. Remove `System.Data` usage, `columnTypeDict`/`radiiColumnTypeDict`, and `ReadCsvToDataTable` once tests are green.

### File layout for tests (partial classes and helpers)
- Split the test harness into focused files for maintainability:
  - `PipeScheduleV2TestsClass.Core.cs`: partial class with init, `[CommandMethod("RUNPS2TESTS")]`, reflection runner, and summary.
  - `PipeScheduleV2Report.cs`: HTML and raw log report writer utilities.
  - `PipeScheduleV2EntityRegistry.cs`: CSV registry utilities and polyline creation in the test DWG.
  - `PipeScheduleV2TestsClass.Tests.cs`: partial class containing all `[Ps2Test]` methods.
  - Optionally standalone attribute/result types if you don’t want them in the main partial: `Ps2TestAttribute.cs`, `Ps2Result.cs`, `Ps2Status.cs`, `Ps2SkipException.cs`.
- Update the test project `.csproj` to include the new files; keep `PipeScheduleV2.cs` linked and `UtilitiesCommonSHARED` imported.

### Performance
- Building composite-key dictionaries provides O(1) lookup for hot paths:
  - `(DN, PipeTypeEnum, PipeSeriesEnum) → PipeTypeRecord`
  - `(DN) → IEnumerable<PipeTypeRecord>`
  - `(DN, PipeLength) → PipeRadiusRecord`
- Expected to be as fast or faster than `DataTable.Select`.

### Verification and rollout
- Baseline: run tests on current DataTable implementation (commit A).
- Migrate and run tests on POCO implementation (commit B). All tests must pass.
- Compare a small set of manual queries against real DWGs if desired (sanity checks).

### Rollback
- If any blocking issues arise, revert to commit A (DataTable version). The test suite ensures a quick diagnose/return.


