# AutoCAD Bundle Guide (.NET 8 / AutoCAD 2025)

This guide explains how to create and deploy AutoCAD `.bundle` packages
for .NET plugins targeting AutoCAD 2025+ (.NET 8).

## What is a Bundle?

A `.bundle` folder is AutoCAD's **Autoloader** package format.
AutoCAD scans specific directories on startup, reads `PackageContents.xml`,
and loads matching plugins automatically — no registry edits, no `NETLOAD`.

Bundles are trusted by default (`SECURELOAD` does not block them).

## Bundle Folder Structure

```
MyPlugin.bundle/
├── PackageContents.xml          ← manifest (which DLL, when to load)
└── Contents/
    └── Win64/
        ├── MyPlugin.dll         ← your plugin (net8.0-windows)
        └── Dependency.dll       ← third-party NuGet DLLs
```

## PackageContents.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPackage
    SchemaVersion="1.0"
    Name="MyPlugin"
    AppVersion="1.0.0"
    Description="Plugin description"
    Author="Author name"
    ProductCode="{GENERATE-A-GUID}">

  <Components>
    <ComponentEntry
        AppName="MyPlugin"
        ModuleName="./Contents/Win64/MyPlugin.dll"
        AppDescription="Plugin description"
        AppType=".Net"
        LoadOnAutoCADStartup="True">
      <RuntimeRequirements OS="Win64" SeriesMin="R25.0" SeriesMax="R25.0" />
    </ComponentEntry>
  </Components>
</ApplicationPackage>
```

### Key attributes

| Attribute | Value | Notes |
|---|---|---|
| `AppType` | `.Net` | Case-sensitive |
| `LoadOnAutoCADStartup` | `True` | Load when AutoCAD starts |
| `SeriesMin` | `R25.0` | AutoCAD 2025 |
| `SeriesMax` | `R25.0` | Prevents loading in older/newer incompatible versions |

**SeriesMin/SeriesMax is mandatory for AutoCAD 2025.** Without it, a .NET 8 DLL
may be loaded into an older .NET Framework host, causing a crash.

### AutoCAD version series reference

| AutoCAD | Series |
|---|---|
| 2024 | R24.3 |
| 2025 | R25.0 |
| 2026 | R26.0 |

## Which DLLs to Include

### AutoCAD-provided — NEVER bundle

These are already loaded in AutoCAD's process. Including them causes version conflicts.

| Assembly | Purpose |
|---|---|
| `accoremgd.dll` | Core managed API |
| `acdbmgd.dll` | Database managed API |
| `acmgd.dll` | Application/Editor API |
| `AdWindows.dll` | UI controls |
| `AcCui.dll` | Customization UI |
| Civil 3D DLLs | `AeccDbMgd.dll`, `AecBaseMgd.dll`, etc. |

Set `<Private>False</Private>` on all AutoCAD references in your `.csproj`.

### .NET 8 runtime — do NOT bundle

Assemblies that are part of the .NET 8 shared framework (e.g. `System.Text.Json`,
`Microsoft.VisualBasic`, `System.Runtime`) are already available at runtime.
The SDK will not copy them to output even with `CopyLocalLockFileAssemblies=true`.

### Third-party NuGet packages — DO bundle

Any NuGet package that is NOT part of the .NET 8 runtime must be in `Contents/Win64/`.
The .NET runtime probes the directory containing your plugin DLL, so sibling DLLs
are resolved automatically.

**Warning:** AutoCAD 2025 ships some third-party libraries internally.
If your plugin needs a *different version* of a library AutoCAD uses,
you will get a version conflict. Options:

1. Match the version AutoCAD ships with
2. Use a custom `AssemblyLoadContext` for isolation (advanced)

## .csproj Configuration

### Required properties

```xml
<PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Platforms>x64</Platforms>

    <!-- Prevent net8.0-windows/ subfolder in output path -->
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>

    <!-- Ensure NuGet DLLs are copied to build output -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

### Bundle output paths

