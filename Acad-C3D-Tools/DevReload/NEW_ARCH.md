<section>
<overview>

# New Architecture: NSLOAD as Universal Loader

## Summary

NSLOAD becomes the single universal loader for ALL AutoCAD plugins. It handles two categories:

| Category | Mechanism | Example |
|----------|-----------|---------|
| **PaletteSet plugins** | ALC isolation via `PluginHost<IPlugin>` | GeoSearch (NSGIS), NSPalette |
| **Command-heavy plugins** | Old CSV + `Assembly.LoadFrom()` | IntersectUtilities (267 commands) |

PaletteSet plugins get dedicated `[CommandMethod]` entries in NSLOAD (e.g., `NSGIS`, `NSGISDEV`, `NSGISUNLOAD`). Command-heavy plugins keep the existing `NSLOAD` command with CSV-driven selection dialog.

**Key benefit**: Each PaletteSet plugin is just ONE project (the Core). No per-plugin Loader projects.

</overview>
</section>

<section>
<overview>

## Architecture Diagram

```
acad2025.lsp (AutoCAD startup)
    │
    ▼
NSLOAD.dll  ◄── auto-loaded via netload
    │
    ├── [CommandMethod("NSLOAD")]     ← CSV dialog for command-heavy plugins
    │       │
    │       ▼
    │   Register-2025.csv → Assembly.LoadFrom()
    │       (IntersectUtilities, ExportShapeFiles, etc.)
    │
    ├── [CommandMethod("NSGIS")]      ← ALC load from Isolated/ (release)
    │       │
    │       ▼
    │   PluginHost<IPlugin>.Load("GeoSearch.Core.dll", "DevReload.Interface")
    │       → IPlugin.Initialize() → IPlugin.CreatePaletteSet() → PaletteSet.Visible
    │
    ├── [CommandMethod("NSGISDEV")]   ← DevReload: unload → VS build → reload
    │       │
    │       ▼
    │   DevReloadService.FindAndBuild("GeoSearch", ed)
    │       → Unload ALC → Find VS → Build Debug → Load fresh DLL
    │
    └── [CommandMethod("NSGISUNLOAD")] ← Unload ALC, free DLLs
```

</overview>
</section>

<section>
<overview>

## Project Structure After Migration

```
Acad-C3D-Tools/                              ← shared infrastructure repo
    DevReload/                               ← shared project (.shproj)
        IsolatedPluginContext.cs              ← generic collectible ALC
        PluginHost.cs                        ← generic plugin lifecycle
        VsInstanceFinder.cs                  ← P/Invoke ROT enumeration
        DevReloadService.cs                  ← VS build orchestration
    DevReload.Interface/                     ← regular project (.csproj) → single DLL
        IPlugin.cs                           ← universal plugin interface
    FormsSHARED/                             ← shared project (.shproj)
        StringGridFormCaller.cs              ← UI for multi-instance selection
    NSLOAD/                                  ← the universal loader
        NsLoad.cs                            ← NSLOAD command + per-plugin commands
        NSLOAD.csproj                        ← refs: DevReload, DevReload.Interface, FormsSHARED

Plugin repos (e.g., Webservices/):
    GeoSearch/                               ← Core project (the ONLY project needed)
        GeoSearch.csproj                     ← EnableDynamicLoading, outputs to deploy folder
        GeoSearchPlugin.cs                   ← implements IPlugin
        Services/, Views/, etc.              ← all plugin code + NuGet deps
```

**What's gone**: No `GeoSearch.Loader/`, no `GeoSearch.Interface/`. Each plugin is a single project.

</overview>
</section>

<section>
<overview>

## NSLOAD Changes

### NSLOAD.csproj additions

```xml
<!-- DevReload shared project (ALC, PluginHost, VS Interop) -->
<Import Project="..\DevReload\DevReload.projitems" Label="Shared" />

<!-- DevReload.Interface (IPlugin) - regular project ref -->
<ProjectReference Include="..\DevReload.Interface\DevReload.Interface.csproj" />

<!-- Required for DevReload's VS Interop -->
<PackageReference Include="Microsoft.VisualStudio.Interop" Version="17.10.40170" />

<!-- AdWindows needed for PaletteSet -->
<Reference Include="AdWindows">
    <HintPath>C:\Program Files\Autodesk\AutoCAD 2025\AdWindows.dll</HintPath>
    <Private>False</Private>
</Reference>
```

### NsLoad.cs structure

