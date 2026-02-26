<section>
<overview>

# DevReload System

Shared infrastructure for AutoCAD .NET plugins that provides:
- **AssemblyLoadContext isolation** - isolates plugin NuGet dependencies from AutoCAD's runtime, solving DLL version conflicts
- **Collectible ALC** - plugins can be unloaded at runtime, freeing DLLs on disk for recompilation
- **Hot-reload via VS Interop** - `*DEV` commands find Visual Studio, build Debug, and load fresh DLLs without restarting AutoCAD

</overview>
</section>

<section>
<overview>

## Architecture

Each plugin is split into **2 projects** (Loader + Core) plus **2 shared projects** from this repo:

```
Acad-C3D-Tools/                         <- this repo
    DevReload/                          <- shared project (.shproj)
        IsolatedPluginContext.cs         <- generic collectible ALC
        PluginHost.cs                   <- generic plugin lifecycle (load/unload/GC)
        VsInstanceFinder.cs             <- P/Invoke ROT enumeration for VS instances
        DevReloadService.cs             <- orchestrate: find VS -> build -> return DLL path
    DevReload.Interface/                <- regular project (.csproj)
        IPlugin.cs                      <- universal plugin interface

YourPlugin repo/
    YourPlugin.Loader/                  <- host project (AutoCAD loads this)
        YourPluginCommands.cs           <- commands: LOAD, DEV, UNLOAD
        YourPlugin.Loader.csproj
    YourPlugin/                         <- core project (runs in isolated ALC)
        YourPluginImpl.cs               <- implements IPlugin
        YourPlugin.csproj               <- existing project, modified
```

### Why 2 projects per plugin?

The Loader lives in AutoCAD's default AssemblyLoadContext. The Core lives in an isolated collectible ALC with all its NuGet dependencies. This prevents version conflicts between your NuGet packages and AutoCAD's shipped assemblies.

### Why DevReload.Interface is a regular .csproj (NOT shared)?

The IPlugin interface MUST produce a single DLL referenced by both Loader and Core. If it were a shared project, the interface type would compile into both assemblies as two different types, and casting across the ALC boundary would fail. A single DLL ensures shared type identity.

</overview>
</section>

<section>
<overview>

## How It Works At Runtime

```
1. NSLOAD loads RELEASE YourPlugin.Loader.dll from deployment folder
2. User types YOURPLUGIN -> loads Core from Isolated/ subfolder
3. User types YOURPLUGINDEV:
   a. Closes UI (PaletteSet)
   b. Unloads current ALC (collectible) -> DLLs freed on disk
   c. Finds VS instance with matching project open (via COM ROT)
   d. Verifies Debug configuration
   e. Builds the project via VS automation (EnvDTE SolutionBuild)
   f. Gets output path from VS project properties
   g. Loads fresh Core.dll into new collectible ALC
   h. Calls IPlugin.Initialize() + CreatePaletteSet()
4. Subsequent YOURPLUGINDEV calls repeat steps 3a-3h (instant reload cycle)
```

### Key types in the isolated ALC:
- Your NuGet packages (isolated, no conflicts)
- Your plugin code (GeoSearch.Core.dll etc.)

### Key types in the default context (shared via fallback):
- DevReload.Interface.dll (IPlugin) - excluded from ALC loading for type identity
- AutoCAD assemblies (accoremgd, acmgd, etc.) - Private=False, not copied
- .NET framework and WPF types - resolved from runtime

</overview>
</section>

<section>
<overview>

## Migration Guide: Converting a Plugin

### Prerequisites
- Plugin targets `net8.0-windows`, `x64`
- Plugin has a PaletteSet or similar UI entry point

### Step 1: Modify existing project (becomes Core)

In your existing `.csproj`, add/change these properties:

```xml
<PropertyGroup>
    <AssemblyName>YourPlugin.Core</AssemblyName>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>

<!-- Debug outputs into Loader's Isolated subfolder -->
<PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'">
    <OutputPath>..\YourPlugin.Loader\bin\Debug\Isolated\</OutputPath>
</PropertyGroup>
<PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
    <OutputPath>..\YourPlugin.Loader\bin\Release\Isolated\</OutputPath>
</PropertyGroup>
```

Replace any old interface references with DevReload.Interface:

```xml
<ProjectReference Include="X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\DevReload.Interface\DevReload.Interface.csproj">
    <Private>false</Private>
</ProjectReference>
```

`Private=false` is critical - DevReload.Interface.dll must NOT be copied into the Isolated folder. It must only exist alongside the Loader so both contexts share the same type.

### Step 2: Create IPlugin implementation

