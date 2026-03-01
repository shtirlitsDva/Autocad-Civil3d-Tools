<nsload-migration>
NSLOAD Migration: NoCommands + AssemblyResolve Removal
</nsload-migration>

<overview>
NSLOAD now uses `IsolatedPluginContext` (stream-based assembly loading) + `CommandRegistrar` for all plugin loading.
Loaded plugins are invisible to AutoCAD's `ExtensionLoader` — commands are registered dynamically by NSLOAD.

Two changes per plugin project:
1. **Add `[assembly: CommandClass(typeof(Namespace.NoCommands))]`** — prevents AutoCAD from scanning the assembly for `[CommandMethod]` attributes (avoids duplicate registration).
2. **Remove `AssemblyResolve` handlers** — no longer needed since `IsolatedPluginContext` uses `AssemblyDependencyResolver` which reads `.deps.json`.
</overview>

<csv-plugins>
12 plugins in Register-2025.csv. 10 in-repo, 2 external (NSGIS, SheetSetManager — no changes needed).
</csv-plugins>

<per-project-changes>

<project name="IntersectUtilities" dll="IntersectUtilities.dll">
<add-nocommands>
- File: `Intersect.cs`
- Change: `[assembly: CommandClass(typeof(IntersectUtilities.Intersect))]` → `[assembly: CommandClass(typeof(IntersectUtilities.NoCommands))]`
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- DELETE: `DebugHelpers/AssemblyResolveEvents.cs`
- Remove: `#if DEBUG` block in `Intersect.cs` Initialize (lines 76-79)
</remove-assemblyresolve>
</project>

<project name="AcadOverrules" dll="AcadOverrules.dll">
<add-nocommands>
- File: `AcadOverrules.cs`
- Add: `[assembly: CommandClass(typeof(AcadOverrules.NoCommands))]` before namespace
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- Remove: `#if DEBUG` block in `AcadOverrules.cs` Initialize (lines 19-22)
- Handler was in IntersectUtilities (already deleted)
</remove-assemblyresolve>
</project>

<project name="Dimensionering" dll="Dimensionering.dll">
<add-nocommands>
- File: `Dimensionering.cs`
- Add: `[assembly: CommandClass(typeof(IntersectUtilities.Dimensionering.NoCommands))]` before namespace
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- DELETE: `DebugHelper.cs`
- Remove: `#if DEBUG` block in `Dimensionering.cs` Initialize (lines 94-97)
</remove-assemblyresolve>
</project>

<project name="DimensioneringV2" dll="DimensioneringV2.dll">
<add-nocommands>
- File: `Commands.cs`
- Change: `[assembly: CommandClass(typeof(DimensioneringV2.Commands))]` → `[assembly: CommandClass(typeof(DimensioneringV2.NoCommands))]`
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- DELETE: `DebugHelper.cs`
- Remove: `#if DEBUG` block in `Commands.cs` Initialize (lines 86-89)
</remove-assemblyresolve>
</project>

<project name="ExportShapeFiles" dll="ExportShapeFiles.dll">
<add-nocommands>
- File: `ExportShapeFiles.cs`
- Add: `[assembly: CommandClass(typeof(ExportShapeFiles.NoCommands))]` before namespace
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- Remove: `#if DEBUG` block in `ExportShapeFiles.cs` Initialize (line 42-44)
- Remove: `#if DEBUG` block in `ExportShapeFiles.cs` Terminate (line 49-51)
- Handler was in IntersectUtilities (already deleted)
</remove-assemblyresolve>
</project>

<project name="LERImporter" dll="LERImporter.dll">
<add-nocommands>
- File: `LerImporter.cs`
- Add: `[assembly: CommandClass(typeof(LERImporter.NoCommands))]` before namespace
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- None (no AssemblyResolve handler)
</remove-assemblyresolve>
</project>

<project name="Ler2PolygonSplitting" dll="Ler2PolygonSplitting.dll">
<add-nocommands>
- File: `Ler2PolygonSplitting.cs`
- Add: `[assembly: CommandClass(typeof(Ler2PolygonSplitting.NoCommands))]` before namespace
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- Remove: `#if DEBUG` block in `Ler2PolygonSplitting.cs` Initialize (lines 40-43)
- Remove: local `Debug_AssemblyResolve` method (lines 634-671)
</remove-assemblyresolve>
</project>

<project name="NSTBL" dll="NSTBL.dll">
<add-nocommands>
- File: `NSTBL.cs`
- Add: `[assembly: CommandClass(typeof(IntersectUtilities.NSTBL.NoCommands))]` before namespace
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- None (no AssemblyResolve handler)
</remove-assemblyresolve>
</project>

<project name="SheetCreationAutomation" dll="SheetCreationAutomation.dll">
<add-nocommands>
- File: `01 SheetCreationAutomation.cs`
- Add: `[assembly: CommandClass(typeof(SheetCreationAutomation.NoCommands))]` before namespace
- Add: `public class NoCommands { }` inside namespace
</add-nocommands>
<remove-assemblyresolve>
- DELETE: `Debug/DebugHelper.cs`
- Remove: `#if DEBUG` block in `01 SheetCreationAutomation.cs` Initialize (lines 51-54)
</remove-assemblyresolve>
</project>

<project name="NSPaletteSet" dll="NSPalette.dll">
<add-nocommands>
- File: `NSPalettePlugin.cs`
- Remove: IPlugin/IPluginPalette interfaces, `using DevReload;`
- Add: IExtensionApplication with Initialize/Terminate
- Add: `[assembly: CommandClass(typeof(NSPaletteSet.NoCommands))]` before namespace
- Add: `public class NoCommands { }` inside namespace
- Remove: DevReload.Interface project reference from NSPalette.csproj
</add-nocommands>
<remove-assemblyresolve>
- None (no AssemblyResolve handler)
</remove-assemblyresolve>
</project>

</per-project-changes>

<non-csv-assemblyresolve-removal>
Projects not in CSV that also had AssemblyResolve handlers:

<project name="IsoTools">
- DELETE: `DebugHelpers/AssemblyResolveEvents.cs`
- Remove: `#if DEBUG` block in `IsoTools.cs` Initialize (lines 27-29)
</project>

<project name="NTRExport">
- DELETE: `DebugHelper.cs`
- Remove: `#if DEBUG` block in `Commands.cs` Initialize (lines 43-46)
</project>

<project name="Net-Reload">
- **SKIP** — left as-is per decision
</project>

</non-csv-assemblyresolve-removal>