```csharp
using DevReload;

[assembly: CommandClass(typeof(DRILOAD.NsLoad))]
[assembly: ExtensionApplication(typeof(DRILOAD.NsLoad))]

namespace DRILOAD
{
    public class NsLoad : IExtensionApplication
    {
        // ── Plugin registry ──────────────────────────────────
        // Each PaletteSet plugin gets an entry here
        private static readonly Dictionary<string, PluginHost<IPlugin>> _plugins = new();
        private static readonly Dictionary<string, PaletteSet?> _palettes = new();

        // ── Existing NSLOAD command (unchanged) ──────────────
        [CommandMethod("NSLOAD")]
        public static void nsload()
        {
            // Reads CSV, shows StringGridForm, Assembly.LoadFrom()
            // ... existing code unchanged ...
        }

        // ══════════════════════════════════════════════════════
        // ── NSGIS (GeoSearch) ────────────────────────────────
        // ══════════════════════════════════════════════════════

        [CommandMethod("NSGIS")]
        public static void LoadNsgis()
        {
            string loaderDir = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location)!;
            string pluginPath = Path.Combine(
                loaderDir, "NSGIS", "Isolated", "GeoSearch.Core.dll");
            LoadPalettePlugin("NSGIS", pluginPath);
        }

        [CommandMethod("NSGISDEV")]
        public static void DevReloadNsgis()
        {
            DevReloadPalettePlugin("NSGIS", "GeoSearch");
        }

        [CommandMethod("NSGISUNLOAD")]
        public static void UnloadNsgis()
        {
            UnloadPalettePlugin("NSGIS");
        }

        // ══════════════════════════════════════════════════════
        // ── Next plugin: copy the 3 methods above ────────────
        // ══════════════════════════════════════════════════════

        // [CommandMethod("NSPALETTE")]
        // [CommandMethod("NSPALETTEDEV")]
        // [CommandMethod("NSPALETTEUNLOAD")]

        // ══════════════════════════════════════════════════════
        // ── Shared helpers ───────────────────────────────────
        // ══════════════════════════════════════════════════════

        private static void LoadPalettePlugin(string key, string dllPath)
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                ClosePalette(key);
                if (!_plugins.ContainsKey(key))
                    _plugins[key] = new PluginHost<IPlugin>();

                var host = _plugins[key];
                if (host.IsLoaded) host.Unload();

                var plugin = host.Load(dllPath, "DevReload.Interface");
                plugin.Initialize();

                var ps = (PaletteSet)plugin.CreatePaletteSet();
                ps.Visible = true;
                ps.Size = new System.Drawing.Size(400, 400);
                ps.Dock = DockSides.Right;
                _palettes[key] = ps;

                ed?.WriteMessage($"\n{key} loaded successfully.");
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\nError loading {key}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void DevReloadPalettePlugin(string key, string vsProjectName)
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                ClosePalette(key);
                if (_plugins.TryGetValue(key, out var host) && host.IsLoaded)
                    host.Unload();

                string? dllPath = DevReloadService.FindAndBuild(vsProjectName, ed);
                if (dllPath == null) return;

                LoadPalettePlugin(key, dllPath);
                ed?.WriteMessage($"\n{key} dev-reloaded successfully.");
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\nError dev-reloading {key}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void UnloadPalettePlugin(string key)
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ClosePalette(key);
            if (_plugins.TryGetValue(key, out var host) && host.IsLoaded)
            {
                host.Unload();
                ed?.WriteMessage($"\n{key} unloaded.");
            }
            else
            {
                ed?.WriteMessage($"\n{key} is not loaded.");
            }
        }

        private static void ClosePalette(string key)
        {
            if (_palettes.TryGetValue(key, out var ps) && ps != null)
            {
                ps.Close();
                _palettes[key] = null;
            }
        }
    }
}
```

### Adding a new PaletteSet plugin to NSLOAD

Copy-paste 3 methods per plugin (7 lines each):

```csharp
[CommandMethod("MYPLUGIN")]
public static void LoadMyPlugin()
{
    string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
    LoadPalettePlugin("MYPLUGIN", Path.Combine(dir, "MYPLUGIN", "Isolated", "MyPlugin.Core.dll"));
}

[CommandMethod("MYPLUGINDEV")]
public static void DevReloadMyPlugin()
    => DevReloadPalettePlugin("MYPLUGIN", "MyPlugin");

[CommandMethod("MYPLUGINUNLOAD")]
public static void UnloadMyPlugin()
    => UnloadPalettePlugin("MYPLUGIN");
```

</overview>
</section>

<section>
<overview>

## Deployment Structure

### NSLOAD publish output

