# AutoProfile System Documentation

## Overview

The **AutoProfile** system automatically generates longitudinal pipe profiles for district heating pipelines in Civil 3D. The system creates a profile that:

1. **Maintains minimum cover depth** below the terrain surface (based on pipe dimensions)
2. **Avoids unavoidable utility crossings** by routing the pipe around obstacles
3. **Uses elastic bending (fillets)** for smooth direction changes that respect the minimum elastic bending radius of the steel pipe

---

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Main Commands](#main-commands)
3. [Processing Pipeline](#processing-pipeline)
4. [Data Classes](#data-classes)
5. [Filleting System](#filleting-system)
6. [Algorithm Details](#algorithm-details)
7. [File Structure](#file-structure)
8. [Known Limitations & Feedback](#known-limitations--feedback)

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              APCREATE Command                                │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Data Gathering Phase                               │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────┐  ┌───────────────────┐   │
│  │ Surface     │  │ Pipeline     │  │ Horizontal │  │ Utility           │   │
│  │ Profile     │  │ Size Array   │  │ Arcs       │  │ Obstacles         │   │
│  └─────────────┘  └──────────────┘  └────────────┘  └───────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Avoidance Geometry Generation                         │
│  • Generate avoidance arcs for each utility                                  │
│  • Generate horizontal arc avoidance polylines                               │
│  • Convert to NTS polygons                                                   │
│  • Merge avoidance polygons                                                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Utility Classification                               │
│  • Test floating status (is utility above or intersecting profile?)         │
│  • Iterative selection: deepest non-floating utilities are Selected         │
│  • Covered utilities are Ignored                                            │
│  • Overlapping floating utilities become non-floating                       │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      Unfilleted Polyline Generation                          │
│  • Create polygon from offset centreline                                     │
│  • Union with selected utility avoidance regions                             │
│  • Extract lower boundary polyline                                           │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          Filleting Phase                                     │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    AutoProfileFilleter                               │    │
│  │  • Extract segments from polyline                                    │    │
│  │  • Sanitize: prune short segments, linearize nearly-flat arcs       │    │
│  │  • For each non-tangent vertex:                                      │    │
│  │    - Select appropriate FilletStrategy                               │    │
│  │    - Attempt fillet at minimum elastic radius                        │    │
│  │    - On failure: use TriageStrategy to find alternative              │    │
│  │  • Build final polyline                                              │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            Validation Loop                                   │
│  • Check if any Ignored/Unknown utilities now intersect the filleted curve  │
│  • If yes: promote those utilities to Selected and re-process               │
│  • Repeat until no intersections                                            │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Output                                          │
│  • Final filleted polyline added to drawing on "AutoProfile" layer          │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Main Commands

### APCREATE
**Purpose:** Creates automatic pipe profiles for longitudinal sections.

**Workflow:**
1. User selects alignment(s) to process (or "All")
2. System gathers data from:
   - Surface profiles (`*_surface_P`)
   - FJV database (pipeline polylines)
   - Detailing blocks (`*_PV`)
3. Generates avoidance geometry for non-relocatable utilities
4. Classifies utilities (Selected, Ignored, Unknown, Floating)
5. Creates unfilleted polyline from merged regions
6. Fillets the polyline using minimum elastic bending radii
7. Outputs red polyline on "AutoProfile" layer

### APTEST (DEBUG only)
**Purpose:** Test filleting on a manually selected polyline.

### APEXPORTPLINE
**Purpose:** Export polyline segments to JSON for debugging.

### APDATAEXPORT
**Purpose:** Export all profile data (surface, pipeline sizes, utilities) to JSON files in `C:\Temp\AP\`.

---

## Processing Pipeline

### Phase 1: Data Gathering

For each alignment:

1. **Profile View** - Find the single profile view associated with the alignment
2. **Pipeline Size Array** - Build from FJV database entities, contains DN, system, type, and minimum radii per station
3. **Surface Profile** - Extract from Civil 3D profile ending with `*_surface_P`
4. **Horizontal Arcs** - Extract arc segments from pipeline polylines (affects vertical radius constraints)
5. **Utilities** - Extract from detailing block entities:
   - Filter out relocatable utilities (`CanBeRelocated` property)
   - Create bounding boxes using NetTopologySuite
   - Union overlapping boxes

### Phase 2: Offset Centreline Generation

The surface profile is offset downward by the required cover depth:

```
Cover Depth = GetCoverDepth(DN, System, Type) + (Kod / 1000 / 2)
```

Where:
- `Kod` = Outer diameter of the casing/jacket (mm)
- Cover depth is measured to the pipe centreline

The surface polyline is:
1. Simplified using Douglas-Peuker algorithm (tolerance: 0.1)
2. Split at size change stations
3. Each segment offset by its respective cover depth
4. Segments merged back into single polyline

### Phase 3: Avoidance Geometry

For each utility obstacle:

1. **Avoidance Arc** - Circular arc centered below the utility, with radius = minimum vertical elastic radius at that station
   - Arc extends upward and is trimmed at surface elevation + 2.0m
   - Arc is rotated to match the surface slope

2. **Horizontal Arc Avoidance** (if utility falls within a horizontal arc section)
   - Additional avoidance polyline with arcs at start and end stations of the horizontal arc
   - Accounts for reduced vertical bendability in horizontal arc sections

3. **Polygon Conversion** - Avoidance arcs converted to NTS polygons with curve approximation

4. **Merging** - Normal avoidance polygon merged with horizontal arc avoidance polygon

### Phase 4: Utility Classification

Utilities are classified iteratively using a deepest-first algorithm:

```
Status Values:
  - Unknown  : Not yet processed
  - Selected : Must be avoided (pipe goes around)
  - Ignored  : Covered by another utility's avoidance (redundant)
  - Floating : Above the profile, can be ignored
```

**Algorithm:**
1. Test each utility's "floating" status against the offset centreline
2. While there are Unknown non-floating utilities:
   - Pick the deepest (lowest elevation) Unknown non-floating utility
   - If it overlaps a Floating utility → mark that Floating utility as non-floating (restart)
   - If it covers other Unknown utilities → mark them as Ignored
   - Mark current utility as Selected

### Phase 5: Unfilleted Polyline Generation

1. Create closed polygon from offset centreline (with vertical extensions to +20m at ends)
2. Create Region from the polygon
3. Handle vertical segments (size changes) by adding small arc regions at vertices
4. Union with all Selected utility avoidance regions
5. Trim left and right sides to profile view extents
6. Explode the region to lines and arcs
7. Filter to only entities within profile view bounds
8. Sort by X coordinate and construct the "lower boundary" polyline

### Phase 6: Filleting

See [Filleting System](#filleting-system) below.

### Phase 7: Validation Loop

After filleting:
1. Check if any Ignored/Unknown utilities now intersect the filleted polyline
2. If yes: promote those utilities to Selected
3. Re-run ProcessSelectedUtilities and FilletPolyline
4. Repeat until no new intersections

---

## Data Classes

### Core Data Container

#### `AP_PipelineData`
The main container for all data related to a single pipeline/alignment.

```csharp
class AP_PipelineData
{
    string Name;                              // Alignment name
    AP_SurfaceProfileData? SurfaceProfile;    // Terrain profile data
    IPipelineSizeArrayV2? SizeArray;          // Pipe dimensions along station
    List<AP_HorizontalArc> HorizontalArcs;    // Horizontal bends in plan view
    AP_ProfileViewData? ProfileView;          // Civil 3D profile view reference
    List<AP_Utility> Utility;                 // Utility obstacles
    Polyline? UnfilletedPolyline;            // Raw profile before filleting
    Polyline? FilletedPolyline;              // Final smoothed profile
}
```

**Key Methods:**
- `GenerateAvoidanceGeometryForUtilities()` - Creates avoidance arcs for all utilities
- `GenerateAvoidancePolygonsForUtilities()` - Converts arcs to NTS polygons
- `MergeAvoidancePolygonsForUtilities()` - Combines normal + horizontal arc avoidance
- `ProcessSelectedUtilitiesToCreateUnfilletedPolyline()` - Boolean operations to create raw profile
- `FilletPolyline()` - Smooth all vertices with elastic bending arcs

### Supporting Data Classes

#### `AP_SurfaceProfileData`
Contains terrain surface data and offset centrelines.

```csharp
class AP_SurfaceProfileData : PipelineDataBase
{
    string Name;
    Profile? Profile;                        // Civil 3D Profile object
    Polyline? SurfacePolylineFull;          // Full resolution surface
    Polyline? SurfacePolylineSimplified;    // Douglas-Peuker reduced
    Polyline? SurfacePolylineWithHangingEnds; // With vertical extensions
    Polyline? OffsetCentrelines;            // Surface offset by cover depth
}
```

#### `AP_Utility`
Represents a utility obstacle that may need to be avoided.

```csharp
class AP_Utility : PipelineDataBase
{
    Extents2d Extents;                       // Bounding box in profile view
    Polygon UtilityPolygon;                  // NTS polygon of utility bounds
    Polygon? MergedAvoidancePolygon;         // Combined avoidance region
    bool IsFloating;                         // Is above the profile?
    double MidStation, StartStation, EndStation;
    double TopElevation, BottomElevation;
    AP_Status Status;                        // Unknown, Selected, Ignored
    
    // Avoidance Geometry
    Polyline? AvoidanceArc;                  // Arc-shaped avoidance polyline
    Polygon? AvoidancePolygon;               // NTS version
    Region? AvoidanceRegion;                 // AutoCAD region
    Polyline? HorizontalArcAvoidancePolyline;
    Polygon? HorizontalArcAvoidancePolygon;
    Region? HorizontalArcAvoidanceRegion;
}
```

#### `AP_HorizontalArc`
Represents a horizontal curve section in the pipeline plan view.

```csharp
class AP_HorizontalArc : PipelineDataBase
{
    double StartStation;
    double EndStation;
}
```

#### `AP_ProfileViewData`
Reference to the Civil 3D ProfileView object.

```csharp
class AP_ProfileViewData : PipelineDataBase
{
    string Name;
    ProfileView ProfileView;
}
```

### Enums

#### `AP_Status`
```csharp
enum AP_Status
{
    Unknown,   // Not yet classified
    Selected,  // Must be avoided
    Ignored    // Covered by another, can skip
}
```

#### `Relation`
```csharp
enum Relation
{
    Unknown,
    Inside,    // Utility completely inside avoidance region
    Outside,   // Utility completely outside
    Overlaps   // Partial overlap
}
```

---

## Filleting System

The filleting system uses the **Strategy Pattern** to handle different segment type combinations.

### Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         AutoProfileFilleter                                  │
│  Orchestrates the entire filleting process                                   │
│  Dependencies:                                                               │
│    - IFilletRadiusProvider (provides radius based on position)              │
│    - ISegmentExtractor (converts Polyline → LinkedList<IPolylineSegment>)   │
│    - IPolylineBuilder (converts LinkedList<IPolylineSegment> → Polyline)    │
│    - FilletStrategyManager (selects appropriate fillet strategy)            │
│    - TriageStrategyManager (handles fillet failures)                        │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Interfaces

#### `IPolylineSegment`
```csharp
interface IPolylineSegment
{
    SegmentType SegmentType { get; }
    Point2d StartPoint { get; }
    Point2d EndPoint { get; }
    double Length { get; }
    Curve2d GetGeometry2d();
    Vector2d GetStartTangent();
    Vector2d GetEndTangent();
}
```

Implementations:
- `PolylineLineSegment` - wraps `LineSegment2d`
- `PolylineArcSegment` - wraps `CircularArc2d`

#### `IFilletStrategy`
```csharp
interface IFilletStrategy
{
    bool CanHandle(IPolylineSegment segment1, IPolylineSegment segment2);
    IFilletResult CreateFillet(IPolylineSegment segment1, IPolylineSegment segment2, double radius);
}
```

#### `IFilletResult`
```csharp
interface IFilletResult
{
    bool Success { get; }
    FilletFailureReason FailureReason { get; }
    string? ErrorMessage { get; }
    void UpdateWithResults(LinkedList<IPolylineSegment> segments);
}
```

#### `ITriageStrategy`
```csharp
interface ITriageStrategy
{
    bool CanHandle(IFilletStrategy filletStrategy, FilletFailureReason reason);
    IFilletResult Triage(
        (LinkedListNode<IPolylineSegment> firstNode,
         LinkedListNode<IPolylineSegment> secondNode) nodes,
        double radius);
}
```

### Fillet Strategies

| Strategy | First Segment | Second Segment | Description |
|----------|--------------|----------------|-------------|
| `FilletStrategyLineToLine` | Line | Line | Classic line-line fillet |
| `FilletStrategyLineToArc` | Line | Arc | Line tangent to existing arc |
| `FilletStrategyArcToLine` | Arc | Line | Arc tangent to line |
| `FilletStrategyArcToArc` | Arc | Arc | Arc tangent to two arcs |

### Failure Reasons

```csharp
enum FilletFailureReason
{
    None,
    Seg1TooShort,          // First segment too short for fillet
    Seg2TooShort,          // Second segment too short for fillet
    BothSegsTooShort,      // Both segments too short
    SegmentsAreTangential, // Already smooth, no fillet needed
    UnsupportedSegmentTypes,
    CalculationError,
    InvalidRadius,
    RadiusTooLarge         // Radius exceeds available space
}
```

### Triage Strategies

When a fillet fails, triage strategies attempt recovery:

| Strategy | Handles | Action |
|----------|---------|--------|
| `TriageStrategyArcLineS2Short` | Arc→Line where Seg2 too short | Look forward for longer line or another arc |
| `TriageStrategyLineArcS1Short` | Line→Arc where Seg1 too short | Look backward for longer line or another arc |

**Triage Logic:**
1. If the adjacent segment is too short, look at the *next* segment
2. Skip over "upward-bulge" arcs (they don't interfere with downward routing)
3. Skip over nearly-linear arcs (sagitta < 0.05)
4. If we find a suitable line or downward arc, attempt fillet with that segment

### Filleting Algorithm

```
1. Extract segments from polyline → LinkedList<IPolylineSegment>
2. Sanitize:
   - Prune segments shorter than 0.01
   - Convert nearly-flat arcs (sagitta < 0.005) to lines
3. While there's an unfilleted vertex:
   a. Find next non-tangent consecutive pair (skip if already smooth)
   b. Get fillet strategy for segment types
   c. Get radius from RadiusProvider at vertex location
   d. Attempt fillet
   e. If success: replace original segments with trimmed + fillet arc
   f. If failure:
      - Get triage strategy
      - Attempt triage (look for alternative segment)
      - If triage success: update segments
      - If triage fails: skip this vertex (add to skipped set)
4. Build final polyline from segments
```

### Math Utilities (`FilletMath`)

Key functions:
- `TryConstructFillet(v, d1, d2, r, out t1, out t2, out arc)` - Line-line fillet
- `TryCircleCircleTangent(c1, r1, c2, r2, filletR, out cen1, out cen2)` - Arc-arc fillet centres
- `TryLineArcTangent(ln, arc, filletR, out centre, out tOnArc, out tOnLine)` - Line-arc fillet
- `TrimArcToPoint(arc, p, trimEnd)` - Shorten arc to given point
- `IsArcBulgeUpwards(arc)` - Check if arc curves upward (can be skipped in triage)
- `IsArcAlmostLinear(arc, maxSagitta)` - Check if arc is nearly flat

---

## Algorithm Details

### Offset Centreline Calculation

For each size segment in the pipeline:

```
Offset = CoverDepth(DN, System, Type) + (Kod / 2000)
```

Where:
- **CoverDepth** is looked up from pipe schedule based on DN, System (Stål/Twin), and Type
- **Kod** is the outer jacket diameter in mm

### Avoidance Arc Geometry

For a utility with bottom-left `BL` and bottom-right `BR` points:

1. Find midpoint M of BL-BR
2. Calculate centre height: `h = √(R² - (halfDist)²)` where `halfDist = |BL-BR| / 2`
3. Arc centre = `(M.X, M.Y + h)`
4. Arc spans from angle at BL to angle at BR
5. Arc is trimmed at `surfaceElevation + 2.0m` above the utility
6. Arc is rotated to match the surface slope at that station

### Utility Selection Algorithm

The algorithm processes utilities from deepest (lowest elevation) to shallowest:

```python
while unknowns_exist:
    current = deepest_unknown_nonfloating()
    if current is None:
        break
    
    # Case 1: Check if we overlap any floating utilities
    for floating in overlapping_floatings(current):
        floating.IsFloating = False  # Demote to non-floating
        continue  # Restart the loop
    
    # Case 2: Mark covered utilities as Ignored
    for covered in covered_by_current(current):
        covered.Status = Ignored
    
    # Case 3: Current is now Selected
    current.Status = Selected
```

---

## File Structure

```
AutoProfile/
├── Auto Profile.cs                    # Main commands (APCREATE, etc.)
├── PolylineExportSegment.cs          # JSON export DTOs
├── AutoProfileJsonConverters.cs      # JSON serialization helpers
├── acad_classes.txt                  # AutoCAD API reference
│
├── Docs/
│   ├── feedback.md                   # User feedback from testing
│   └── AutoProfile_Documentation.md  # This file
│
├── PipelineDataClasses/
│   ├── PipelineDataBase.cs           # Abstract base class
│   ├── AP_PipelineData.cs            # Main data container
│   ├── AP_SurfaceProfileData.cs      # Surface/terrain data
│   ├── AP_ProfileViewData.cs         # Civil 3D profile view
│   ├── AP_HorizontalArc.cs           # Horizontal arc sections
│   ├── AP_Utility.cs                 # Utility obstacles
│   └── Enums/
│       ├── AP_Status.cs              # Unknown/Selected/Ignored
│       └── AP_Relation.cs            # Inside/Outside/Overlaps
│
└── AutoProfileClasses/
    ├── AutoProfileFilleter.cs        # Main filleting orchestrator
    │
    ├── Interfaces/
    │   ├── IPolylineSegment.cs       # Segment abstraction
    │   ├── IFilletStrategy.cs        # Fillet strategy interface
    │   ├── IFilletResult.cs          # Fillet result interface
    │   ├── IFilletRadiusProvider.cs  # Radius lookup interface
    │   ├── IPolylineBuilder.cs       # Polyline construction
    │   ├── ISegmentExtractor.cs      # Polyline → segments
    │   └── ITriageStrategy.cs        # Failure recovery interface
    │
    ├── SegmentImplementations/
    │   ├── PolylineLineSegment.cs    # Line segment wrapper
    │   └── PolylineArcSegment.cs     # Arc segment wrapper
    │
    ├── Services/
    │   ├── SegmentExtractor.cs       # Extracts segments from polyline
    │   ├── PolylineBuilder.cs        # Builds polyline from segments
    │   ├── RadiusProvider.cs         # Callback-based radius lookup
    │   └── PolylineSanitizer.cs      # Prune/linearize segments
    │
    ├── FilletStrategies/
    │   ├── FilletMath.cs             # Core geometry calculations
    │   ├── FilletStrategyManager.cs  # Strategy selection
    │   ├── FilletStrategyLineToLine.cs
    │   ├── FilletStrategyLineToArc.cs
    │   ├── FilletStrategyArcToLine.cs
    │   └── FilletStrategyArcToArc.cs
    │
    ├── ExecutionControl/
    │   ├── FilletFailureReason.cs    # Failure enum
    │   ├── FilletResultBase.cs       # Abstract result
    │   ├── FilletResultThreePart.cs  # Trimmed1 + Fillet + Trimmed2
    │   ├── FilletValidation.cs       # Leg room checks
    │   └── VertexKey.cs              # Vertex identity for skipping
    │
    ├── TriageStrategies/
    │   ├── TriageStrategyManager.cs  # Triage selection
    │   ├── TriageStrategyArcLineS2Short.cs  # Arc→Line, Seg2 short
    │   └── TriageStrategyLineArcS1Short.cs  # Line→Arc, Seg1 short
    │
    └── Extensions/
        ├── LinkedListExtensions.cs   # TryGetFilletCandidate
        ├── PolylineSegmentExtensions.cs  # IsTangentialTo
        └── Vector2dExtensions.cs     # CrossProduct
```

---

## Known Limitations & Feedback

Based on user feedback (Henrik Hein, June 2025):

### Issues Identified

1. **Radius Precision** - Elastic bend radii are slightly off from the documented minimum values

2. **Component Leg Length** - The system doesn't account for pre-bent component leg lengths (e.g., 90° pre-bends)

3. **Cover Depth Violations** - In some cases, the profile may not maintain the required 60cm cover depth

4. **F-rør Components** - Pre-insulated components (F-rør) not always respected as obstacles

5. **Horizontal + Vertical Arc Overlap** - When a vertical elastic bend is placed over a horizontal elastic bend:
   - Currently works if horizontal bend is larger than minimum
   - May cause issues at minimum horizontal bend radii

6. **Pre-insulated Components in Arcs** - System may place elastic bends over pre-insulated components

7. **Spurious Vertices** - Sometimes inserts vertex points to avoid combining horizontal + vertical bends

8. **Branch Stubs + Size Changes** - Vertical bends placed directly at branch stubs or material changes

9. **Large Terrain Jumps** - Cannot handle profiles that pass under highways/bridges with large elevation changes

### Potential Improvements

- [ ] Account for component leg lengths in avoidance geometry
- [ ] Stricter cover depth validation
- [ ] Better handling of pre-insulated component regions
- [ ] Smarter radius selection considering horizontal bend presence
- [ ] Improved handling of size change transitions
- [ ] Better terrain discontinuity detection

---

## Dependencies

- **Autodesk.AutoCAD.DatabaseServices** - AutoCAD drawing objects
- **Autodesk.Civil.DatabaseServices** - Civil 3D profiles, alignments, profile views
- **NetTopologySuite** - Geometry operations (union, intersection, polygon creation)
- **IntersectUtilities.PipelineNetworkSystem** - Pipeline network and size arrays
- **IntersectUtilities.PipeScheduleV2** - Pipe specifications and cover depths
- **IntersectUtilities.NTS** - NTS ↔ AutoCAD geometry conversion




