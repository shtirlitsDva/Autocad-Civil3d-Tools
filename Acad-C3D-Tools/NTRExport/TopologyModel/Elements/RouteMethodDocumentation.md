# Route Method Documentation

## Overview

The `Route` method is the core method in the NTR Export topology routing system. It is implemented by all `ElementBase` subclasses (`TPipe`, `TFitting` subclasses, etc.) to emit routed geometry and propagate elevation and slope information through the pipeline network.

## Method Signature

```csharp
public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
    RoutedGraph g, 
    Topology topo, 
    RouterContext ctx, 
    TPort entryPort, 
    double entryZ, 
    double entrySlope)
```

## Parameters

### `RoutedGraph g`
The graph that collects all routed members (pipes, bends, valves, etc.) and soil hints. Add all emitted geometry to `g.Members`.

### `Topology topo`
The topology model containing connectivity and flow role information. Use this to resolve flow roles and infer pipe properties.

### `RouterContext ctx`
Context containing routing parameters (e.g., `CushionReach` for soil hints).

### `TPort entryPort`
The port through which routing enters this element. Used to determine which port is the entry and which are exits.

### `double entryZ`
**CRITICAL**: The centerline elevation (in meters) at the entry port. This is the elevation at which the pipe centerline must be placed at the entry port. All geometry emitted from this element must respect this elevation.

**Important**: `entryZ` represents the **centerline elevation**, not the pipe top or bottom. For twin pipes, offsets are applied relative to this centerline.

### `double entrySlope`
The slope (rise/run, dimensionless) of the incoming pipe at the entry port. Positive values indicate upward slope in the direction of flow.

## Return Value

Returns a list of tuples `(TPort exitPort, double exitZ, double exitSlope)` for each exit port:

- **`exitPort`**: The port through which routing exits this element
- **`exitZ`**: The centerline elevation (in meters) at the exit port after passing through this element
- **`exitSlope`**: The slope (rise/run) at the exit port

**Critical**: The `exitZ` values returned become the `entryZ` values for connected downstream elements. This is how elevation propagates through the network.

## Common Patterns

### 1. Simple Propagation (No Geometry Change)

For elements that don't change geometry (e.g., simple pass-throughs):

```csharp
public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
    RoutedGraph g, Topology topo, RouterContext ctx, 
    TPort entryPort, double entryZ, double entrySlope)
{
    var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();
    foreach (var p in Ports)
    {
        if (ReferenceEquals(p, entryPort)) continue;
        exits.Add((p, entryZ, entrySlope)); // Propagate unchanged
    }
    return exits;
}
```

**Example**: `ElementBase.Route()` (base implementation)

### 2. Applying entryZ to Geometry

**CRITICAL RULE**: Always use `entryZ` as the base elevation. Never use `port.Node.Pos.Z` directly - it may not match the routed elevation.

#### Pattern A: Simple Constant Elevation

For elements with constant elevation (e.g., horizontal elbows):

```csharp
var a = portA.Node.Pos;
var b = portB.Node.Pos;
var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);

// Use port XY positions, but set Z to entryZ + offset
var bend = new RoutedBend(Source, this)
{
    A = a.Z(zUp + entryZ),  // Centerline at entryZ, offset by zUp
    B = b.Z(zUp + entryZ),
    T = tangentPoint.Z(zUp + entryZ),
    // ...
};
```

**Example**: `ElbowFormstykke.Route()` (lines 72-74)

#### Pattern B: Elevation Change Along Element

For elements that change elevation (e.g., pipes with slope):

```csharp
var other = ReferenceEquals(entryPort, A) ? B : A;
var length = Length; // Distance in XY plane

// Compute exit elevation from entryZ and slope
double exitZ = entryZ + entrySlope * length;

// Compute centerline elevations along the pipe
double ZAtParam(double t)
{
    var s = ReferenceEquals(entryPort, A) ? t * length : (1.0 - t) * length;
    return entryZ + entrySlope * s;
}

// Use computed Z values for geometry
var zCenterA = ZAtParam(0.0); // Should equal entryZ
var zCenterB = ZAtParam(1.0); // Should equal exitZ
var aCenter = new Point3d(aPos.X, aPos.Y, zCenterA);
var bCenter = new Point3d(bPos.X, bPos.Y, zCenterB);
```

**Example**: `TPipe.Route()` (lines 51-110)

### 3. Twin vs Bonded Pipes

#### Getting Twin Offsets

```csharp
var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
```

**Important**: 
- For **twin pipes** (`Variant.IsTwin == true`): Returns `(zUp, zLow)` where `zUp > 0` and `zLow < 0`
- For **bonded pipes** (`Variant.IsTwin == false`): Returns `(0.0, 0.0)`

**Example**: `ElementBase.ComputeTwinOffsets()` (lines 87-100)

#### Emitting Twin Pipe Geometry