```
X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\2025\NSLOAD\
    NSLOAD.dll                     ← auto-loaded at startup
    DevReload.Interface.dll        ← shared IPlugin type (tiny, ~3KB)
    NSGIS\                         ← per-plugin subfolder
        Isolated\
            GeoSearch.Core.dll     ← plugin code
            GeoSearch.Core.deps.json
            GeoSearch.Core.runtimeconfig.json
            *.dll                  ← NuGet dependencies (isolated)
    NSPALETTE\                     ← next plugin
        Isolated\
            NSPalette.Core.dll
            ...
```

### Why subfolder per plugin?

Each PaletteSet plugin gets its own subfolder under NSLOAD's deploy directory. This keeps each plugin's `Isolated/` folder separate, preventing DLL name collisions between plugins.

### CSV Register stays for command-heavy plugins

```
Register-2025.csv:
DisplayName;Path
INTERSECTUTIL;X:\...\2025\IntersectUtilities\IntersectUtilities.dll
EXPORTSHAPEFILES;X:\...\2025\ExportShapeFiles\ExportShapeFiles.dll
...
```

PaletteSet plugins (NSGIS, NSPALETTE, etc.) are **removed from the CSV** — they have dedicated commands now.

</overview>
</section>

<section>
<overview>

## Plugin Migration Guide

### What you need: 1 project (the Core)

Your existing project IS the Core. No Loader project, no Interface project.

### Step 1: Modify your .csproj

```xml
<PropertyGroup>
    <!-- Rename assembly to include .Core suffix -->
    <AssemblyName>YourPlugin.Core</AssemblyName>
    <!-- Required for .deps.json generation (ALC dependency resolution) -->
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <!-- Required to copy NuGet DLLs to output (isolated in ALC) -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>

<!-- Debug: output into NSLOAD's plugin subfolder for dev-reload -->
<PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'">
    <OutputPath>..\path-to-nsload\bin\Debug\YOURPLUGIN\Isolated\</OutputPath>
</PropertyGroup>

<!-- Release: output into deploy structure -->
<PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
    <OutputPath>..\path-to-nsload\bin\Release\YOURPLUGIN\Isolated\</OutputPath>
</PropertyGroup>
```

Add DevReload.Interface reference with `Private=false`:

```xml
<ProjectReference Include="X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\DevReload.Interface\DevReload.Interface.csproj">
    <Private>false</Private>
</ProjectReference>
```

`Private=false` is **critical** — DevReload.Interface.dll must NOT be in the Isolated folder. It must only exist alongside NSLOAD.dll so both the default ALC and the isolated ALC share the same type.

### Step 2: Add IPlugin implementation

Create one class in your project:

```csharp
using DevReload;

namespace YourPlugin
{
    public class YourPluginImpl : IPlugin
    {
        public void Initialize()
        {
            // Optional: capture SynchronizationContext for async
            // AcContext.Current = SynchronizationContext.Current;
        }

        public object CreatePaletteSet()
        {
            return new YourPaletteSet();  // return your existing PaletteSet
        }

        public void Terminate() { }
    }
}
```

### Step 3: Strip AutoCAD bootstrap code from Core

Remove from your project:
- `[assembly: CommandClass(...)]` attributes
- `[assembly: ExtensionApplication(...)]` attributes
- `IExtensionApplication` implementation
- `[CommandMethod]` definitions

Your project should have **zero** AutoCAD command registrations. All commands live in NSLOAD.

### Step 4: Add commands to NSLOAD

In `NsLoad.cs`, add 3 methods for your plugin (see "Adding a new PaletteSet plugin" above).

### Step 5: AutoCAD refs

Keep AutoCAD references (`accoremgd`, `acdbmgd`, `acmgd`) in your Core project with `Private=False`. They're needed for compilation but AutoCAD provides them at runtime.

### Step 6: Publish profile

Your publish profile should copy the Isolated/ folder to the deploy structure:

```
X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\2025\NSLOAD\YOURPLUGIN\Isolated\
    YourPlugin.Core.dll
    YourPlugin.Core.deps.json
    YourPlugin.Core.runtimeconfig.json
    *.dll  (NuGet deps)
```

</overview>
</section>

<section>
<overview>

## Runtime Flow

### Standard load (user types NSGIS)

```
1. NSLOAD.dll already loaded at startup (via acad2025.lsp)
2. User types NSGIS
3. NSLOAD creates new PluginHost<IPlugin>
4. PluginHost creates collectible IsolatedPluginContext
   - Excludes "DevReload.Interface" from isolation (shared type identity)
5. Loads GeoSearch.Core.dll into isolated ALC
6. Finds class implementing IPlugin via reflection
7. Calls IPlugin.Initialize()
8. Calls IPlugin.CreatePaletteSet() → PaletteSet shown
```

### Dev reload (user types NSGISDEV)

