# DataScience Module - Agent Instructions

This document describes the data access patterns and command structure used in the DataScience module for AutoCAD/Civil 3D plugin development.

## Overview

The DataScience module provides commands (prefixed with `DS`) for analyzing and manipulating property data attached to AutoCAD entities. It heavily relies on **PropertySets** - a Civil 3D/Map 3D feature that allows attaching structured data to entities.

---

## PropertySet System

### Core Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `PropertySetManager` | `UtilitiesCommonSHARED/PropertySets/PropertySetManager.cs` | Core class for reading/writing PropertySet data |
| `PSetDefs` | Same file | Nested class containing PropertySet definitions |
| `PSetDefs.DefinedSets` | Same file | Enum of all predefined PropertySet types |
| Wrapper classes (e.g., `BBR`) | Same file | Typed wrappers providing property accessors |

### Two Types of PropertySet Access

#### 1. Predefined PropertySets (Type-Safe)

For PropertySets defined in `PSetDefs`, use typed wrapper classes:

```csharp
// Ensure the PropertySet definition exists in the drawing
PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

// Create wrapper - provides typed property access
var bbr = new BBR(blockReference);

// Read/write via properties
double area = bbr.SamletBoligareal;    // Read
bbr.EstimeretVarmeForbrug = 150.5;     // Write
string address = bbr.Adresse;          // Read
```

**Available wrapper classes:**
- `BBR` - Building Registration data (Danish: Bygnings- og Boligregistret)
- `NtrData` - Network tracing data
- `PSM_Pipeline` - Pipeline-specific data

#### 2. Non-Defined PropertySets (Dynamic)

For PropertySets created dynamically (e.g., from CSV imports), use static methods:

```csharp
// Read from any PropertySet by name
double value = PropertySetManager.ReadNonDefinedPropertySetDouble(
    entity,           // The entity with attached PropertySet
    "PropertySetName", // Name of the PropertySet
    "PropertyName"    // Name of the property within the set
);

string text = PropertySetManager.ReadNonDefinedPropertySetString(
    entity, "MyData", "Description");

// Also available: ReadNonDefinedPropertySetInt, ReadNonDefinedPropertySetObject
```

---

## Command Structure Pattern

All DS commands follow this standard structure:

```csharp
[CommandMethod("DSCOMMANDNAME")]
public void dscommandname()
{
    // 1. Get document and database references
    DocumentCollection docCol = Application.DocumentManager;
    Database localDb = docCol.MdiActiveDocument.Database;

    // 2. Ensure required PropertySet definitions exist
    PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

    // 3. Start transaction (using statement for auto-disposal)
    using Transaction tx = localDb.TransactionManager.StartTransaction();

    try
    {
        // 4. Query entities and perform operations
        // ... command logic here ...
    }
    catch (System.Exception ex)
    {
        // 5. On error: abort transaction and log
        tx.Abort();
        prdDbg(ex);
        return;
    }

    // 6. On success: commit transaction
    tx.Commit();
}
```

### Key Points

- **Always call `UpdatePropertySetDefinition`** before accessing predefined PropertySets
- **Transaction is required** - PropertySetManager methods expect an active transaction
- **Abort on error** - Always call `tx.Abort()` in catch block before returning
- **Commit on success** - Call `tx.Commit()` only after all operations succeed

---

## Entity Querying

### Querying by Type with PropertySet Filter

```csharp
// Get all BlockReferences that have the BBR PropertySet attached
var bbrs = localDb.HashSetOfTypeWithPs<BlockReference>(tx, PSetDefs.DefinedSets.BBR)
    .Select(x => new BBR(x))
    .ToHashSet();
```

### Querying by Type Only

```csharp
// Get all DBPoints in the drawing
var pts = localDb.HashSetOfType<DBPoint>(tx);
```

### Interactive Selection

```csharp
// Select single entity of specific type
var id = Interaction.GetEntity(
    "\nSelect block: ",
    typeof(BlockReference));

if (id == ObjectId.Null)
    return; // User cancelled

var entity = tx.GetObject(id, OpenMode.ForWrite) as BlockReference;
```

**Selection loop pattern** (select multiple until cancel):
```csharp
List<BlockReference> selected = new List<BlockReference>();
while (true)
{
    var id = Interaction.GetEntity($"\nSelect ({selected.Count}): ", typeof(BlockReference));
    if (id == ObjectId.Null) break;  // Space/Escape pressed

    var br = tx.GetObject(id, OpenMode.ForWrite) as BlockReference;
    if (br != null) selected.Add(br);
}
```

---

## BBR Wrapper Class Reference

The `BBR` class wraps BlockReference entities representing buildings from the Danish Building Register.

### Constructor
```csharp
var bbr = new BBR(blockReference); // Throws if entity is not BlockReference
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Entity` | `Entity` | The underlying AutoCAD entity |
| `X`, `Y` | `double` | Geographic coordinates |
| `Adresse` | `string` | Full address (street + house number) |
| `Vejnavn` | `string` | Street name |
| `Husnummer` | `string` | House number |
| `SamletBoligareal` | `int` | Total residential floor area (m²) |
| `SamletErhvervsareal` | `int` | Total commercial floor area (m²) |
| `SamletBygningsareal` | `int` | Total building area (m²) |
| `EstimeretVarmeForbrug` | `double` | Estimated heat consumption (MWh) |
| `TempDeltaVarme` | `double` | Temperature delta for heating |
| `VarmeInstallation` | `string` | Heating installation type |
| `OpvarmningsMiddel` | `string` | Heating medium |
| `Status` | `string` | Building status |

---

## Debug Output

Use `prdDbg()` for command-line output:

```csharp
prdDbg("Simple message");
prdDbg($"Formatted: {value:F2}");
prdDbg(exception);  // Logs exception details
```

---

## Common Patterns

### Spatial Query (Find Nearby Entities)

```csharp
var location = new Point3d(bbr.X, bbr.Y, 0);
var nearbyPoints = allPoints
    .Where(p => p.Position.DistanceHorizontalTo(location) < 3.0);
```

### Weighted Average Calculation

```csharp
var data = points.Select(p => (
    Weight: PropertySetManager.ReadNonDefinedPropertySetDouble(p, "data", "Energy"),
    Value: PropertySetManager.ReadNonDefinedPropertySetDouble(p, "data", "Temperature")
));

double totalWeight = data.Sum(d => d.Weight);
double weightedAverage = data.Sum(d => d.Weight * d.Value) / totalWeight;
```

### Proportional Distribution

```csharp
double totalArea = blocks.Sum(b => b.FloorSpace);
foreach (var block in blocks)
{
    double proportion = block.FloorSpace / totalArea;
    block.AllocatedValue = totalValue * proportion;
}
```

---

## File Organization

```
DataScience/
├── DataScience.cs              # Main command implementations
├── PropertySetBrowser/         # WPF browser for PropertySet data
│   ├── PropertySetBrowserWindow.xaml
│   ├── PropertySetBrowserWindow.xaml.cs
│   └── PropertySetBrowserViewModel.cs
├── CsvTypedDataTable.cs        # CSV import with type inference
├── GenericPropertySetImporter.cs # CSV to PropertySet importer
└── MergeReportTemplate.cs      # HTML report generation
```
