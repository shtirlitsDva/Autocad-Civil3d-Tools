# DevReload — Hot-Reload Plugin System for AutoCAD 2025

DevReload lets you edit, build, and reload AutoCAD .NET plugins without restarting AutoCAD. It uses .NET 8 collectible `AssemblyLoadContext` to isolate plugins and stream-loads DLLs so Visual Studio can rebuild freely while the old plugin runs. The `{PREFIX}DEV` command builds your project via VS COM automation, tears down the old plugin, and loads the new one — all in one step.

## Project Setup (.csproj)

Your plugin project needs these settings:

```xml
<PropertyGroup>
    <TargetFramework>net8.0-windows8.0</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <Platforms>x64</Platforms>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ImportWindowsDesktopTargets>true</ImportWindowsDesktopTargets>
    <OutputType>Library</OutputType>

    <!-- REQUIRED for collectible ALC -->
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>

    <!-- Convention: DLL is named {ProjectName}.Core.dll -->
    <AssemblyName>MyPlugin.Core</AssemblyName>
</PropertyGroup>
```

References:

```xml
<ItemGroup>
    <!-- AutoCAD assemblies — Private=False so they're not copied -->
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
    <!-- Shared interface — Private=false keeps it in default ALC for type identity -->
    <ProjectReference Include="..\DevReload.Interface\DevReload.Interface.csproj">
        <Private>false</Private>
    </ProjectReference>
</ItemGroup>
```

See `DevReloadTest/DevReloadTest.csproj` for a complete working example.

## Dual-Mode: IExtensionApplication + IPlugin

Plugins work in **two modes** from the same DLL:
- **Release** (for users): loaded via `NETLOAD` — AutoCAD calls `IExtensionApplication.Initialize()`
- **Development** (for you): loaded via DevReload — AutoCAD *still* calls `Initialize()` automatically

AutoCAD scans *all* loaded assemblies — including those stream-loaded into a custom `AssemblyLoadContext` — for `[assembly: ExtensionApplication]` and calls `Initialize()` on its own. DevReload does **not** call `Initialize()` to avoid double-calling it.

DevReload **does** call `Terminate()` on teardown, because AutoCAD does NOT call `Terminate()` when an ALC is unloaded.

**Summary:**
| Method | NETLOAD (AutoCAD) | DevReload |
|--------|-------------------|-----------|
| `Initialize()` | AutoCAD calls it | AutoCAD calls it (DevReload skips) |
| `Terminate()` | AutoCAD calls on shutdown | DevReload calls on unload/reload |
| `CreatePaletteSet()` | Not called | DevReload calls it |

## Implement IPlugin (+ IExtensionApplication)

Your plugin class implements both `IPlugin` and `IExtensionApplication`. The shared `Initialize()` and `Terminate()` methods satisfy both interfaces via implicit implementation — no code duplication:

```csharp
using Autodesk.AutoCAD.Runtime;
using DevReload;

[assembly: ExtensionApplication(typeof(MyNamespace.MyPlugin))]

namespace MyNamespace
{
    public class MyPlugin : IPlugin, IExtensionApplication
    {
        public void Initialize()
        {
            // Subscribe to events, set up state, etc.
            // Called once by AutoCAD — whether via NETLOAD or DevReload.
        }

        public object CreatePaletteSet()
        {
            // Return a PaletteSet instance, or null for command-only plugins.
            // Only called by DevReload, not by AutoCAD's NETLOAD.
            return new MyPaletteSet();
        }

        public void Terminate()
        {
            // CRITICAL: Clean up EVERYTHING.
            // Called on every unload and reload — not just app shutdown.
            //
            // Cleanup checklist:
            // - Unsubscribe from Document/Editor/Database events
            // - Release COM objects (Marshal.ReleaseComObject)
            // - Stop and dispose timers
            // - Clear static fields that reference plugin types
            // - Dispose IDisposable resources
        }
    }
}
```

**Palette-only example** (no commands — see `NSPaletteSet/NSPalettePlugin.cs`):

```csharp
public class NSPalettePlugin : IPlugin
{
    public void Initialize() { }
    public object CreatePaletteSet() => new MyPaletteSet();
    public void Terminate() { }
}
```

## plugins.json Configuration

Create a `plugins.json` file next to `DevReload.dll` to register your plugins:

```json
{
  "plugins": [
    {
      "name": "MyPlugin",
      "dllPath": "C:\\Path\\To\\MyPlugin.Core.dll",
      "vsProject": "MyPlugin",
      "commands": true,
      "commandPrefix": "MYPLUGIN",
      "loadOnStartup": true,
      "paletteWidth": 400,
      "paletteHeight": 600,
      "dockSide": "Right"
    }
  ]
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `name` | *(required)* | Unique plugin name used in messages and lifecycle calls |
| `dllPath` | *(required)* | Absolute path to the plugin DLL |
| `vsProject` | `{name}` | VS Solution Explorer project name for dev-reload builds |
| `commands` | `false` | Enable `CommandRegistrar` to scan for `[CommandMethod]` |
| `commandPrefix` | `{name}` | Prefix for generated LOAD/DEV/UNLOAD commands |
| `loadOnStartup` | `false` | Auto-load when AutoCAD starts |
| `paletteWidth` | `400` | Initial palette width |
| `paletteHeight` | `600` | Initial palette height |
| `dockSide` | `Right` | Palette dock side (`Left`, `Right`) |

## Generated Commands

For each plugin, DevReload registers three commands using the `commandPrefix`:

| Command | Action |
|---------|--------|
| `{PREFIX}LOAD` | Load from configured DLL path. If already loaded, shows the palette. |
| `{PREFIX}DEV` | Build from VS, then reload. If build fails, old plugin stays running. |
| `{PREFIX}UNLOAD` | Close palette, unregister commands, terminate, unload ALC. |

The management palette is opened with the `DEVRELOAD` command.

## Dev Workflow

1. Open your solution in Visual Studio
2. Start AutoCAD (DevReload loads via autoload)
3. Edit your plugin code in VS
4. In AutoCAD command line, type `{PREFIX}DEV` (e.g., `TESTDEV`)
5. DevReload builds your project via VS COM automation, tears down the old plugin, loads the new DLL
6. See your changes immediately — no AutoCAD restart needed

The `{PREFIX}DEV` command is safe: it builds **before** tearing down. If the build fails, the old plugin stays loaded and functional.

## Examples

### Command + Palette plugin (DevReloadTest)

```json
{
  "name": "DevReloadTest",
  "dllPath": "C:\\Path\\To\\DevReloadTest.Core.dll",
  "commands": true,
  "commandPrefix": "TEST",
  "loadOnStartup": false
}
```

Files: `TestPlugin.cs` (IPlugin + IExtensionApplication), `TestCommands.cs` (commands), `TestPaletteSet.cs` (palette).

Commands generated: `TESTLOAD`, `TESTDEV`, `TESTUNLOAD`.

### Palette-only plugin (NSPalette)

```json
{
  "name": "NSPalette",
  "dllPath": "C:\\Path\\To\\NSPalette.dll",
  "commands": false,
  "loadOnStartup": true
}
```

Files: `NSPalettePlugin.cs` (IPlugin, no commands).

Commands generated: `NSPALETTELOAD`, `NSPALETTEDEV`, `NSPALETTEUNLOAD`.