```
1. Closes existing PaletteSet (if open)
2. Unloads current ALC → GC.Collect() → DLLs freed on disk
3. DevReloadService.FindAndBuild("GeoSearch", ed):
   a. Enumerates Running Object Table for VS instances
   b. Finds VS instance with "GeoSearch" project
   c. If multiple matches → StringGridFormCaller dialog
   d. Verifies Debug configuration active
   e. Calls SolutionBuild.BuildProject() via EnvDTE COM
   f. Gets output path from VS project properties
   g. Returns full path to built DLL
4. Loads fresh GeoSearch.Core.dll into new ALC
5. IPlugin.Initialize() + CreatePaletteSet()
6. User sees updated code immediately
```

### Unload (user types NSGISUNLOAD)

```
1. Closes PaletteSet
2. Unloads ALC → DLLs freed
3. Plugin can be reloaded with NSGIS or NSGISDEV
```

</overview>
</section>

<section>
<overview>

## Important Rules

1. **AutoCAD refs: `Private=False`** in both NSLOAD and Core projects. AutoCAD provides these at runtime.

2. **DevReload.Interface: `Private=false` in Core only**. Prevents copy into Isolated folder. Both contexts must share the same DLL for `IPlugin` type identity.

3. **`EnableDynamicLoading=true`** in Core .csproj. Generates `.deps.json` and `.runtimeconfig.json` for `AssemblyDependencyResolver`.

4. **`CopyLocalLockFileAssemblies=true`** in Core .csproj. Copies all NuGet DLLs to Isolated folder.

5. **`"DevReload.Interface"` string** in `_host.Load(dllPath, "DevReload.Interface")` tells the ALC to NOT isolate this assembly. Falls back to default context for shared type identity.

6. **VS project name** in `DevReloadService.FindAndBuild("GeoSearch", ed)` must match exactly the project name in VS Solution Explorer.

7. **VS must be in Debug** for the DEV command. DevReloadService aborts if Release is active.

8. **One Isolated/ folder per plugin** under NSLOAD's directory. Never mix multiple plugins' NuGet deps in the same folder.

</overview>
</section>

<section>
<overview>

## Migration Checklist

### Per plugin (Core project):
- [ ] Add `AssemblyName=YourPlugin.Core` to .csproj
- [ ] Add `EnableDynamicLoading=true` to .csproj
- [ ] Add `CopyLocalLockFileAssemblies=true` to .csproj
- [ ] Set Debug/Release OutputPath to NSLOAD's `YOURPLUGIN\Isolated\` subfolder
- [ ] Add `DevReload.Interface` ProjectReference with `Private=false`
- [ ] Create IPlugin implementation class
- [ ] Remove `[assembly: CommandClass]`, `[assembly: ExtensionApplication]`
- [ ] Remove `IExtensionApplication` implementation
- [ ] Remove all `[CommandMethod]` definitions
- [ ] Verify AutoCAD refs have `Private=False`
- [ ] Update publish profile to deploy into `NSLOAD\YOURPLUGIN\Isolated\`
- [ ] Remove from Register-2025.csv (no longer loaded via NSLOAD command)

### NSLOAD project (once per plugin):
- [ ] Add 3 `[CommandMethod]` entries (LOAD, DEV, UNLOAD)
- [ ] Rebuild and redeploy NSLOAD

### One-time NSLOAD setup:
- [ ] Import DevReload.projitems shared project
- [ ] Import FormsSHARED.projitems shared project
- [ ] Add DevReload.Interface ProjectReference
- [ ] Add `Microsoft.VisualStudio.Interop` NuGet
- [ ] Add `AdWindows` reference (for PaletteSet)
- [ ] Add shared helper methods (LoadPalettePlugin, DevReloadPalettePlugin, etc.)

</overview>
</section>

<section>
<overview>

## What Stays the Same

- `acad2025.lsp` startup script — unchanged
- `NSLOAD` command for CSV-based plugins — unchanged
- `Register-2025.csv` — only remove PaletteSet plugins that migrate
- IntersectUtilities and other command-heavy plugins — no changes needed
- DevReload shared project files — unchanged (already generic)
- DevReload.Interface — unchanged (already generic IPlugin)

## What Changes

| Before | After |
|--------|-------|
| Per-plugin Loader project | Commands in NSLOAD |
| 2 projects per PaletteSet plugin + 1 shared Interface | 1 project per plugin |
| NSGIS.dll loaded via CSV | NSGIS command built into NSLOAD |
| Each Loader has its own DevReload copy | NSLOAD has single DevReload copy |
| Publish: GeoSearch.Loader → NSGIS/ | Publish: GeoSearch → NSLOAD/NSGIS/Isolated/ |

</overview>
</section>
