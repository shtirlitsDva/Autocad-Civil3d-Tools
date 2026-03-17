<section>
    <overview>
        <title>Report System — Implementation Status & Continuation Guide</title>
        <description>
            Comprehensive handoff document for the DimensioneringV2 report generation system.
            Written 2026-03-17 after completing all 5 implementation phases.
            Use this to continue development in a new session.
            Branch: feature/hydraulic-network
        </description>
    </overview>
</section>

<section>
    <title>1. What Was Built</title>
    <overview>
        A QuestPDF-based PDF report generation system with modular section architecture.
        User selects a HydraulicNetwork in CalcManager, clicks "Rapport", fills in project
        metadata, and gets a professional dimensioning report PDF.
    </overview>

    <subsection>
        <title>1.1 Commits (on feature/hydraulic-network)</title>
        <content>
1. `c65b7d0d` — Phases 1-3: Foundation + framework + MVP
2. `97f5cb08` — Phases 4-5: All remaining modules
3. `4d82386a` — UI dialogs + gear button + naming fixes
        </content>
    </subsection>

    <subsection>
        <title>1.2 User Flow</title>
        <content>
1. Open CalcManager window (existing command)
2. Select a calculated HydraulicNetwork from the DataGrid
3. Click "Rapport" button → HnReportSettingsDialog opens (project metadata)
4. Fill in project name, number, author, design pressure, etc. → OK
5. Save dialog appears → choose PDF location
6. PDF generated with all enabled modules → opens in viewer
7. Gear button (⚙) next to Rapport → opens ReportSettingsWindow for profile management
        </content>
    </subsection>
</section>

<section>
    <title>2. Architecture</title>

    <subsection>
        <title>2.1 Pipeline</title>
        <content>
```
CalcManagerViewModel.GenerateReport()
  → ReportProfileService.Instance.LoadFromActiveDocument()
  → HnReportSettingsDialog (modal, metadata entry)
  → ReportOrchestrator.Generate(hn, profile)
    → ReportDataExtractor.Extract(hn, profile) → ReportDataContext
    → foreach enabled module in profile:
        module.Compose(container, context)
    → Document.GeneratePdf(filePath)
```
        </content>
    </subsection>

    <subsection>
        <title>2.2 Key Abstractions</title>
        <content>
- **IReportModule** — interface: Id, DisplayName, IsImplemented, Compose(container, context)
- **ReportDataContext** — read-only snapshot with pre-extracted DTOs (segments, nodes, consumers, summary, compliance, supply points)
- **ReportDataExtractor** — static, extracts all data from HydraulicNetwork once before rendering
- **ReportOrchestrator** — entry point, builds QuestPDF Document from enabled modules
- **ReportModuleRegistry** — static list of all 11 module instances
- **ReportProfileService** — singleton, CRUD for profiles, persists via SettingsSerializer to FlexDataStore
- **NodeNumberingService** — assigns stable 1-based NodeIds using path-based ordering
        </content>
    </subsection>

    <subsection>
        <title>2.3 Data Models</title>
        <content>
Persistence models (in Models/Report/):
- **ReportModuleId** — enum of 11 module IDs
- **ReportModuleEntry** — ModuleId + IsEnabled + SortOrder
- **ReportProfile** — name + List of entries + general settings (norm text, nyttetimer display)
- **ReportProfileStore** — persisted to FlexDataStore (SelectedProfileName + List of profiles)
- **ReportHnSettings** — per-HN metadata (project name, author, design pressure, version history)

Extraction DTOs (in Services/Report/DataModels/):
- SegmentRow, NodeRow, ConsumerRow, SystemSummary, ComplianceRow, SupplyPointRow
        </content>
    </subsection>

    <subsection>
        <title>2.4 Serialization</title>
        <content>
- **ReportHnSettings** serialized via MessagePack DTO: ReportHnSettingsMsgDto [Key(9)] on HydraulicNetworkMsgDto
- **NodeId** serialized via NodeJunctionMsgDto [Key(7)]
- **ReportProfileStore** serialized via SettingsSerializer (JSON in FlexDataStore)
- VersionHistoryEntry has its own MessagePack DTO: VersionHistoryEntryMsgDto
        </content>
    </subsection>
</section>

<section>
    <title>3. Module Status</title>
    <content>
