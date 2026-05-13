# PipePlan POC

This project is a proof-of-concept AutoCAD/Civil 3D plugin for constrained plan-view pipe drafting.

## What it does

- Adds the `PPDRAW` command
- Adds the `PPSETTINGS` command for the settings palette
- Adds the `PPEDIT` command for constrained editing
- Uses an explicit `Apply Settings` button in the palette before changes take effect
- Lets you enter a minimum bend radius for each size
- Lets you choose the active pipe size
- Lets you set a straight-snap tolerance
- Collects plan points and previews the route in:
  - green when the selected radius fits
  - red when one or more bends do not fit
- blue when Ctrl is actively snapping to the previous straight segment
- Bakes the result as a polyline with arc bulges on a size-specific layer
- Writes a lightweight pipeTag XData record with the size and radius
- Stores full pipeGeometryData inside the object's pipeData dictionary for later constrained edits

## Build

The project targets `net8.0-windows` and references the local AutoCAD 2025 assemblies directly.

Typical build command once the .NET 8 SDK is installed:

```powershell
dotnet build .\PipePlan.Plugin.csproj -c Release
```

## Load

Load the built DLL with `APPLOAD`, then run:

```text
PPDRAW
```

Use this to change the active size and radii:

```text
PPSETTINGS
```

After changing size, radii, or straight-snap tolerance in the palette, click `Apply Settings`.

Use this to edit an existing PipePlan polyline by moving control vertices or segment handles:

```text
PPEDIT
```

## Notes

- `PPEDIT` requires PipePlan polylines baked with the current metadata-enabled version of the plugin.
- Older proof-of-concept polylines need to be re-baked once before constrained editing is available.