Add a class to your existing (Core) project:

```csharp
using DevReload;

namespace YourPlugin
{
    public class YourPluginImpl : IPlugin
    {
        public void Initialize()
        {
            // Capture SynchronizationContext if you use async
            // AcContext.Current = SynchronizationContext.Current;
        }

        public object CreatePaletteSet()
        {
            // Return your existing PaletteSet instance
            return new YourPaletteSet();
        }

        public void Terminate() { }
    }
}
```

### Step 3: Strip old IExtensionApplication and CommandClass

Remove from Core project:
- `[assembly: CommandClass(...)]` attributes
- `[assembly: ExtensionApplication(...)]` attributes
- `IExtensionApplication` implementation
- Any `[CommandMethod]` definitions (commands move to Loader)

These all move to the Loader. The Core project should have NO AutoCAD command registrations.

### Step 4: Create the Loader project

Create `YourPlugin.Loader/YourPlugin.Loader.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0-windows</TargetFramework>
        <PlatformTarget>x64</PlatformTarget>
        <Platforms>x64</Platforms>
        <Nullable>enable</Nullable>
        <UseWPF>true</UseWPF>
        <UseWindowsForms>true</UseWindowsForms>
        <ImportWindowsDesktopTargets>true</ImportWindowsDesktopTargets>
        <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
        <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
        <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
        <OutputType>Library</OutputType>
        <AssemblyName>YOURPLUGIN</AssemblyName>
    </PropertyGroup>
    <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|x64'">
        <OutputPath>bin\Debug\</OutputPath>
        <DebugType>full</DebugType>
    </PropertyGroup>
    <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|x64'">
        <OutputPath>bin\Release\</OutputPath>
        <DebugType>full</DebugType>
    </PropertyGroup>
    <ItemGroup>
        <!-- Minimum AutoCAD refs needed for commands + PaletteSet -->
        <Reference Include="accoremgd">
            <HintPath>C:\Program Files\Autodesk\AutoCAD 2025\accoremgd.dll</HintPath>
            <Private>False</Private>
        </Reference>
        <Reference Include="acdbmgd">
            <HintPath>C:\Program Files\Autodesk\AutoCAD 2025\acdbmgd.dll</HintPath>
            <Private>False</Private>
        </Reference>
        <Reference Include="acmgd">
            <HintPath>C:\Program Files\Autodesk\AutoCAD 2025\acmgd.dll</HintPath>
            <Private>False</Private>
        </Reference>
        <Reference Include="AdWindows">
            <HintPath>C:\Program Files\Autodesk\AutoCAD 2025\AdWindows.dll</HintPath>
            <Private>False</Private>
        </Reference>
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\DevReload.Interface\DevReload.Interface.csproj" />
    </ItemGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.VisualStudio.Interop" Version="17.10.40170" />
    </ItemGroup>
    <Import Project="X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\DevReload\DevReload.projitems" Label="Shared" />
    <Import Project="X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\FormsSHARED\FormsSHARED.projitems" Label="Shared" />
</Project>
```

The Loader has NO NuGet packages of its own (except VS Interop for DevReload). It only references AutoCAD assemblies and DevReload infrastructure. FormsSHARED is required because DevReload uses `StringGridFormCaller` for VS instance selection when multiple instances are open.

### Step 5: Create Loader commands file

Create `YourPlugin.Loader/YourPluginCommands.cs`:

