# DSMERGE RazorLight Implementation Plan

> **Quick Start for Next Session:**
> "Continue implementing RazorLight for DSMERGE report. Read the plan at `IntersectUtilities/DataScience/DSMERGE_RazorLight_Implementation_Plan.md` and start with Step 1."

---

## Overview
Refactor the `DSMERGEDATA` command's HTML report generator to use **RazorLight** templating instead of manual `StringBuilder` concatenation.

## Current State
- File: `DataScience.cs` (lines ~653-870)
- Method: `GenerateMergeReport()` 
- Currently uses `StringBuilder` to build HTML manually
- HTML/CSS is embedded as string literals in C# code
- Works but is hard to maintain and modify

## Target State
- Use RazorLight to render HTML from a Razor template
- Template stored as embedded resource or string constant
- Clean separation between data model and presentation
- Easier to modify HTML/CSS without touching C# logic

---

## Implementation Steps

### Step 1: Add NuGet Package

Add to `IntersectUtilities.csproj`:

```xml
<PackageReference Include="RazorLight" Version="2.3.1" />
```

**Note:** RazorLight requires .NET Standard 2.0+ or .NET Core. Verify the target framework is compatible.

### Step 2: Create Report Model Classes

Create a new file: `DataScience/MergeReportModel.cs`

```csharp
using System;
using System.Collections.Generic;

namespace IntersectUtilities.DataScience
{
    public class MergeReportModel
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public List<SummaryCard> SummaryCards { get; set; } = new();
        public List<StageCount> MatchStages { get; set; } = new();
        public List<ReportSection> Sections { get; set; } = new();
    }

    public class SummaryCard
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string CssClass { get; set; } = ""; // "success", "warning", "error", or ""
    }

    public class StageCount
    {
        public string Stage { get; set; } = "";
        public int Count { get; set; }
        public string CssClass => $"stage-{Stage.Replace("+", "")}";
    }

    public class ReportSection
    {
        public string Title { get; set; } = "";
        public int Count { get; set; }
        public string BadgeClass { get; set; } = ""; // "success", "warning", "error"
        public bool AutoOpen => BadgeClass != "success" && Count > 0;
        public string EmptyMessage { get; set; } = "No data.";
        public List<SectionGroup> Groups { get; set; } = new();
    }

    public class SectionGroup
    {
        public string Header { get; set; } = "";
        public string Description { get; set; } = "";
        public List<DataTableModel> Tables { get; set; } = new();
    }

    public class DataTableModel
    {
        public string Label { get; set; } = "";
        public List<string> Columns { get; set; } = new();
        public List<List<CellValue>> Rows { get; set; } = new();
    }

    public class CellValue
    {
        public string Display { get; set; } = "";
        public string FullValue { get; set; } = "";
        public string CssClass { get; set; } = "";
        public bool IsBadge { get; set; }

        public static CellValue Simple(string value, int maxLength = 25)
        {
            var full = value ?? "";
            var display = full.Length > maxLength ? full.Substring(0, maxLength - 3) + "..." : full;
            return new CellValue { Display = display, FullValue = full };
        }

        public static CellValue Badge(string value, string cssClass)
        {
            return new CellValue { Display = value, FullValue = value, CssClass = cssClass, IsBadge = true };
        }
    }
}
```

### Step 3: Create Razor Template

Create a new file: `DataScience/MergeReportTemplate.cshtml` (or embed as string constant)

**Option A: Embedded Resource File**
1. Create `MergeReportTemplate.cshtml` in the DataScience folder
2. Set Build Action to "Embedded Resource" in file properties
3. Load at runtime using `Assembly.GetManifestResourceStream()`

**Option B: String Constant** (simpler for AutoCAD plugin)
Create in a separate file or at top of DataScience.cs:

