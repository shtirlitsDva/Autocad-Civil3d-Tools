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