```csharp
if (Variant.IsTwin)
{
    // Emit return pipe (upper)
    g.Members.Add(new RoutedStraight(Source, this)
    {
        A = aCenter.Off(upVec3, zUp),  // Offset upward from centerline
        B = bCenter.Off(upVec3, zUp),
        FlowRole = FlowRole.Return,
        // ...
    });
    
    // Emit supply pipe (lower)
    g.Members.Add(new RoutedStraight(Source, this)
    {
        A = aCenter.Off(upVec3, zLow), // Offset downward from centerline
        B = bCenter.Off(upVec3, zLow),
        FlowRole = FlowRole.Supply,
        // ...
    });
    
    // Emit rigid connections between pipes at ports
    g.Members.Add(new RoutedRigid(Source, this)
    {
        P1 = new Point3d(port.X, port.Y, entryZ + zLow),
        P2 = new Point3d(port.X, port.Y, entryZ + zUp),
        // ...
    });
}
else
{
    // Emit single bonded pipe at centerline
    g.Members.Add(new RoutedStraight(Source, this)
    {
        A = aCenter,  // Already at centerline (entryZ)
        B = bCenter,
        FlowRole = ResolveBondedFlowRole(topo),
        // ...
    });
}
```

**Example**: `TPipe.Route()` (lines 112-150)

### 4. Computing Exit Elevation

For elements that change elevation, compute `exitZ` from geometry:

```csharp
// Example: Vertical elbow with angle change
var alphaE = Math.Atan(entrySlope);
var theta = angleDeg * Math.PI / 180.0;
var alphaO = alphaE + signedTheta;
var exitSlope = Math.Tan(alphaO);

// Compute exit elevation from arc geometry
var deltaW = -R * (Math.Cos(alphaO) - Math.Cos(alphaE));
var exitZ = entryZ + deltaW;

exits.Add((otherPort, exitZ, exitSlope));
```

**Example**: `ElbowVertical.Route()` (non-twin case, lines 320-434)

### 5. Handling Degenerate Cases

Always provide fallback behavior for degenerate cases:

```csharp
List<(TPort exitPort, double exitZ, double exitSlope)> PropagateUnchanged()
{
    var fallback = new List<(TPort exitPort, double exitZ, double exitSlope)>();
    foreach (var p in Ports)
    {
        if (ReferenceEquals(p, entryPort)) continue;
        fallback.Add((p, entryZ, entrySlope)); // Propagate unchanged
    }
    return fallback;
}

// Use fallback when geometry computation fails
if (dirLen < 1e-9)
{
    return PropagateUnchanged();
}
```

**Example**: `ElbowVertical.Route()` (lines 64-73, 119-123)

## Examples by Class

### TPipe
- **Purpose**: Straight pipe segments
- **Key Pattern**: Computes `exitZ = entryZ + entrySlope * length`
- **Twin Handling**: Emits two `RoutedStraight` objects offset by `zUp`/`zLow`
- **Reference**: Lines 45-154

### ElbowFormstykke
- **Purpose**: Horizontal elbows (constant elevation)
- **Key Pattern**: Uses `a.Z(zUp + entryZ)` to set Z directly
- **Twin Handling**: Emits two `RoutedBend` objects, adds `RoutedRigid` at ports
- **Reference**: Lines 55-122

### ElbowVertical
- **Purpose**: Vertical elbows (changes elevation)
- **Key Pattern**: Computes arc geometry in local (u,w) coordinates, maps to world
- **Twin Handling**: Computes geometry for both pipes, applies offsets along bisector
- **Critical**: Must ensure centerline is at `entryZ` at entry port
- **Reference**: Lines 49-439

### Valve
- **Purpose**: Valves (pass-through, no elevation change)
- **Key Pattern**: Simple propagation, emits `RoutedValve` at `entryZ + offset`
- **Twin Handling**: Emits two `RoutedValve` objects
- **Reference**: Lines 29-95

### Reducer
- **Purpose**: Pipe diameter reduction
- **Key Pattern**: Uses different offsets for different diameters
- **Twin Handling**: Computes offsets for both diameters
- **Reference**: Lines 47-106

### TeeMainRun
- **Purpose**: Tee fittings with main run and branch
- **Key Pattern**: Computes different elevations for main and branch ports
- **Twin Handling**: Complex geometry with spring offsets
- **Reference**: Lines 174-811

### TeeFormstykke
- **Purpose**: Forged tee fittings (Svejsetee, PreskoblingTee, Muffetee)
- **Key Pattern**: 
  - **Bonded pipes**: Simple straight from main center to branch port at `entryZ` (handled in `Route` method)
  - **Twin with same DN**: Level pipes, no elevation change (handled in `Route` method)
  - **Twin with different DN**: Uses **post-processing** approach because branch leg is too short to accommodate bends
- **Post-Processing**: For twin + different DN cases:
  1. `Route` method only routes the main run and propagates exits
  2. After all elements are routed, `Router.PostProcessForgedTees()` finds connected branch `RoutedStraight` members
  3. Replaces branch straights with angled branch geometry (branch segment + bend + stub) similar to `AfgreningsStuds`
  4. Maintains connectivity by updating branch straight endpoints
