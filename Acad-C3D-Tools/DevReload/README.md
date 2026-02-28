# DevReload — Hot-Reload Plugin System for AutoCAD 2025

DevReload lets you edit, build, and reload AutoCAD .NET plugins without restarting AutoCAD. It uses .NET 8 collectible `AssemblyLoadContext` to isolate plugins and stream-loads DLLs so Visual Studio can rebuild freely while the old plugin runs. The `{PREFIX}DEV` command builds your project via VS COM automation, tears down the old plugin, and loads the new one — all in one step.

DevReload works exclusively with **Debug builds**. The "Add Plugin" flow contacts Visual Studio via COM, lists loaded projects, and auto-derives the Debug output path and DLL name.

## Quickstart

1. Place `DevReload.dll` + `DevReload.Interface.dll` in a folder, autoload via `acad2025.lsp`
2. Open your plugin solution in Visual Studio (**Debug** configuration)
3. Start AutoCAD, type `DEVRELOAD` to open the management palette
4. Click **+ Add Plugin** → pick your project from the VS project list
5. Click **Add** → your plugin is registered with `{PREFIX}LOAD` / `{PREFIX}DEV` / `{PREFIX}UNLOAD`
7. Type `{PREFIX}LOAD` — loads your Debug DLL (builds first if it doesn't exist)
8. Edit code in VS → type `{PREFIX}DEV` → see changes instantly, no restart

**Required in your plugin** (prevents stale commands on reload):
```csharp
#if DEBUG
[assembly: CommandClass(typeof(YourNamespace.NoAutoCommands))]
#endif
// ...
#if DEBUG
public class NoAutoCommands { }
#endif
```

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
- **Release** (for users): loaded via `NETLOAD` — AutoCAD calls `IExtensionApplication.Initialize()` and registers commands normally
- **Debug** (for you): loaded via DevReload — AutoCAD *still* calls `Initialize()` automatically, but command registration is suppressed

### Dual-Instance Problem & Static State

AutoCAD and DevReload create **separate instances** of your plugin class:
- **Instance A**: AutoCAD creates via `[assembly: ExtensionApplication]` → calls `Initialize()`
- **Instance B**: DevReload creates via `Activator.CreateInstance` → calls `Terminate()` on unload

These are different objects. Instance fields set in `Initialize()` on Instance A are NOT visible to `Terminate()` on Instance B. **Use static fields for anything that needs cleanup:**

```csharp
public class MyPlugin : IPlugin, IExtensionApplication
{
    private static EventHandler? _docActivated;

    public void Initialize()
    {
        _docActivated = (s, e) => { /* handle */ };
        Application.DocumentManager.DocumentActivated += _docActivated;
    }

    public void Terminate()
    {
        if (_docActivated != null)
            Application.DocumentManager.DocumentActivated -= _docActivated;
        _docActivated = null;
    }
}
```

### CommandClass Suppression (Required for Debug)

AutoCAD's `ExtensionLoader` scans loaded assemblies for `[CommandMethod]` attributes and registers them via `CommandClass.AddCommand`. These registrations are **permanent** — no public API to remove them. On reload, this causes `eDuplicateKey` errors and stale commands.

To prevent this, plugin assemblies must suppress AutoCAD's command scanning in Debug builds:

```csharp
#if DEBUG
[assembly: CommandClass(typeof(MyNamespace.NoAutoCommands))]
#endif

namespace MyNamespace
{
#if DEBUG
    public class NoAutoCommands { }
#endif
}
```

- **Debug**: AutoCAD sees `CommandClass(typeof(NoAutoCommands))`, scans only that empty class, finds no commands. DevReload's `CommandRegistrar` handles registration via `Utils.AddCommand` (which CAN be unregistered on reload).
- **Release**: No `CommandClass` attribute → AutoCAD scans all types and registers commands normally via `NETLOAD`.

If your plugin already has a custom `[assembly: CommandClass]` for Release, guard it:

```csharp
#if DEBUG
[assembly: CommandClass(typeof(NoAutoCommands))]
#else
[assembly: CommandClass(typeof(MyProductionCommands))]
#endif
```

### Lifecycle Summary

| Method | NETLOAD (Release) | DevReload (Debug) |
|--------|-------------------|-------------------|
| `Initialize()` | AutoCAD calls it | AutoCAD calls it (DevReload skips) |
| `Terminate()` | AutoCAD calls on shutdown | DevReload calls on unload/reload |
| `CreatePaletteSet()` | Not called | DevReload calls it |
| Commands | AutoCAD registers via `CommandClass.AddCommand` | DevReload registers via `Utils.AddCommand` |

## Implement IPlugin (+ IExtensionApplication)

Your plugin class implements both `IPlugin` and `IExtensionApplication`:

```csharp
using Autodesk.AutoCAD.Runtime;
using DevReload;

[assembly: ExtensionApplication(typeof(MyNamespace.MyPlugin))]

#if DEBUG
[assembly: CommandClass(typeof(MyNamespace.NoAutoCommands))]
#endif

namespace MyNamespace
{
#if DEBUG
    public class NoAutoCommands { }
#endif

    public class MyPlugin : IPlugin, IPluginPalette, IExtensionApplication
    {
        public void Initialize()
        {
            // Use STATIC fields for event subscriptions and state.
        }

        public object CreatePaletteSet()
        {
            return new MyPaletteSet();
        }

        public void Terminate()
        {
            // Clean up STATIC state: unsubscribe events, dispose resources.
        }
    }
}
```

## Adding Plugins (VS-Driven)

1. Open `DEVRELOAD` management palette in AutoCAD
2. Click **"+ Add Plugin"**
3. DevReload contacts Visual Studio via COM and lists all loaded projects
4. Select a project from the picker
5. Project name, Debug DLL path, and VS project name are auto-derived
6. Optionally set: Command Prefix, Load on Startup
7. Click **Add**

If multiple VS instances are open, projects are shown as `SolutionName:ProjectName`.

## plugins.json Configuration

Plugins are stored in `plugins.json` next to `DevReload.dll`:

```json
{
  "plugins": [
    {
      "name": "DevReloadTest",
      "dllPath": "C:\\Path\\To\\bin\\Debug\\DevReloadTest.dll",
      "vsProject": "DevReloadTest",
      "commandPrefix": "TEST",
      "loadOnStartup": false
    }
  ]
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `name` | *(required)* | Unique plugin name (auto-derived from VS project) |
| `dllPath` | *(auto)* | Path to Debug output DLL (auto-derived from VS) |
| `vsProject` | *(auto)* | VS project name (auto-derived) |
| `commandPrefix` | `{name}` | Prefix for generated LOAD/DEV/UNLOAD commands |
| `loadOnStartup` | `false` | Auto-load when AutoCAD starts |
| `paletteWidth` | `400` | Initial palette width |
| `paletteHeight` | `600` | Initial palette height |
| `dockSide` | `Right` | Palette dock side (`Left`, `Right`) |

## Generated Commands

For each plugin, DevReload registers three commands using the `commandPrefix`:

| Command | Action |
|---------|--------|
| `{PREFIX}LOAD` | Load from Debug DLL path. If DLL not found, builds the project first. |
| `{PREFIX}DEV` | Build from VS, then reload. If build fails, old plugin stays running. |
| `{PREFIX}UNLOAD` | Close palette, unregister commands, terminate, unload ALC. |

The management palette is opened with the `DEVRELOAD` command.

## Dev Workflow

1. Open your solution in Visual Studio (Debug configuration)
2. Start AutoCAD (DevReload loads via autoload)
3. `DEVRELOAD` → Add Plugin → select your project
4. Edit your plugin code in VS
5. In AutoCAD, type `{PREFIX}DEV` (e.g., `TESTDEV`)
6. DevReload builds, tears down old plugin, loads new DLL
7. See your changes immediately — no AutoCAD restart needed

The `{PREFIX}DEV` command is safe: it builds **before** tearing down. If the build fails, the old plugin stays loaded and functional.

The `{PREFIX}LOAD` command will auto-build if the Debug DLL doesn't exist yet.