| # | Module ID | Section | File | Status |
|---|-----------|---------|------|--------|
| 1 | CoverPage | §1-§2 Forside + Versionshistorik | CoverPageModule.cs | DONE |
| 2 | Summary | §3 Sammenfatning | SummaryModule.cs | DONE |
| 3 | ProjectBasis | §4 Projektgrundlag | ProjectBasisModule.cs | DONE |
| 4 | CalcPrerequisites | §5 Beregningsforudsætninger | CalcPrerequisitesModule.cs | DONE |
| 5 | SupplyPoints | §6 Forsyningspunkter | SupplyPointsModule.cs | DONE |
| 6 | SystemResults | §7.1-7.2 Systemresultater | SystemResultsModule.cs | DONE |
| 7 | Sensitivity | §7.3 Følsomhedsanalyse | SensitivityModule.cs | STUB (IsImplemented=false) |
| 8 | SegmentResults | §7.4 Strækningsresultater | SegmentResultsModule.cs | DONE (landscape) |
| 9 | NodeResults | §7.5 Knudepunkter | NodeResultsModule.cs | DONE |
| 10 | ConsumerOverview | §8 Forbrugeroversigt | ConsumerOverviewModule.cs | DONE (3 sub-sections, landscape) |
| 11 | OverviewMap | §9 Oversigtskort | OverviewMapModule.cs | STUB (IsImplemented=false) |
    </content>
</section>

<section>
    <title>4. File Tree</title>
    <content>
All paths relative to Acad-C3D-Tools/DimensioneringV2/:

```
Models/Report/
  ReportModuleId.cs              — enum of 11 module IDs
  ReportModuleEntry.cs           — ModuleId + IsEnabled + SortOrder
  ReportProfile.cs               — profile with module list + general settings
  ReportProfileStore.cs          — ObservableObject container for FlexDataStore
  ReportHnSettings.cs            — per-HN metadata + VersionHistoryEntry

Serialization/Binary/
  ReportHnSettingsMsgDto.cs      — MessagePack DTO for ReportHnSettings
  (NodeJunctionMsgDto.cs)        — modified: added Key(7) NodeId
  (HydraulicNetworkMsgDto.cs)    — modified: added Key(9) ReportSettings

Services/Report/
  IReportModule.cs               — module interface
  ReportDataContext.cs            — read-only data snapshot
  ReportDataExtractor.cs         — extracts DTOs from HydraulicNetwork
  ReportModuleRegistry.cs        — static list of all module instances
  ReportOrchestrator.cs          — main entry point (extract → compose → save PDF)
  ReportProfileService.cs        — singleton, CRUD, FlexDataStore persistence
  NodeNumberingService.cs        — path-based node ID assignment
  Styles/
    ReportStyles.cs              — shared QuestPDF constants (fonts, colors, margins)
  DataModels/
    SegmentRow.cs                — pipe segment report row
    NodeRow.cs                   — node report row
    ConsumerRow.cs               — consumer/building report row
    SystemSummary.cs             — aggregated totals
    ComplianceRow.cs             — compliance check row
    SupplyPointRow.cs            — supply point row
  Modules/
    CoverPageModule.cs           — §1-§2
    SummaryModule.cs             — §3 (3-col: Label | Value | Unit)
    ProjectBasisModule.cs        — §4
    CalcPrerequisitesModule.cs   — §5
    SupplyPointsModule.cs        — §6
    SystemResultsModule.cs       — §7.1-7.2
    SensitivityModule.cs         — §7.3 (stub)
    SegmentResultsModule.cs      — §7.4 (landscape)
    NodeResultsModule.cs         — §7.5
    ConsumerOverviewModule.cs    — §8 (3 sub-sections, landscape detail table)
    OverviewMapModule.cs         — §9 (stub)

UI/ReportSettings/
  HnReportSettingsDialog.xaml    — per-HN metadata entry (project name, author, etc.)
  HnReportSettingsDialog.xaml.cs — code-behind, populates/reads ReportHnSettings
  ReportSettingsWindow.xaml      — profile management (tabs: modules, norm text, display)
  ReportSettingsWindow.xaml.cs   — code-behind
  ReportSettingsViewModel.cs     — MVVM for profile management + ModuleToggleItem

UI/CalcManager/
  (CalcManagerWindow.xaml)       — modified: added Rapport + gear buttons
  (CalcManagerViewModel.cs)      — modified: added GenerateReportCommand + OpenReportSettingsCommand

GraphFeatures/
  (NodeJunction.cs)              — modified: added NodeId property

Models/
  (HydraulicNetwork.cs)          — modified: added ReportSettings, calls NodeNumberingService

Commands.cs                      — modified: QuestPDF.Settings.License + ReportProfileService.Reset()
DimensioneringV2.csproj          — modified: added QuestPDF 2024.12.2
```
    </content>
</section>

<section>
    <title>5. Known Issues & TODOs</title>

    <subsection>
        <title>5.1 Data Extraction TODOs</title>
        <content>
- **PressureGradientUtilization** in SegmentRow is hardcoded to 0 — needs computation from accept criteria (MaxPressureGradient per DN)
- **SupplyPoint DifferentialPressure** is hardcoded to 0 — needs calculation
- **SupplyPoint Kote** is always null — needs GDAL elevation lookup (GDALClient exists in Services/)
- **DesignPressureBar** is user-input only — could be auto-computed from critical path pressure + MinDP + HoldetrykMVS
- **Compliance checks** only include MinDifferentialPressure — should add velocity and pressure gradient checks
        </content>
    </subsection>

    <subsection>
        <title>5.2 UI TODOs</title>
        <content>