- **Reference**: `TeeMainRun.cs` lines 144-220, `Router.cs` lines 140-369

## Common Pitfalls and Best Practices

### ❌ WRONG: Using Port Z Directly

```csharp
// DON'T DO THIS
var aWorld = aPort.Node.Pos; // Uses CAD model Z, not routed Z!
```

### ✅ CORRECT: Using entryZ

```csharp
// DO THIS
var aWorld = new Point3d(aPort.Node.Pos.X, aPort.Node.Pos.Y, entryZ);
// Or use extension method:
var aWorld = aPort.Node.Pos.Z(entryZ);
```

### ❌ WRONG: Forgetting to Propagate exitZ

```csharp
// DON'T DO THIS
exits.Add((otherPort, entryZ, entrySlope)); // Wrong if elevation changed!
```

### ✅ CORRECT: Computing and Propagating exitZ

```csharp
// DO THIS
var exitZ = ComputeExitZFromGeometry(); // Based on element geometry
exits.Add((otherPort, exitZ, exitSlope));
```

### ❌ WRONG: Applying Offsets Incorrectly

```csharp
// DON'T DO THIS (for bonded pipes)
var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
var point = port.Pos.Z(entryZ + zUp); // zUp is 0 for bonded, but confusing
```

### ✅ CORRECT: Understanding Offset Semantics

```csharp
// DO THIS
var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
if (Variant.IsTwin)
{
    // Apply offsets for twin pipes
    var returnPoint = port.Pos.Z(entryZ + zUp);
    var supplyPoint = port.Pos.Z(entryZ + zLow);
}
else
{
    // For bonded pipes, zUp == 0, so this is just entryZ
    var point = port.Pos.Z(entryZ + zUp); // Equivalent to entryZ
}
```

### Best Practices

1. **Always use `entryZ` as the base elevation** - Never trust `port.Node.Pos.Z`
2. **Compute `exitZ` from geometry** - Don't assume it equals `entryZ` unless the element doesn't change elevation
3. **Handle degenerate cases** - Provide fallback behavior when geometry computation fails
4. **Respect twin vs bonded distinction** - `ComputeTwinOffsets` returns `(0,0)` for bonded pipes
5. **Apply offsets relative to centerline** - Offsets are applied perpendicular to the centerline, not in world Z
6. **Emit all geometry to `g.Members`** - Don't forget to add routed objects to the graph
7. **Propagate to all exit ports** - Return tuples for every port except the entry port

## Extension Methods

Common extension methods used in Route implementations:

- `Point3d.Z(double z)` - Creates new Point3d with specified Z coordinate
- `Point3d.ModZ(double deltaZ)` - Creates new Point3d with Z offset by deltaZ
- `Point3d.To2d()` - Extracts XY coordinates as Point2d
- `Point2d.To3d(double Z)` - Creates Point3d from Point2d with specified Z

**Reference**: `UtilitiesCommonSHARED/UtilsCommon.cs` (lines 2892-2901)

## Post-Processing Elements

Some elements require post-processing because they need to interact with already-emitted geometry:

### TeeFormstykke (Forged Tees)

**When**: Twin pipes with different main and branch DN (`Variant.IsTwin == true && DnM != DnB`)

**Why**: The branch leg of forged tees is too short to accommodate the angled branch geometry with bends. Instead, the connecting pipes must be adjusted.

**How**:
1. During normal `Route()` traversal: Only route the main run (via `RouteMain()`), propagate exits normally
2. After all elements are routed: `Router.PostProcessForgedTees()` runs
3. For each TeeFormstykke with twin + different DN:
   - Build spatial index of `RoutedStraight` endpoints
   - Find branch straights connected to the tee's branch port (by matching XY coordinates)
   - Compute angled branch geometry using `Geometry.SolveBranchFillet()` (same as `AfgreningsStuds`)
   - Replace branch straight endpoints and insert new geometry (branch segment + bend + stub)
   - Maintain connectivity by updating branch straight to connect upstream to new branch start

**Implementation**: See `Router.PostProcessForgedTees()` and `Router.ProcessForgedTee()`

## Summary

The `Route` method is responsible for:
1. **Emitting geometry** - Creating `RoutedBend`, `RoutedStraight`, `RoutedValve`, etc. objects
2. **Respecting `entryZ`** - Ensuring centerline is at `entryZ` at entry port
3. **Computing `exitZ`** - Calculating elevation at exit ports from geometry
4. **Propagating information** - Returning `(exitPort, exitZ, exitSlope)` tuples for downstream routing

**Post-processing** may be required for elements that need to adjust already-emitted geometry (e.g., TeeFormstykke with twin + different DN).

Remember: `entryZ` is the **centerline elevation** at the entry port. All geometry must respect this constraint.