```xml
<PropertyGroup>
    <BundleDir>$(MSBuildProjectDirectory)\..\..\Deploy\MyPlugin.bundle</BundleDir>
    <BundleContentsDir>$(BundleDir)\Contents\Win64</BundleContentsDir>
</PropertyGroup>
```

### AutoCAD references

```xml
<ItemGroup>
    <Reference Include="accoremgd">
        <HintPath>C:\Program Files\Autodesk\AutoCAD 2025\accoremgd.dll</HintPath>
        <Private>False</Private>
    </Reference>
    <!-- ... same pattern for acdbmgd, acmgd, AdWindows -->
</ItemGroup>
```

### MSBuild target (Release-only)

Add this target to your `.csproj` to auto-create the bundle on Release builds:

```xml
<Target Name="CreateBundle" AfterTargets="Build" Condition="'$(Configuration)' == 'Release'">
    <MakeDir Directories="$(BundleContentsDir)" />

    <ItemGroup>
        <BundleDlls Include="$(TargetDir)*.dll"
            Exclude="$(TargetDir)accoremgd.dll;
                     $(TargetDir)acdbmgd.dll;
                     $(TargetDir)acmgd.dll;
                     $(TargetDir)AdWindows.dll" />
    </ItemGroup>

    <Copy SourceFiles="@(BundleDlls)" DestinationFolder="$(BundleContentsDir)"
          SkipUnchangedFiles="true" />
    <Copy SourceFiles="$(MSBuildProjectDirectory)\PackageContents.xml"
          DestinationFolder="$(BundleDir)" SkipUnchangedFiles="true" />

    <Message Importance="high" Text="Bundle created at $(BundleDir)" />
</Target>
```

The `Exclude` list is a safety net — `Private=False` should prevent AutoCAD DLLs
from appearing in the output, but the exclude ensures they never end up in the bundle.

## Deployment

Copy the `.bundle` folder to one of these locations:

| Scope | Path |
|---|---|
| All users | `%PROGRAMDATA%\Autodesk\ApplicationPlugins\` |
| Current user | `%APPDATA%\Autodesk\ApplicationPlugins\` |

AutoCAD will discover and load it on next startup.

The `APPAUTOLOAD` system variable controls when AutoCAD scans these folders.
Default value loads plugins at startup and when a new drawing is opened.

## Your Plugin Entry Point

Your DLL must implement `IExtensionApplication` and declare it via assembly attribute:

```csharp
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(MyNamespace.MyApp))]

namespace MyNamespace
{
    public class MyApp : IExtensionApplication
    {
        public void Initialize()
        {
            // Runs in Application context — no document exists yet.
            // Defer UI work (ribbon, palettes) to the Idle event:
            Application.Idle += OnFirstIdle;
        }

        private void OnFirstIdle(object sender, EventArgs e)
        {
            Application.Idle -= OnFirstIdle;
            // Safe to create UI here
        }

        public void Terminate()
        {
            // Cleanup on shutdown
        }
    }
}
```

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Plugin not loading | Missing `SeriesMin`/`SeriesMax` | Add `RuntimeRequirements` to `PackageContents.xml` |
| `FileNotFoundException` for a dependency | NuGet DLL not in `Contents/Win64/` | Set `CopyLocalLockFileAssemblies=true` and check bundle |
| Version conflict crash | AutoCAD ships same library, different version | Match AutoCAD's version or use `AssemblyLoadContext` |
| `SECURELOAD` prompt | Not using bundle format | Switch from `NETLOAD` to `.bundle` deployment |
| `Initialize()` crashes on UI access | No document in Application context | Defer to `Application.Idle` event |

## NSLOAD Bundle

This project has bundle creation configured in `NSLOAD.csproj`.
Build in Release mode to create the bundle:

```
dotnet build Acad-C3D-Tools/NSLOAD/NSLOAD.csproj -c Release -p:Platform=x64
```

Output: `Deploy/NSLOAD.bundle/`

Deploy by copying to `%PROGRAMDATA%\Autodesk\ApplicationPlugins\NSLOAD.bundle\`.
