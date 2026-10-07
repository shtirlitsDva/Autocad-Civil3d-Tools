# LERPROBE

Reload IntersectUtilities, run **LERPROBE**, then click a polyline inside the
LER xref. A read-only window shows its attached AEC property sets without
opening the source drawing. Lightweight, legacy 2D and 3D polylines are accepted.

The header identifies the xref, source file, layer and entity handle. The table
shows only property-set name, property name and value. Search filters property
names, values and set names. Select rows and press **Ctrl+C** to copy them with column headings.
Close or Escape returns to the drawing.

All attached sets are included, including properties hidden in a definition.
Duplicate property names remain identified by their set. Automatic properties
evaluate with the source object as context. An unavailable value is identified
in its row; a polyline without attached sets receives a clear empty-state message.
Values are displayed using the current culture, without inventing units.

The inspector uses the selected entity's source database and reads every object
with `OpenMode.ForRead`. It does not attach sets, change attributes, create
geometry, load a separate DWG, or save the xref. All native transactions end
before displaying the window; the window holds a snapshot of strings/values.
Inspection works with nested blocks and xrefs and requires a loaded reference.

## Validation

On 2026-10-07, **11 native Civil 3D 2025 fixture cases passed**, covering a real
temporary xref with nested blocks, two property sets, duplicate property names,
Danish text, numeric values, automatic length, source metadata, container order,
3D polylines, empty sets and rejected host/non-polyline selections.

The production reader also read the loaded `LER_2D_7.24.9` xref's `Elledning`
(26 properties) and `Vandledning` (24 properties) sets, with no unavailable
values and unchanged active drawing `DBMOD` (17 before/after). Five window
checks passed: complete read-only table, value search, case-insensitive name
search, empty search results and restoration after clearing the filter.
The window was rendered and visually checked in the running Civil 3D process.

`LerProbeRegression.csx` runs in ACD-MCP after the crawl core and probe models,
reader and window are loaded as script declarations. Strip namespaces/imports
from the files and put imports first. Its fixtures use disposable databases
and temporary DWGs; they do not modify the user's drawing. The full interactive
command selection/modal-window workflow still needs a user run after reload.

API evidence: native reflection verified `PropertyDataServices.GetPropertySets`,
`PropertySet.GetAt(int, DBObject)`, definition/property members and
`Application.ShowModalDialog(Form)` in the installed 2025 assemblies; the
fixture and live-source reads exercised those AEC members.
