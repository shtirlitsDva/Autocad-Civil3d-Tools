# DimensioneringV2 pipe type priority settings
_Exported on 1/14/2026 at 12:53:00 GMT+1 from Cursor (2.3.35)_

---

**User**

our task is to implement a rather complex feature for DimensioneringV2 project, starting with the @Acad-C3D-Tools/DimensioneringV2/UI/SettingsTab.xaml . The application allows a user to design a pipe network by calculating flows and hydraulic properties and finding appropriate pipe sizes. The new feature is going to enable a user to have granular accept criteria for each pipe size. I have not decided on the full feature scope and details, so we have to work together to flesh out all the details and write a spec document and plan implementation.
Currently, if you look at @Acad-C3D-Tools/DimensioneringV2/UI/SettingsTab.xaml you will notice rows in the Fordelings- and Stikledninger part that start with Acceptkriterie hastighed and trykgradient. We will be implementing a system that is more general and generic.
Currently the application assumes that the steel pipes are ALWAYS present and one can choose to use PertFlextra for smaller sizes.
We have to rework the system in this overall vision:
1. we will still work with settings for fordelings- and stikledninger separately.
2. for each category a new row will be present (instead of all 'Acceptkriterie ...' rows), named 'Rørindstillinger'. This row will have a button in the payload section that says 'Rediger'.
3. When pushing this button, a new window will open that allows the user to edit the pipe settings.
4. The pipe settings are envisioned as a prioritised list.
5. There are controls for adding, deleting and moving the entries up and down, the usual stuff
6. The user then adds a PipeCriteria? I don't know what to call this class.
7. PipeCriterias are taken from the @Acad-C3D-Tools/NorsynHydraulicShared/Pipes/PipeTypes.cs class, ie. we need to support all pipes defined there.
8. So a user adds a PipeCriteria and it is auto assigned first priority -> ie. list is empty so it gets the first.
9. The list is shown in a kind of tabular form, because beside the name of the PipeCriteria (let's call it pc onwards) there should appear two dropdowns 'Min' 'Maks' for selecting minimum and maximum dn sizes participating in the criteria and these are taken from pipetype dynamically. Then only for Stikledninger appears a button 'Regler' and then a button further to the right for both categories named 'Accepkriterier'.
10. in the min maks dropdowns user selects the nominal diamters that participate in the design.
11. 'Regler' will we process in the next step, so just a stub for now.
12. 'Acceptkriterier' opens a new window dedicated to the pipetype criteria that called it where user can define max medium velocity and max pressure gradient for each pipe size in this pipe type. There are some default values you can read in current @Acad-C3D-Tools/DimensioneringV2/NorsynHydraulic/HydraulicSettings.cs class. So the criteria window should be filtered for enabled pipe sizes and for disabled pipesizes the values should default to 0, and we need some kind of mechanism to prevent the program from using uninitialized kriterias for pipesizes, for example if user using the drop down includes a pipe size that was not participating earlier, but forgets to edit acceptcriteria, the program should not execute. I assume we will be calling this PipeCriteria mechanism during calculations, so if it detect any uninitialized pipesizes, it should display a warning message and stop execution.
13. and user then can add another PipeCriteria and do the same things as for the first one. For example user will add PertFlextra as first priority, assign criteria, then add Steel pipetype as second priority and assign criteria. ANd when calculating the program will first try to use the first priority pipes and will from small to large and then switch to second pipecriteria when first has exhausted its' capacity for all pipe sizes.
14. the concrete interaction between the calculatin core and these pipecriteria we will work out in a future step.
15. we will call the class PipeTypePriority, not PipeCriteria. Criteria word will be used for the 'Acceptkriterier' window.

---

**Cursor**

I need to understand the existing architecture better before we can flesh out the specification. Let me explore the relevant files to understand how the current system works.

Let me look at how pipe sizes are selected during calculations to understand the current flow better.

I now have a good understanding of the current architecture. The system uses:
- `HydraulicCalc` which initializes max flow tables for FL and SL based on the accept criteria
- Accept criteria (velocity/pressure gradient) are currently grouped by DN ranges
- `MaxFlowCalcWater` builds pipe dimension tables with max flows calculated from these criteria
- Pipe selection happens in `determineDim()` by finding the first pipe in the table that can handle the flow

Before creating a detailed specification, I have some clarifying questions:

Thank you for those answers. I have a few more questions to finalize the specification:

Excellent! Now I have all the information needed to create a comprehensive specification and implementation plan. Let me create the plan:

---

**User**


Remarks:
2.2: No backwards compatibility -> we rewrite this part of the program focused, no old left over code, no compatibility issues, no fallbacks -> main procedure must work.

General wpf remarks: use mvvm model with communitykit. Use as much as possible shared styles in Theme.xaml (look @Acad-C3D-Tools/DimensioneringV2/UI/GASettingsTab.xaml to determine how shared styles are handled). We aim to keep style managing centrally managed, I don't want to have individual styles for each window.

4.1: Use the generic @Acad-C3D-Tools/DimensioneringV2/Services/SettingsSerializer.cs for storage in the dwg database.

4.2: When loading an existing databse that don't have a saved priority configuration, a default one should be generated.
Oh wait, you are totally right. what about older files??? hmmm... we should add versioning to the settings class.. I don't know how this is handled in the industry. I mean okay, your proposed way works for this round of refactoring, but what about next ones? we need to have a robust way to handle legacy settings not only we have now, but a robust system for future version conflicts. Help me deciding here.

5. Can we please wait with the calculation integration? it is too much for me to process in one step. We should postpone it untill we have the priority stuff set up. Until then write stubs, don't mind compile errors. we don't need to one shot this -> we will handle this incrementally.

Okay, update the plan and save it for tomorrow, as I need to go now.

---

**Cursor**

You raise an excellent point about versioning. Let me help you decide on a robust approach for handling settings versioning that will work not just for this refactoring but for future ones as well.

Now I have all the information needed to update the plan with your feedback. Let me read the current plan and update it:

```markdown
---
name: PipeTypePriority Feature
overview: Implement a flexible, prioritized pipe settings system for DimensioneringV2 that replaces hardcoded accept criteria with per-DN granular velocity and pressure gradient settings for multiple pipe types in FL and SL categories.
todos:
  - id: phase1-versioning
    content: Implement settings versioning infrastructure with migration chain pattern
    status: pending
  - id: phase1-models
    content: Create data model classes (DnAcceptCriteria, PipeTypePriority, PipeTypeConfiguration)
    status: pending
  - id: phase1-settings
    content: Rewrite HydraulicSettings - remove old accept criteria, add new config properties
    status: pending
  - id: phase1-defaults
    content: Create default configuration factory for new files and legacy files without config
    status: pending
  - id: phase2-theme-styles
    content: Add required shared styles to Theme.xaml for new windows
    status: pending
  - id: phase2-pipe-window
    content: Create PipeSettingsWindow.xaml and PipeSettingsViewModel (MVVM with CommunityToolkit)
    status: pending
  - id: phase2-criteria-window
    content: Create AcceptCriteriaWindow.xaml and AcceptCriteriaViewModel
    status: pending
  - id: phase2-settings-tab
    content: Update SettingsTab.xaml - replace accept criteria rows with Rorindstillinger
    status: pending
  - id: phase2-viewmodel
    content: Update SettingsTabViewModel with commands for new windows
    status: pending
  - id: phase3-validation
    content: Create PipeConfigValidationService with validation logic
    status: pending
  - id: phase3-calc-stubs
    content: Add stub methods in HydraulicCalc/MaxFlowCalc for future calculation integration
    status: pending
---

# PipeTypePriority Feature Implementation Plan

## 1. Overview

Replace the current fixed accept criteria (velocity/pressure gradient by DN ranges) with a flexible prioritized list of pipe type configurations. Each pipe type in the list has individual accept criteria for every participating DN size.

**Key decisions:**
- **No backwards compatibility** - Complete rewrite, no legacy code, no fallbacks
- **MVVM with CommunityToolkit** - All ViewModels use `ObservableObject`, `RelayCommand`, etc.
- **Centralized styling** - All styles in `Theme.xaml`, following `GASettingsTab.xaml` pattern
- **Migration chain versioning** - Robust version handling for future changes

## 2. Settings Versioning System

### 2.1 Versioning Strategy

Implement a **migration chain** pattern where each version knows how to migrate TO the next version:

```
v1 -> v2 -> v3 -> ... -> vCurrent
```

### 2.2 Implementation

**Location:** `Acad-C3D-Tools/DimensioneringV2/Services/`

```csharp
// Base interface for versioned settings
public interface IVersionedSettings
{
    int Version { get; set; }
}

// Migration step interface
public interface ISettingsMigration<T> where T : IVersionedSettings
{
    int FromVersion { get; }
    int ToVersion { get; }
    T Migrate(T settings);
}

// Migration chain executor
public class SettingsMigrationService<T> where T : IVersionedSettings, new()
{
    private readonly List<ISettingsMigration<T>> _migrations;
    private readonly int _currentVersion;
    
    public T MigrateToCurrentVersion(T settings)
    {
        while (settings.Version < _currentVersion)
        {
            var migration = _migrations.First(m => m.FromVersion == settings.Version);
            settings = migration.Migrate(settings);
        }
        return settings;
    }
}
```

### 2.3 HydraulicSettings Version Property

```csharp
public partial class HydraulicSettings : ObservableObject, IHydraulicSettings, IVersionedSettings
{
    // Current version of HydraulicSettings schema
    public const int CurrentVersion = 2;  // Increment when schema changes
    
    [ObservableProperty]
    private int version = CurrentVersion;
    
    // ... rest of properties
}
```

### 2.4 Version History

| Version | Description |
|---------|-------------|
| 1 | Legacy format (pre-PipeTypePriority) - files without Version property |
| 2 | PipeTypePriority format - current implementation |

### 2.5 Migration from v1 (Legacy) to v2

When loading a file:
1. If `Version` property is missing or 0, treat as v1
2. Generate default `PipeTypeConfiguration` based on medium type rules
3. Set `Version = 2`

## 3. Data Model

### 3.1 New Classes

**Location:** `Acad-C3D-Tools/DimensioneringV2/Models/`

```csharp
// Holds velocity and pressure gradient for a single pipe dimension
public class DnAcceptCriteria
{
    public int NominalDiameter { get; set; }
    public double MaxVelocity { get; set; }      // m/s
    public int MaxPressureGradient { get; set; } // Pa/m
    public bool IsInitialized { get; set; }      // false = never edited by user
}

// A single pipe type with its configuration
public class PipeTypePriority
{
    public int Priority { get; set; }            // 1 = highest
    public PipeType PipeType { get; set; }
    public int MinDn { get; set; }
    public int MaxDn { get; set; }
    public List<DnAcceptCriteria> AcceptCriteria { get; set; }
    // Stub for Stikledninger rules (future feature)
    public object RegelConfig { get; set; }
}

// Container for FL or SL pipe settings
public class PipeTypeConfiguration
{
    public SegmentType SegmentType { get; set; }  // FL or SL
    public List<PipeTypePriority> Priorities { get; set; }
}
```

### 3.2 Integration with HydraulicSettings

**Complete rewrite** of [`HydraulicSettings.cs`](Acad-C3D-Tools/DimensioneringV2/NorsynHydraulic/HydraulicSettings.cs):

- **Remove** all old accept criteria properties:
  - `AcceptVelocity20_150FL`, `AcceptVelocity200_300FL`, `AcceptVelocity350PlusFL`
  - `AcceptPressureGradient20_150FL`, `AcceptPressureGradient200_300FL`, `AcceptPressureGradient350PlusFL`
  - `AcceptVelocityFlexibleSL`, `AcceptVelocity20_150SL`
  - `AcceptPressureGradientFlexibleSL`, `AcceptPressureGradient20_150SL`
  - `UsePertFlextraFL`, `PertFlextraMaxDnFL`, `PipeTypeSL`, `PipeTypeFL`

- **Add** new properties:
  ```csharp
  [ObservableProperty]
  private PipeTypeConfiguration pipeConfigFL;

  [ObservableProperty]
  private PipeTypeConfiguration pipeConfigSL;
  ```

## 4. Serialization

### 4.1 Use Generic SettingsSerializer

Use existing [`SettingsSerializer<T>`](Acad-C3D-Tools/DimensioneringV2/Services/SettingsSerializer.cs) for storage.

The `FlexDataStore` already handles JSON serialization internally, so nested objects like `PipeTypeConfiguration` will be serialized automatically.

### 4.2 Loading Flow

```
Load() called
    |
    v
Check if settings exist in FlexDataStore
    |
    +-- No --> Create new HydraulicSettings with default PipeTypeConfiguration
    |
    +-- Yes --> Deserialize settings
                    |
                    v
                Check Version property
                    |
                    +-- Version < CurrentVersion --> Run migration chain
                    |
                    +-- Version == CurrentVersion --> Use as-is
                    |
                    +-- Version > CurrentVersion --> Warning + use as-is (forward compat)
```

### 4.3 Default Configuration Factory

**Location:** `Acad-C3D-Tools/DimensioneringV2/Services/PipeConfigDefaultsFactory.cs`

```csharp
public static class PipeConfigDefaultsFactory
{
    public static PipeTypeConfiguration CreateDefaultFL(MediumTypeEnum medium)
    {
        var rules = MediumRulesFactory.GetRules(medium);
        // Build default based on valid pipe types for medium
        // For Water: PertFlextra (50-75) priority 1, Steel (32-600) priority 2
    }
    
    public static PipeTypeConfiguration CreateDefaultSL(MediumTypeEnum medium)
    {
        // For Water: AluPEX (26-32) priority 1, Steel (32-150) priority 2
    }
}
```

## 5. UI Components

### 5.1 Style Management

All new UI components must use shared styles from [`Theme.xaml`](Acad-C3D-Tools/DimensioneringV2/UI/Theme.xaml).

Following the pattern in [`GASettingsTab.xaml`](Acad-C3D-Tools/DimensioneringV2/UI/GASettingsTab.xaml):

```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Theme.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        
        <!-- Local style overrides if absolutely necessary -->
    </ResourceDictionary>
</UserControl.Resources>
```

### 5.2 Required Theme.xaml Additions

Add to `Theme.xaml`:
- `ModernGroupBox` style (copy from GASettingsTab.xaml to Theme.xaml)
- `PriorityListStyle` for DataGrid/ListView
- `ToolbarButtonStyle` for Add/Delete/Up/Down buttons

### 5.3 SettingsTab Changes

In [`SettingsTab.xaml`](Acad-C3D-Tools/DimensioneringV2/UI/SettingsTab.xaml):

- **Remove** all "Acceptkriterie" rows from both Fordelingsledninger and Stikledninger
- **Remove** `UsePertFlextraFL`, `PertFlextraMaxDnFL`, `PipeTypeSL` rows
- **Add** single "Rørindstillinger" row per section with "Rediger" button

### 5.4 New Windows (Modal Dialogs)

| Window | Purpose |
|--------|---------|
| `PipeSettingsWindow.xaml` | Main editor for PipeTypePriority list |
| `AcceptCriteriaWindow.xaml` | Per-DN criteria editor |

### 5.5 PipeSettingsWindow Layout

```
+----------------------------------------------------------+
| Rørindstillinger for Fordelingsledninger          [X]    |
+----------------------------------------------------------+
| [+ Tilføj] [Slet] [Op] [Ned]                             |
+----------------------------------------------------------+
| Pri | Rørtype      | Min DN | Max DN | Regler* | Accept. |
+----------------------------------------------------------+
|  1  | PertFlextra  | [50 v] | [75 v] | [...]   | [Rediger]|
|  2  | Stål         | [32 v] | [600v] | [...]   | [Rediger]|
+----------------------------------------------------------+
|                              [OK] [Annuller]             |
+----------------------------------------------------------+

* Regler column only visible for Stikledninger (stub button for now)
```

### 5.6 AcceptCriteriaWindow Layout

```
+----------------------------------------------------------+
| Acceptkriterier for PertFlextra                   [X]    |
+----------------------------------------------------------+
| DN    | Max Hastighed [m/s] | Max Trykgradient [Pa/m]   |
+----------------------------------------------------------+
|  50   | [1.5          ]     | [100                ]      |
|  63   | [1.5          ]     | [100                ]      |
|  75   | [1.5          ]     | [100                ]      |
+----------------------------------------------------------+
| Only DN sizes within Min-Max range are shown             |
+----------------------------------------------------------+
|                              [OK] [Annuller]             |
+----------------------------------------------------------+
```

## 6. Calculation Integration (DEFERRED)

**Status: POSTPONED** - Will be implemented after UI is complete.

### 6.1 Stub Methods

Add stub implementations that throw `NotImplementedException` or use placeholder logic:

```csharp
// In HydraulicCalc.cs
private double Vmax(Dim dim, SegmentType st)
{
    // TODO: Implement using PipeTypeConfiguration
    throw new NotImplementedException("PipeTypePriority calculation not yet implemented");
}

private double dPdx_max(Dim dim, SegmentType st)
{
    // TODO: Implement using PipeTypeConfiguration
    throw new NotImplementedException("PipeTypePriority calculation not yet implemented");
}
```

### 6.2 Interface Changes (Deferred)

Changes to `IHydraulicSettings` interface will be made when calculation integration is implemented.

## 7. Validation

### 7.1 Validation Points

| When | Action |
|------|--------|
| Window close | Warn if any `DnAcceptCriteria.IsInitialized == false` within enabled DN range |
| Calculation start | Abort with error message listing uninitialized DN sizes |

### 7.2 Validation Service

**Location:** `Acad-C3D-Tools/DimensioneringV2/Services/PipeConfigValidationService.cs`

```csharp
public class PipeConfigValidationService
{
    public ValidationResult Validate(PipeTypeConfiguration config);
    public List<string> GetUninitializedDimensions(PipeTypeConfiguration config);
    public bool HasOverlappingDnRanges(PipeTypeConfiguration config);
}
```

## 8. Implementation Phases

### Phase 1: Infrastructure (Data Model, Versioning, Serialization)
1. Implement versioning infrastructure (`IVersionedSettings`, `SettingsMigrationService`)
2. Create model classes (`DnAcceptCriteria`, `PipeTypePriority`, `PipeTypeConfiguration`)
3. Rewrite `HydraulicSettings` with new properties (remove old ones completely)
4. Create `PipeConfigDefaultsFactory` for generating default configurations
5. Implement v1->v2 migration for legacy files

### Phase 2: UI Implementation
1. Add required styles to `Theme.xaml`
2. Create `PipeSettingsWindow` + `PipeSettingsViewModel`
3. Create `AcceptCriteriaWindow` + `AcceptCriteriaViewModel`
4. Update `SettingsTab.xaml` - remove old rows, add "Rørindstillinger" rows
5. Update `SettingsTabViewModel` with commands

### Phase 3: Validation and Stubs
1. Create `PipeConfigValidationService`
2. Add validation to window close events
3. Add stub methods in calculation classes

### Phase 4: Calculation Integration (FUTURE)
- Will be tackled separately after Phase 1-3 are complete and tested

## 9. Files to Create

| File | Description |
|------|-------------|
| `Services/IVersionedSettings.cs` | Versioning interface |
| `Services/SettingsMigrationService.cs` | Migration chain executor |
| `Services/HydraulicSettingsMigrations.cs` | Specific migrations for HydraulicSettings |
| `Models/DnAcceptCriteria.cs` | Per-DN accept criteria data |
| `Models/PipeTypePriority.cs` | Single pipe type configuration |
| `Models/PipeTypeConfiguration.cs` | Container for FL/SL priorities |
| `Services/PipeConfigDefaultsFactory.cs` | Default configuration generator |
| `Services/PipeConfigValidationService.cs` | Validation logic |
| `UI/PipeSettings/PipeSettingsWindow.xaml` | Main pipe settings editor |
| `UI/PipeSettings/PipeSettingsViewModel.cs` | ViewModel for main editor |
| `UI/PipeSettings/AcceptCriteriaWindow.xaml` | Per-DN criteria editor |
| `UI/PipeSettings/AcceptCriteriaViewModel.cs` | ViewModel for criteria editor |

## 10. Files to Modify

| File | Changes |
|------|---------|
| [`HydraulicSettings.cs`](Acad-C3D-Tools/DimensioneringV2/NorsynHydraulic/HydraulicSettings.cs) | **Rewrite**: Remove old properties, add versioning, add new config properties |
| [`Theme.xaml`](Acad-C3D-Tools/DimensioneringV2/UI/Theme.xaml) | Add shared styles for new windows |
| [`SettingsTab.xaml`](Acad-C3D-Tools/DimensioneringV2/UI/SettingsTab.xaml) | Remove accept criteria rows, add Rørindstillinger rows |
| [`SettingsTabViewModel.cs`](Acad-C3D-Tools/DimensioneringV2/UI/SettingsTabViewModel.cs) | Add commands for new windows, remove old ValidPipeTypes logic |
| [`HydraulicCalc.cs`](Acad-C3D-Tools/NorsynHydraulicShared/HydraulicCalc.cs) | Add stub methods (Phase 3) |

## 11. Default Values

When creating a new `PipeTypePriority`, use these defaults:

### Fordelingsledninger (FL)

| Pipe Type | DN Range | Default Velocity | Default Gradient |
|-----------|----------|-----------------|------------------|
| PertFlextra | 50-75 | 1.5 m/s | 100 Pa/m |
| Stål | 20-150 | 1.5 m/s | 100 Pa/m |
| Stål | 200-300 | 2.5 m/s | 100 Pa/m |
| Stål | 350+ | 3.0 m/s | 120 Pa/m |

### Stikledninger (SL)

| Pipe Type | DN Range | Default Velocity | Default Gradient |
|-----------|----------|-----------------|------------------|
| AluPEX | 26-32 | 1.0 m/s | 600 Pa/m |
| PertFlextra | 25-75 | 1.0 m/s | 600 Pa/m |
| Kobber | 22-28 | 1.0 m/s | 600 Pa/m |
| Stål | 32-150 | 1.5 m/s | 600 Pa/m |

## 12. Medium Type Restrictions

When adding a `PipeTypePriority`, only show pipe types valid for the selected medium:

| Medium | Valid Pipe Types (FL) | Valid Pipe Types (SL) |
|--------|----------------------|----------------------|
| Water | Stål, PertFlextra | AluPEX, PertFlextra, Kobber, Stål |
| Water72Ipa28 | Pe | Pe |

Use `MediumRulesFactory.GetRules(medium).GetValidPipeTypesForSupply/Service()` to filter options.

```

I've updated the plan with all your feedback. Here's a summary of the key changes:

## Summary of Plan Updates

### 1. No Backwards Compatibility
- Complete rewrite of `HydraulicSettings` - all old accept criteria properties will be **removed**, not marked obsolete
- No fallback code, no legacy support within the code itself

### 2. Settings Versioning System (New Section 2)
Implemented **migration chain** pattern:
- `IVersionedSettings` interface with `Version` property inside the settings class
- Each version knows how to migrate TO the next version (v1→v2→v3→...)
- `SettingsMigrationService<T>` executes the chain automatically on load
- **Version 1** = Legacy format (files without Version property)
- **Version 2** = Current PipeTypePriority format
- Future schema changes just add new migrations to the chain

### 3. Serialization
- Uses existing generic `SettingsSerializer<T>` 
- When loading files without saved config, generate defaults via `PipeConfigDefaultsFactory`

### 4. WPF/MVVM Standards
- All ViewModels use CommunityToolkit MVVM
- Shared styles in `Theme.xaml` following `GASettingsTab.xaml` pattern
- `ModernGroupBox` and other styles will be moved to Theme.xaml

### 5. Calculation Integration Deferred
- Marked as **POSTPONED** in the plan
- Stub methods will throw `NotImplementedException`
- We'll tackle this incrementally after UI is working

### 6. Updated Implementation Phases
- **Phase 1**: Versioning infrastructure + Data models + Settings rewrite + Defaults factory
- **Phase 2**: UI (Theme styles + Windows + SettingsTab update)
- **Phase 3**: Validation + Stubs
- **Phase 4**: Calculation integration (FUTURE - separate effort)

The plan is saved at `c:\Users\MichailGolubjev\.cursor\plans\pipetypepriority_feature_8e9514ba.plan.md`. See you tomorrow!

---

**User**

write the plan to an md file in the project in appropriate folder, maybe I will work from home tomorrow and will need it there

---

**Cursor**

```markdown
# PipeTypePriority Feature Implementation Plan

**Created:** 2026-01-13  
**Status:** Planning complete, ready for implementation

## Implementation Todos

| Phase | Task | Status |
|-------|------|--------|
| 1 | Implement settings versioning infrastructure with migration chain pattern | pending |
| 1 | Create data model classes (DnAcceptCriteria, PipeTypePriority, PipeTypeConfiguration) | pending |
| 1 | Rewrite HydraulicSettings - remove old accept criteria, add new config properties | pending |
| 1 | Create default configuration factory for new files and legacy files without config | pending |
| 2 | Add required shared styles to Theme.xaml for new windows | pending |
| 2 | Create PipeSettingsWindow.xaml and PipeSettingsViewModel (MVVM with CommunityToolkit) | pending |
| 2 | Create AcceptCriteriaWindow.xaml and AcceptCriteriaViewModel | pending |
| 2 | Update SettingsTab.xaml - replace accept criteria rows with Rørindstillinger | pending |
| 2 | Update SettingsTabViewModel with commands for new windows | pending |
| 3 | Create PipeConfigValidationService with validation logic | pending |
| 3 | Add stub methods in HydraulicCalc/MaxFlowCalc for future calculation integration | pending |

---

## 1. Overview

Replace the current fixed accept criteria (velocity/pressure gradient by DN ranges) with a flexible prioritized list of pipe type configurations. Each pipe type in the list has individual accept criteria for every participating DN size.

**Key decisions:**

- **No backwards compatibility** - Complete rewrite, no legacy code, no fallbacks
- **MVVM with CommunityToolkit** - All ViewModels use `ObservableObject`, `RelayCommand`, etc.
- **Centralized styling** - All styles in `Theme.xaml`, following `GASettingsTab.xaml` pattern
- **Migration chain versioning** - Robust version handling for future changes

## 2. Settings Versioning System

### 2.1 Versioning Strategy

Implement a **migration chain** pattern where each version knows how to migrate TO the next version:

```
v1 -> v2 -> v3 -> ... -> vCurrent
```

### 2.2 Implementation

**Location:** `DimensioneringV2/Services/`

```csharp
// Base interface for versioned settings
public interface IVersionedSettings
{
    int Version { get; set; }
}

// Migration step interface
public interface ISettingsMigration<T> where T : IVersionedSettings
{
    int FromVersion { get; }
    int ToVersion { get; }
    T Migrate(T settings);
}

// Migration chain executor
public class SettingsMigrationService<T> where T : IVersionedSettings, new()
{
    private readonly List<ISettingsMigration<T>> _migrations;
    private readonly int _currentVersion;
    
    public T MigrateToCurrentVersion(T settings)
    {
        while (settings.Version < _currentVersion)
        {
            var migration = _migrations.First(m => m.FromVersion == settings.Version);
            settings = migration.Migrate(settings);
        }
        return settings;
    }
}
```

### 2.3 HydraulicSettings Version Property

```csharp
public partial class HydraulicSettings : ObservableObject, IHydraulicSettings, IVersionedSettings
{
    // Current version of HydraulicSettings schema
    public const int CurrentVersion = 2;  // Increment when schema changes
    
    [ObservableProperty]
    private int version = CurrentVersion;
    
    // ... rest of properties
}
```

### 2.4 Version History

| Version | Description |
|---------|-------------|
| 1 | Legacy format (pre-PipeTypePriority) - files without Version property |
| 2 | PipeTypePriority format - current implementation |

### 2.5 Migration from v1 (Legacy) to v2

When loading a file:

1. If `Version` property is missing or 0, treat as v1
2. Generate default `PipeTypeConfiguration` based on medium type rules
3. Set `Version = 2`

## 3. Data Model

### 3.1 New Classes

**Location:** `DimensioneringV2/Models/`

```csharp
// Holds velocity and pressure gradient for a single pipe dimension
public class DnAcceptCriteria
{
    public int NominalDiameter { get; set; }
    public double MaxVelocity { get; set; }      // m/s
    public int MaxPressureGradient { get; set; } // Pa/m
    public bool IsInitialized { get; set; }      // false = never edited by user
}

// A single pipe type with its configuration
public class PipeTypePriority
{
    public int Priority { get; set; }            // 1 = highest
    public PipeType PipeType { get; set; }
    public int MinDn { get; set; }
    public int MaxDn { get; set; }
    public List<DnAcceptCriteria> AcceptCriteria { get; set; }
    // Stub for Stikledninger rules (future feature)
    public object RegelConfig { get; set; }
}

// Container for FL or SL pipe settings
public class PipeTypeConfiguration
{
    public SegmentType SegmentType { get; set; }  // FL or SL
    public List<PipeTypePriority> Priorities { get; set; }
}
```

### 3.2 Integration with HydraulicSettings

**Complete rewrite** of `HydraulicSettings.cs`:

- **Remove** all old accept criteria properties:
  - `AcceptVelocity20_150FL`, `AcceptVelocity200_300FL`, `AcceptVelocity350PlusFL`
  - `AcceptPressureGradient20_150FL`, `AcceptPressureGradient200_300FL`, `AcceptPressureGradient350PlusFL`
  - `AcceptVelocityFlexibleSL`, `AcceptVelocity20_150SL`
  - `AcceptPressureGradientFlexibleSL`, `AcceptPressureGradient20_150SL`
  - `UsePertFlextraFL`, `PertFlextraMaxDnFL`, `PipeTypeSL`, `PipeTypeFL`

- **Add** new properties:
  ```csharp
  [ObservableProperty]
  private PipeTypeConfiguration pipeConfigFL;
  
  [ObservableProperty]
  private PipeTypeConfiguration pipeConfigSL;
  ```

## 4. Serialization

### 4.1 Use Generic SettingsSerializer

Use existing `SettingsSerializer<T>` from `Services/SettingsSerializer.cs` for storage.

The `FlexDataStore` already handles JSON serialization internally, so nested objects like `PipeTypeConfiguration` will be serialized automatically.

### 4.2 Loading Flow

```
Load() called
    |
    v
Check if settings exist in FlexDataStore
    |
    +-- No --> Create new HydraulicSettings with default PipeTypeConfiguration
    |
    +-- Yes --> Deserialize settings
                    |
                    v
                Check Version property
                    |
                    +-- Version < CurrentVersion --> Run migration chain
                    |
                    +-- Version == CurrentVersion --> Use as-is
                    |
                    +-- Version > CurrentVersion --> Warning + use as-is (forward compat)
```

### 4.3 Default Configuration Factory

**Location:** `DimensioneringV2/Services/PipeConfigDefaultsFactory.cs`

```csharp
public static class PipeConfigDefaultsFactory
{
    public static PipeTypeConfiguration CreateDefaultFL(MediumTypeEnum medium)
    {
        var rules = MediumRulesFactory.GetRules(medium);
        // Build default based on valid pipe types for medium
        // For Water: PertFlextra (50-75) priority 1, Steel (32-600) priority 2
    }
    
    public static PipeTypeConfiguration CreateDefaultSL(MediumTypeEnum medium)
    {
        // For Water: AluPEX (26-32) priority 1, Steel (32-150) priority 2
    }
}
```

## 5. UI Components

### 5.1 Style Management

All new UI components must use shared styles from `Theme.xaml`.

Following the pattern in `GASettingsTab.xaml`:

```xml
<UserControl.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Theme.xaml"/>
        </ResourceDictionary.MergedDictionaries>
        
        <!-- Local style overrides if absolutely necessary -->
    </ResourceDictionary>
</UserControl.Resources>
```

### 5.2 Required Theme.xaml Additions

Add to `Theme.xaml`:

- `ModernGroupBox` style (copy from GASettingsTab.xaml to Theme.xaml)
- `PriorityListStyle` for DataGrid/ListView
- `ToolbarButtonStyle` for Add/Delete/Up/Down buttons

### 5.3 SettingsTab Changes

In `SettingsTab.xaml`:

- **Remove** all "Acceptkriterie" rows from both Fordelingsledninger and Stikledninger
- **Remove** `UsePertFlextraFL`, `PertFlextraMaxDnFL`, `PipeTypeSL` rows
- **Add** single "Rørindstillinger" row per section with "Rediger" button

### 5.4 New Windows (Modal Dialogs)

| Window | Purpose |
|--------|---------|
| `PipeSettingsWindow.xaml` | Main editor for PipeTypePriority list |
| `AcceptCriteriaWindow.xaml` | Per-DN criteria editor |

### 5.5 PipeSettingsWindow Layout

```
+----------------------------------------------------------+
| Rørindstillinger for Fordelingsledninger          [X]    |
+----------------------------------------------------------+
| [+ Tilføj] [Slet] [Op] [Ned]                             |
+----------------------------------------------------------+
| Pri | Rørtype      | Min DN | Max DN | Regler* | Accept. |
+----------------------------------------------------------+
|  1  | PertFlextra  | [50 v] | [75 v] | [...]   | [Rediger]|
|  2  | Stål         | [32 v] | [600v] | [...]   | [Rediger]|
+----------------------------------------------------------+
|                              [OK] [Annuller]             |
+----------------------------------------------------------+

* Regler column only visible for Stikledninger (stub button for now)
```

### 5.6 AcceptCriteriaWindow Layout

```
+----------------------------------------------------------+
| Acceptkriterier for PertFlextra                   [X]    |
+----------------------------------------------------------+
| DN    | Max Hastighed [m/s] | Max Trykgradient [Pa/m]   |
+----------------------------------------------------------+
|  50   | [1.5          ]     | [100                ]      |
|  63   | [1.5          ]     | [100                ]      |
|  75   | [1.5          ]     | [100                ]      |
+----------------------------------------------------------+
| Only DN sizes within Min-Max range are shown             |
+----------------------------------------------------------+
|                              [OK] [Annuller]             |
+----------------------------------------------------------+
```

## 6. Calculation Integration (DEFERRED)

**Status: POSTPONED** - Will be implemented after UI is complete.

### 6.1 Stub Methods

Add stub implementations that throw `NotImplementedException` or use placeholder logic:

```csharp
// In HydraulicCalc.cs
private double Vmax(Dim dim, SegmentType st)
{
    // TODO: Implement using PipeTypeConfiguration
    throw new NotImplementedException("PipeTypePriority calculation not yet implemented");
}

private double dPdx_max(Dim dim, SegmentType st)
{
    // TODO: Implement using PipeTypeConfiguration
    throw new NotImplementedException("PipeTypePriority calculation not yet implemented");
}
```

### 6.2 Interface Changes (Deferred)

Changes to `IHydraulicSettings` interface will be made when calculation integration is implemented.

## 7. Validation

### 7.1 Validation Points

| When | Action |
|------|--------|
| Window close | Warn if any `DnAcceptCriteria.IsInitialized == false` within enabled DN range |
| Calculation start | Abort with error message listing uninitialized DN sizes |

### 7.2 Validation Service

**Location:** `DimensioneringV2/Services/PipeConfigValidationService.cs`

```csharp
public class PipeConfigValidationService
{
    public ValidationResult Validate(PipeTypeConfiguration config);
    public List<string> GetUninitializedDimensions(PipeTypeConfiguration config);
    public bool HasOverlappingDnRanges(PipeTypeConfiguration config);
}
```

## 8. Implementation Phases

### Phase 1: Infrastructure (Data Model, Versioning, Serialization)

1. Implement versioning infrastructure (`IVersionedSettings`, `SettingsMigrationService`)
2. Create model classes (`DnAcceptCriteria`, `PipeTypePriority`, `PipeTypeConfiguration`)
3. Rewrite `HydraulicSettings` with new properties (remove old ones completely)
4. Create `PipeConfigDefaultsFactory` for generating default configurations
5. Implement v1->v2 migration for legacy files

### Phase 2: UI Implementation

1. Add required styles to `Theme.xaml`
2. Create `PipeSettingsWindow` + `PipeSettingsViewModel`
3. Create `AcceptCriteriaWindow` + `AcceptCriteriaViewModel`
4. Update `SettingsTab.xaml` - remove old rows, add "Rørindstillinger" rows
5. Update `SettingsTabViewModel` with commands

### Phase 3: Validation and Stubs

1. Create `PipeConfigValidationService`
2. Add validation to window close events
3. Add stub methods in calculation classes

### Phase 4: Calculation Integration (FUTURE)

- Will be tackled separately after Phase 1-3 are complete and tested

## 9. Files to Create

| File | Description |
|------|-------------|
| `Services/IVersionedSettings.cs` | Versioning interface |
| `Services/SettingsMigrationService.cs` | Migration chain executor |
| `Services/HydraulicSettingsMigrations.cs` | Specific migrations for HydraulicSettings |
| `Models/DnAcceptCriteria.cs` | Per-DN accept criteria data |
| `Models/PipeTypePriority.cs` | Single pipe type configuration |
| `Models/PipeTypeConfiguration.cs` | Container for FL/SL priorities |
| `Services/PipeConfigDefaultsFactory.cs` | Default configuration generator |
| `Services/PipeConfigValidationService.cs` | Validation logic |
| `UI/PipeSettings/PipeSettingsWindow.xaml` | Main pipe settings editor |
| `UI/PipeSettings/PipeSettingsViewModel.cs` | ViewModel for main editor |
| `UI/PipeSettings/AcceptCriteriaWindow.xaml` | Per-DN criteria editor |
| `UI/PipeSettings/AcceptCriteriaViewModel.cs` | ViewModel for criteria editor |

## 10. Files to Modify

| File | Changes |
|------|---------|
| `NorsynHydraulic/HydraulicSettings.cs` | **Rewrite**: Remove old properties, add versioning, add new config properties |
| `UI/Theme.xaml` | Add shared styles for new windows |
| `UI/SettingsTab.xaml` | Remove accept criteria rows, add Rørindstillinger rows |
| `UI/SettingsTabViewModel.cs` | Add commands for new windows, remove old ValidPipeTypes logic |
| `NorsynHydraulicShared/HydraulicCalc.cs` | Add stub methods (Phase 3) |

## 11. Default Values

When creating a new `PipeTypePriority`, use these defaults:

### Fordelingsledninger (FL)

| Pipe Type | DN Range | Default Velocity | Default Gradient |
|-----------|----------|------------------|------------------|
| PertFlextra | 50-75 | 1.5 m/s | 100 Pa/m |
| Stål | 20-150 | 1.5 m/s | 100 Pa/m |
| Stål | 200-300 | 2.5 m/s | 100 Pa/m |
| Stål | 350+ | 3.0 m/s | 120 Pa/m |

### Stikledninger (SL)

| Pipe Type | DN Range | Default Velocity | Default Gradient |
|-----------|----------|------------------|------------------|
| AluPEX | 26-32 | 1.0 m/s | 600 Pa/m |
| PertFlextra | 25-75 | 1.0 m/s | 600 Pa/m |
| Kobber | 22-28 | 1.0 m/s | 600 Pa/m |
| Stål | 32-150 | 1.5 m/s | 600 Pa/m |

## 12. Medium Type Restrictions

When adding a `PipeTypePriority`, only show pipe types valid for the selected medium:

| Medium | Valid Pipe Types (FL) | Valid Pipe Types (SL) |
|--------|----------------------|----------------------|
| Water | Stål, PertFlextra | AluPEX, PertFlextra, Kobber, Stål |
| Water72Ipa28 | Pe | Pe |

Use `MediumRulesFactory.GetRules(medium).GetValidPipeTypesForSupply/Service()` to filter options.

```

Done! The plan has been saved to:

**`Acad-C3D-Tools/DimensioneringV2/Docs/PipeTypePriority_Implementation_Plan.md`**

This file is now part of your Git repository, so you'll have access to it from any machine where you clone/pull the repo. The plan includes:

- Implementation todos table at the top for quick reference
- Complete specification with code examples
- Phase breakdown for incremental implementation
- File lists (create and modify)
- Default values and medium type restrictions

Have a good evening, and see you tomorrow! 🏠

---

**User**

okay, the plan is now implemented. What we need to implement now is 'Regler' button. Read the implementation that another agent created, I mean read files, becuase it is not 100% as was written in the implementation plan. I've deleted the old plan btw.
'Regler' is going to be a feature of the pipepriorities for SL. Currently there are only two types of rules:
1. no rules -> priorities execute in sequence as defined
2. parent pipe rule -> when a SL needs to determine pipetype to use it must look at the parent pipe. In my dim2 application the pipes are arranged in a graph, and pipes are edges (not nodes). the nodes in this graph are only used to bind the edges, they do not participate in any logic. why I used edges as pipes? because else I couldn't determine children and parents at junctions. And in my system in dim2 Stikledninger are ALWAYS connected to a Fordelingsledning edge through a node. Ie I NEVER have SL edge to have parent edge as SL, they are always FL. Now it is a little misleading graphtheoretically to call edges parents, because nodes actually keep the data on what is connected to what, but i mean this in a way if you look at the supply flow and think of the graph as a pipe network. Then this is very close to what happens in reality. So if a SL detects that its' parent pipe is a Pert, then the paren pipe rule says it needs to be a pert stik, parent steel makes the sl a alupex stik. So in our priorities SL window I would have three priorities 1. alupex with steel parent rule, 2. pert with pert parent rule, 3. steel that is a fall through for flows that are too large to handle for the above two first rules. It has no rules.

So we have two tasks:
implement the rule settings for SL domain in the ui in the dim2 project.
rewrite the logic that builds the maxflowtable for SL and that determines what pipetype to use for a SL based on the parent pipe.

this rule based calculation brings us a challenge. As currently I calculate the SL once before the genetic algorithm I run that finds the most effective route for my piping based on the road/street network.
This is done to avoid calculating SL at every genetic fitness evaluation.
But at this point we don't know parent pipetype so the rules cannot be used or used with a default pipetype. this is okay, because the only thing we need at the start is the characteristic flows from each client segment (SL). These flows are then summed through the network and a design flow is calculated. So after we've implemented the UI for the rules we can look on how we can make the calculations work.
but clearly we would need a second pass where the program first calculates the FL pipes with summed flow totals and then calculates SL using a detected parent pipe (if rules are present for SL). This is a distinct departure from my current procedure. @Acad-C3D-Tools/DimensioneringV2/MapCommands/CalculateGA.cs is the main calculation procedure.

Btw. I forgot to mention, that NorsynHydraulicShared should be treated as a stand alone library and dim2 project as a consumer that implements a ui for handling NHS configurations, but NHS should be able to run from other projects with settings either created programmatically or again via a ui. So we have here separation of concerns. So always remember that in our design NHS should not depend on Dim2 project to be able to calculate pipe sizes.

---

**Cursor**

Let me first explore the current implementation to understand what's already been built for the PipeTypePriority feature.

Now let me look at the current HydraulicCalc to understand how the max flow table is being built and used:

I now have a complete picture of the current implementation. Let me ask some clarifying questions to finalize the Regler feature specification:

---

**User**

Ah! good point! rules must look at fl priorities and parent pipe rule may only be defined if a corresponding pipetype is defined as priority in FL. if a pipetype is removed from FL priority and it is used as a parenttype rule in SL then SL settings are invalidated ie rule is removed