```csharp
private const string MergeReportTemplate = @"
@model IntersectUtilities.DataScience.MergeReportModel
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>DSMERGE Report</title>
    <style>
    /* === CSS STYLES (copy from current implementation) === */
    :root {
      --bg-primary: #1a1a2e;
      --bg-secondary: #16213e;
      --bg-tertiary: #0f3460;
      --text-primary: #eaeaea;
      --text-secondary: #a0a0a0;
      --accent: #e94560;
      --accent-secondary: #0f9b8e;
      --success: #4caf50;
      --warning: #ff9800;
      --error: #f44336;
      --border: #2a2a4a;
    }
    /* ... rest of CSS from current implementation ... */
    </style>
</head>
<body>
    <h1>DSMERGE Report</h1>
    <p class=""timestamp"">Generated: @Model.GeneratedAt.ToString(""yyyy-MM-dd HH:mm:ss"")</p>

    <!-- Summary Cards -->
    <div class=""summary-grid"">
        @foreach (var card in Model.SummaryCards)
        {
            <div class=""summary-card @card.CssClass"">
                <div class=""value"">@card.Value</div>
                <div class=""label"">@Html.Raw(System.Net.WebUtility.HtmlEncode(card.Label))</div>
            </div>
        }
    </div>

    <!-- Match Stage Breakdown -->
    <h2>Match Stage Breakdown</h2>
    <table class=""data-table"" style=""max-width: 400px;"">
        <tr><th>Stage</th><th>Count</th></tr>
        @foreach (var stage in Model.MatchStages)
        {
            <tr>
                <td><span class=""stage-badge @stage.CssClass"">@stage.Stage</span></td>
                <td>@stage.Count</td>
            </tr>
        }
    </table>

    <!-- Sections -->
    @foreach (var section in Model.Sections)
    {
        <div class=""section @(section.AutoOpen ? ""open"" : """")"">
            <div class=""section-header"">
                <h3>@section.Title</h3>
                <span class=""badge @section.BadgeClass"">@section.Count</span>
            </div>
            <div class=""section-content"">
                @if (section.Groups.Count == 0)
                {
                    <p class=""no-data"">@section.EmptyMessage</p>
                }
                else
                {
                    @foreach (var group in section.Groups)
                    {
                        <div class=""group-container"">
                            @if (!string.IsNullOrEmpty(group.Header))
                            {
                                <div class=""group-header"">@group.Header</div>
                            }
                            <div class=""group-content"">
                                @if (!string.IsNullOrEmpty(group.Description))
                                {
                                    <p>@group.Description</p>
                                }
                                @foreach (var table in group.Tables)
                                {
                                    @if (!string.IsNullOrEmpty(table.Label))
                                    {
                                        <div class=""sub-table-label"">@table.Label</div>
                                    }
                                    <div class=""table-scroll"">
                                        <table class=""data-table"">
                                            <tr>
                                                @foreach (var col in table.Columns)
                                                {
                                                    <th>@col</th>
                                                }
                                            </tr>
                                            @foreach (var row in table.Rows)
                                            {
                                                <tr>
                                                    @foreach (var cell in row)
                                                    {
                                                        <td title=""@cell.FullValue"">
                                                            @if (cell.IsBadge)
                                                            {
                                                                <span class=""stage-badge @cell.CssClass"">@cell.Display</span>
                                                            }
                                                            else
                                                            {
                                                                @cell.Display
                                                            }
                                                        </td>
                                                    }
                                                </tr>
                                            }
                                        </table>
                                    </div>
                                }
                            </div>
                        </div>
                    }
                }
            </div>
        </div>
    }

    <script>
    document.querySelectorAll('.section-header').forEach(header => {
        header.addEventListener('click', () => {
            header.parentElement.classList.toggle('open');
        });
    });
    </script>
</body>
</html>
";
```

### Step 4: Create RazorLight Engine Helper

Add to DataScience.cs or create separate file:

```csharp
using RazorLight;

private static readonly Lazy<RazorLightEngine> _razorEngine = new Lazy<RazorLightEngine>(() =>
    new RazorLightEngineBuilder()
        .UseMemoryCachingProvider()
        .Build());

private static async Task<string> RenderReportAsync(MergeReportModel model)
{
    var engine = _razorEngine.Value;
    return await engine.CompileRenderStringAsync("MergeReport", MergeReportTemplate, model);
}

// Synchronous wrapper for AutoCAD context (if async is problematic)
private static string RenderReport(MergeReportModel model)
{
    return RenderReportAsync(model).GetAwaiter().GetResult();
}
```

### Step 5: Create Model Builder Method

Replace the current `GenerateMergeReport` method body with a model-building approach:

```csharp
private static MergeReportModel BuildReportModel(
    CsvTypedDataTable leftTable,
    CsvTypedDataTable rightTable,
    List<MatchResult> matches,
    List<UnmatchedLeftRow> unmatched,
    List<int> orphanedRightIndices,
    Dictionary<string, int> matchStageCounts,
    int adjustedCoordinates,
    Dictionary<MergeKey, List<int>> leftGroups,
    Dictionary<MergeKey, List<int>> rightGroups)
{
    var model = new MergeReportModel();

    // Identify non-1:1 matches
    var nonSimpleMatches = matches.Where(m => m.Stage != "Ejendomsnr").ToList();
    var matchesByKey = matches
        .GroupBy(m => m.Key)
        .Where(g => g.Count() > 1 || g.Any(m => m.Stage != "Ejendomsnr"))
        .ToDictionary(g => g.Key, g => g.ToList());

    // Summary Cards
    model.SummaryCards = new List<SummaryCard>
    {
        new() { Label = "Left Rows", Value = leftTable.RowCount.ToString() },
        new() { Label = "Right Rows", Value = rightTable.RowCount.ToString() },
        new() { Label = "Matched", Value = matches.Count.ToString(), 
                CssClass = matches.Count == leftTable.RowCount ? "success" : "warning" },
        new() { Label = "Unmatched Left", Value = unmatched.Count.ToString(),
                CssClass = unmatched.Count == 0 ? "success" : "error" },
        new() { Label = "Orphaned Right", Value = orphanedRightIndices.Count.ToString(),
                CssClass = orphanedRightIndices.Count == 0 ? "success" : "warning" },
        new() { Label = "Non-Simple Matches", Value = nonSimpleMatches.Count.ToString(),
                CssClass = nonSimpleMatches.Count == 0 ? "success" : "warning" },
        new() { Label = "Coord. Adjustments", Value = adjustedCoordinates.ToString(),
                CssClass = adjustedCoordinates == 0 ? "success" : "warning" },
    };

    // Match Stages
    model.MatchStages = matchStageCounts
        .OrderByDescending(x => x.Value)
        .Select(x => new StageCount { Stage = x.Key, Count = x.Value })
        .ToList();

    // Build sections...
    // (Convert each section from current StringBuilder logic to model objects)
    
    return model;
}
```

### Step 6: Update GenerateMergeReport Method

```csharp
private static void GenerateMergeReport(
    string reportPath,
    CsvTypedDataTable leftTable,
    CsvTypedDataTable rightTable,
    List<MatchResult> matches,
    List<UnmatchedLeftRow> unmatched,
    List<int> orphanedRightIndices,
    Dictionary<string, int> matchStageCounts,
    int adjustedCoordinates,
    Dictionary<MergeKey, List<int>> leftGroups,
    Dictionary<MergeKey, List<int>> rightGroups)
{
    var model = BuildReportModel(
        leftTable, rightTable, matches, unmatched,
        orphanedRightIndices, matchStageCounts, adjustedCoordinates,
        leftGroups, rightGroups);

    string html = RenderReport(model);
    File.WriteAllText(reportPath, html, Encoding.UTF8);
}
```

---

## Files to Create/Modify

| File | Action | Description |
|------|--------|-------------|
| `IntersectUtilities.csproj` | Modify | Add RazorLight NuGet package |
| `DataScience/MergeReportModel.cs` | Create | Model classes for report data |
| `DataScience/MergeReportTemplate.cs` | Create | Razor template as string constant |
| `DataScience/DataScience.cs` | Modify | Replace StringBuilder code with model builder |

---

## Framework Compatibility ✅ CONFIRMED

**Target Framework:** `net8.0-windows10.0.26100.0` (.NET 8)
**AutoCAD Version:** 2025

✅ **RazorLight is fully compatible** with .NET 8. No compatibility issues expected.

---

## Potential Issues & Solutions

### Issue 1: Async in AutoCAD Context
AutoCAD commands run on a special thread. RazorLight is async-first.
**Solution:** Use `.GetAwaiter().GetResult()` wrapper, or use `Task.Run()` to offload.

### Issue 2: Assembly Loading
RazorLight dynamically compiles Razor templates which may have assembly loading issues.
**Solution:** Use `RazorLightEngineBuilder().UseMemoryCachingProvider()` and cache the engine.

### Issue 3: First Compilation Delay
RazorLight compiles the template on first use, which may cause a brief delay.
**Solution:** Use `Lazy<RazorLightEngine>` pattern so the engine is reused across calls.

---

## Testing Checklist

- [ ] Build succeeds after adding NuGet package
- [ ] Report generates without exceptions
- [ ] HTML renders correctly in browser
- [ ] All sections display correctly
- [ ] Collapsible sections work
- [ ] Data tables show all columns
- [ ] Hover tooltips work for truncated values
- [ ] Summary cards show correct values
- [ ] Auto-open works for sections with issues

---

## Rollback Plan

If RazorLight causes issues, the current StringBuilder implementation is preserved in git history.
The refactoring is isolated to the report generation - matching logic is unchanged.

---

## Time Estimate

- Step 1 (NuGet): 5 minutes
- Step 2 (Models): 30 minutes
- Step 3 (Template): 45 minutes
- Step 4 (Engine): 15 minutes
- Step 5-6 (Integration): 45 minutes
- Testing: 30 minutes

**Total: ~3 hours**

---

## Commands for Next Session

```
1. Check .NET target framework in IntersectUtilities.csproj
2. If compatible, add RazorLight package
3. Create model classes
4. Extract current CSS/HTML to template
5. Build model from existing data
6. Test report generation
```
