# Repository Guidelines

## Operation instructions
Follow these instructions:
 Work in EVIDENCE-FIRST mode.

1.	If recency matters or facts may change, run web search and cite 3–5 PRIMARY sources (law/official sites/tech docs/peer-review). For each key claim include: \[Verified\]/\[Unverified\], URL, source date, and confidence 0–1.
2.	If data is insufficient, ask up to 5 clarifying questions and wait. If still lacking, write: “I cannot verify this.”
3.	Forbidden: speculation, ballpark numbers without sources, fake/nonexistent links, unattributed paraphrase.
4.	Output format:

A) Brief facts-only summary;
B) Evidence table: Claim | Source | Date | Confidence;
C) Contradictions/risks and alternative interpretations;
D) Data gaps and what to ask/do next.

5.	Explicit instruction: SOLVE COMPLEX PROBLEMS. Terms: give short definitions and units.

Style: businesslike; no fluff, no stories, no metaphors.

## Strict Evidence Mode

• Prefer primary sources; use news/blogs only for context, tagged [Unverified] or low confidence.
• Do a critical review: when sources disagree, surface the divergences and plausible reasons.
• Don’t cut verifiability to fit length; if tight, prioritize Facts Summary and Evidence Table.
• If pauses aren’t allowed, first list needed clarifications; then give best attempt, explicitly marking assumptions and limits.
• Never mask lack of data: write “I cannot verify this” or “No sufficiently reliable sources found.” 

## Build Scripts
The repository includes batch files for building projects with MSBuild (required due to COM references that `dotnet build` cannot resolve):

| Script | Project | Configuration |
|--------|---------|---------------|
| `build-intersectutilities-debug.bat` | IntersectUtilities | Debug |
| `build-intersectutilities-release.bat` | IntersectUtilities | Release |
| `build-ntrexport-debug.bat` | NTRExport | Debug |
| `build-dimensioneringv2-debug.bat` | DimensioneringV2 | Debug |

All scripts use VS 2022 Community MSBuild at `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` and output errors only with summary.

### dotnet build
When building with `dotnet build`, always suppress warnings and build only the target project:
```bash
dotnet build ProjectName.csproj --nologo -v q
```
- `--nologo`: Suppress the startup banner
- `-v q`: Quiet verbosity (errors only)

Filter output to show only errors: `2>&1 | grep -E "(error|Error)"`

## Coding Style & Naming Conventions
Use four-space indentation and respect nullable reference types (`<Nullable>enable</Nullable>` is the default). Favor PascalCase for public types, camelCase for locals, and avoid abbreviations not already established nearby. Keep command class names aligned with their AutoCAD command keyword (for example `ApplyDimCommand`). `.editorconfig` disables XML documentation warnings; prefer concise inline comments only when the intent is non-obvious.

## Domain knowledge
### District heating pipe types
District Heating steel piping systems currently have two general types:
- Bonded pipes
- Twin pipes

#### Bonded pipes
Bonded pipes exist in all pipe dimensions. There are two distinct steel pipes with each a plastic jacked and insulation. These are drawn using distinct polylines each.

#### Twin pipes
Twin pipes exist in sizes up to DN 200 (250 sometimes). There are two steel pipes in one plastic jacket. The return pipe is above the supply pipe. This system is drawn using one polyline.