- **HnReportSettingsDialog** shown before EVERY report generation — consider showing only on first generation or adding a "skip" option
- **ReportSettingsWindow** profile Import/Export (JSON file) not yet wired — buttons exist in ViewModel but no file dialog
- **ReportSettingsWindow** Rename profile not implemented — only New/Duplicate/Delete
- **Module reorder** via up/down buttons works in ViewModel but selection UX needs polish (ListBox selection vs RadioButton hidden)
        </content>
    </subsection>

    <subsection>
        <title>5.3 Report Content TODOs</title>
        <content>
- **Table of Contents** not generated — could add after cover page
- **Page headers** not present — could add project name + section title
- **Consistent page footers** only on Summary page — should be on all pages
- **Sensitivity module** (§7.3) is a stub — large feature requiring re-running calculations
- **Overview map module** (§9) is a stub — needs Mapsui-to-image rendering with color legend for pipe dimensions
- **Nyttetimer display** "show all vs show only present codes" setting exists in profile but CalcPrerequisitesModule doesn't read it yet
        </content>
    </subsection>

    <subsection>
        <title>5.4 Naming Conventions Applied</title>
        <content>
Per user feedback in this session:
- "bygninger" (not "forbrugere") for building count
- "enheder" (not "boliger") for unit count — maps to NumberOfUnitsConnected
- Values right-aligned with units in separate left-aligned column
        </content>
    </subsection>
</section>

<section>
    <title>6. QuestPDF API Notes</title>
    <overview>
        Important patterns learned during implementation — refer to these when
        modifying or adding new modules.
    </overview>
    <content>
1. **Table headers**: `table.Header(header => { })` — the `header` parameter is NOT `TableDescriptor`. Do NOT create typed helper methods. Inline all header cell rendering directly.

2. **Text chaining**: `.Text(Action)` returns void. Cannot chain `.FontSize()` after it. For page footers, use `t.DefaultTextStyle(x => x.FontSize(...))` inside the text callback.

3. **Right alignment**: `.AlignRight()` must come BEFORE `.Text()` in the container chain.

4. **3-column key-value tables**: Pattern used in Summary and SystemResults:
   - Column 1: Label (RelativeColumn(4))
   - Column 2: Value right-aligned (RelativeColumn(2))
   - Column 3: Unit left-aligned (ConstantColumn(50))

5. **Landscape pages**: Use `page.Size(PageSizes.A4.Landscape())` — used in SegmentResults and ConsumerOverview detail table.

6. **QuestPDF license**: Set once in Commands.Initialize(): `QuestPDF.Settings.License = LicenseType.Community;`

7. **SkiaSharp**: QuestPDF 2024.12.2 and Mapsui 4.1.9 share compatible SkiaSharp versions — no conflicts.
    </content>
</section>

<section>
    <title>7. Node Numbering Algorithm</title>
    <content>
Implemented in `Services/Report/NodeNumberingService.cs`.
Called from `HydraulicNetwork.FinalizeCalculation()`.

Algorithm:
1. Filter out edges where `NumberOfBuildingsSupplied == 0` (discarded by genetic algorithm)
2. Build adjacency map from active edges only
3. Find root node (IsRootNode == true)
4. DFS from root: enumerate all root→leaf paths, tracking physical length (sum of edge.PipeSegment.Length)
5. Sort paths by total length (longest first)
6. Assign 1-based sequential NodeIds along longest path first
7. On subsequent paths, skip already-numbered nodes

NodeIds are persisted via NodeJunctionMsgDto [Key(7)] and stable across report generations.
    </content>
</section>

<section>
    <title>8. Design Session Reference</title>
    <content>
Original architecture decisions are in:
`C:\1\OneDrive - Norsyn\76 Dim2 dev\report-system-design-session.md`

Report template structure is in:
`C:\1\OneDrive - Norsyn\76 Dim2 dev\DIMv2 Report draft.docx`

Example data file:
`C:\1\OneDrive - Norsyn\76 Dim2 dev\Minimal.d2r`
    </content>
</section>

<section>
    <title>9. How to Continue</title>
    <content>
Priority order for next work:
1. Fix PressureGradientUtilization computation (read accept criteria from pipe config)
2. Add compliance checks for velocity and pressure gradient
3. Add page footers to all modules (not just Summary)
4. Wire "show all nyttetimer codes" setting into CalcPrerequisitesModule
5. Implement profile Import/Export (JSON file dialog)
6. Auto-compute DesignPressureBar from critical path data
7. Add Table of Contents module
8. Implement Overview Map module (Mapsui → image export)
9. Implement Sensitivity module (requires re-running calculations)

Build command: `dotnet build Acad-C3D-Tools/DimensioneringV2/DimensioneringV2.csproj -p:WarningLevel=0`
    </content>
</section>