```csharp
using System;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using DevReload;

[assembly: CommandClass(typeof(YourPlugin.Loader.YourPluginCommands))]
[assembly: ExtensionApplication(typeof(YourPlugin.Loader.YourPluginCommands))]

namespace YourPlugin.Loader
{
    public class YourPluginCommands : IExtensionApplication
    {
        private static PluginHost<IPlugin> _host = new();
        private static PaletteSet? _paletteSet;

        public void Initialize()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\nYOURPLUGIN Loader initialized.");
        }

        public void Terminate()
        {
            ClosePaletteSet();
            if (_host.IsLoaded) _host.Unload();
        }

        [CommandMethod("YOURPLUGIN")]
        public static void LoadPlugin()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                string loaderDir = Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location)!;
                string pluginPath = Path.Combine(
                    loaderDir, "Isolated", "YourPlugin.Core.dll");

                Load(pluginPath);
                ed?.WriteMessage("\nYOURPLUGIN loaded successfully.");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\nError: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [CommandMethod("YOURPLUGINDEV")]
        public static void DevReloadPlugin()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            try
            {
                ClosePaletteSet();
                if (_host.IsLoaded) _host.Unload();

                // "YourPlugin" must match the project name in VS Solution Explorer
                string? dllPath = DevReloadService.FindAndBuild("YourPlugin", ed);
                if (dllPath == null) return;

                Load(dllPath);
                ed?.WriteMessage("\nYOURPLUGIN dev-reloaded successfully.");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\nError: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [CommandMethod("YOURPLUGINUNLOAD")]
        public static void UnloadPlugin()
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            if (!_host.IsLoaded)
            {
                ed?.WriteMessage("\nYOURPLUGIN is not loaded.");
                return;
            }
            ClosePaletteSet();
            _host.Unload();
            ed?.WriteMessage("\nYOURPLUGIN unloaded.");
        }

        private static void Load(string dllPath)
        {
            ClosePaletteSet();
            if (_host.IsLoaded) _host.Unload();

            var plugin = _host.Load(dllPath, "DevReload.Interface");
            plugin.Initialize();

            _paletteSet = (PaletteSet)plugin.CreatePaletteSet();
            _paletteSet.Visible = true;
            _paletteSet.Size = new System.Drawing.Size(400, 400);
            _paletteSet.Dock = DockSides.Right;
        }

        private static void ClosePaletteSet()
        {
            if (_paletteSet != null)
            {
                _paletteSet.Close();
                _paletteSet = null;
            }
        }
    }
}
```

### Step 6: Update your solution file

Add the Loader project to your `.sln`. The solution should contain:
- `YourPlugin` (the Core, existing project)
- `YourPlugin.Loader` (new)

DevReload.Interface is NOT in your solution - it's referenced by absolute path from Acad-C3D-Tools. VS will auto-build it as a dependency.

### Step 7: Build output structure

After building, verify:
```
YourPlugin.Loader/bin/Debug/
    YOURPLUGIN.dll              <- NETLOAD this in AutoCAD
    DevReload.Interface.dll     <- shared interface (tiny, ~3KB)
    Isolated/
        YourPlugin.Core.dll     <- your code
        YourPlugin.Core.deps.json
        YourPlugin.Core.runtimeconfig.json
        SomeNuGet.dll           <- all NuGet deps here, isolated
        ...
```

Verify that `DevReload.Interface.dll` is NOT inside `Isolated/`. If it is, the `Private=false` setting on the Core's ProjectReference is missing.

</overview>
</section>

<section>
<overview>

## Reference Implementation: GeoSearch

Located at `C:\Users\MichailGolubjev\Desktop\GitHub\DamgaardRI\Webservices\`

| Project | Assembly | Role |
|---------|----------|------|
| `GeoSearch.Loader` | `NSGIS.dll` | Host - commands NSGIS, NSGISDEV, NSGISUNLOAD |
| `GeoSearch` | `GeoSearch.Core.dll` | Core - all services, UI, NuGet deps |

Commands:
- `NSGIS` - load from `Isolated/GeoSearch.Core.dll` (release path)
- `NSGISDEV` - unload -> find VS -> build "GeoSearch" project -> load debug build
- `NSGISUNLOAD` - unload plugin, free DLLs

Key files:
- `GeoSearch.Loader/GeoSearchLoaderCommands.cs` - full Loader example
- `GeoSearch/GeoSearchPlugin.cs` - IPlugin implementation
- `GeoSearch/GeoSearch.csproj` - Core project config (EnableDynamicLoading, OutputPath to Isolated/)

</overview>
</section>

<section>
<overview>

## Important Rules

1. **AutoCAD refs must have `Private=False`** in both Loader and Core. AutoCAD provides these at runtime. Never copy them to output.

2. **DevReload.Interface ref must have `Private=false` in Core.csproj only**. This prevents it from being copied into the Isolated folder. The Loader's copy in the parent directory is the one both contexts share.

3. **`EnableDynamicLoading=true`** is required in Core.csproj. This generates `.deps.json` and `.runtimeconfig.json` files that `AssemblyDependencyResolver` uses to find NuGet dependencies.

4. **`CopyLocalLockFileAssemblies=true`** is required in Core.csproj. This copies all NuGet DLLs to the output (Isolated folder).

5. **The `"DevReload.Interface"` string** passed to `_host.Load(dllPath, "DevReload.Interface")` tells the ALC to NOT load this assembly in isolation. It falls back to the default context, preserving type identity for the `IPlugin` interface cast.

6. **The project name in `DevReloadService.FindAndBuild("ProjectName", ed)`** must match exactly the project name as shown in VS Solution Explorer.

7. **VS must be in Debug configuration** for the DEV command to work. If Release is active, DevReloadService aborts with a message.

</overview>
</section>
