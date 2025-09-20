# Repository Guidelines

## Project Structure & Module Organization
The solution `Acad-C3D-Tools.sln` organizes AutoCAD plug-ins and helper utilities. Core command assemblies live in `AutoCADCommands`, `AutoCADCommandsSHARED`, and `AutoCadUtils`, sharing geometry and IO helpers with `UtilitiesCommonSHARED`. Feature-specific tools sit in folders such as `ExportBlocksToSvg`, `SheetCreationAutomation`, `IsoTools`, and `DRILOAD`. Packaging and external bridges (for example `GDALService`, `netDxf`, `GeneticSharpV2`) supply integrations, while `DocsCreateCommandDescriptionsHtml` generates command documentation. Manual harnesses reside under `LiveCharts2TestProject` and `NorsynHydraulicCalcTestApp`.

## Build, Test, and Development Commands
- `dotnet restore Acad-C3D-Tools.sln` - restore NuGet packages, including AutoCAD SDK facades.
- `dotnet build Acad-C3D-Tools.sln -c Release -r win-x64` - compile plug-in DLLs into `bin/Release` for deployment into AutoCAD.
- `dotnet test LiveCharts2TestProject/LiveCharts2TestProject.csproj` - run the available automated smoke tests.
- `dotnet run --project DocsCreateCommandDescriptionsHtml` - rebuild HTML command listings when public APIs change.

## Coding Style & Naming Conventions
Use four-space indentation and respect nullable reference types (`<Nullable>enable</Nullable>` is the default). Favor PascalCase for public types, camelCase for locals, and avoid abbreviations not already established nearby. Keep command class names aligned with their AutoCAD command keyword (for example `ApplyDimCommand`). `.editorconfig` disables XML documentation warnings; prefer concise inline comments only when the intent is non-obvious.

## Testing Guidelines
Automated coverage is minimal, so prioritize scenario regression runs using the supplied harnesses. Name new test projects `<FeatureName>.Tests` and add them to the solution so they participate in `dotnet test`. For UI-centric workflows, validate inside a Civil 3D sandbox profile and capture before-and-after screenshots to back up changes.

## Commit & Pull Request Guidelines
Existing commits are short, action-oriented statements ("finish applydim", "grid sampling finished"). Follow the same style: a 50-character subject, imperative verb, and optional scope detail. Pull requests should outline the scenario, list affected commands, reference issue IDs when available, and provide deployment notes (DLL path, Civil 3D version) plus supporting imagery.

## Environment & External Dependencies
Development targets Windows x64 with AutoCAD 2025 SDK packages. Ensure AutoCAD or Civil 3D assemblies are available locally; the projects exclude runtime assets, so test inside the host application by copying built DLLs into `%APPDATA%/Autodesk/ApplicationPlugins`.
