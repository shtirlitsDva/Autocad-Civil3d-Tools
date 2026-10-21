LER Compare — integrated into Intersect Utilities
Civil 3D 2025 / .NET 8 / 2026-10-09

USE
Open the newer LER DWG, switch to Model space, and run LERCOMPARE.
Choose the older DWG and click Compare. The supplied old Lyngby path is prefilled.
The newer drawing is read from its current in-memory state, including unsaved edits.
Neither source drawing is saved or edited by the comparison.

This feature belongs to the existing IntersectUtilities assembly. Use the existing
Intersect Utilities loader. Do not NETLOAD the retired standalone LerCompare.dll.
DevReload is configured to the local IntersectUtilities Debug build. For future
builds, use its normal unload/load commands or development reload workflow.

The repository instructions now make IntersectUtilities the default destination
for new custom Civil 3D tools unless the user specifies another project.

RESULTS
All direct model-space polylines are compared: lightweight, legacy 2D, and 3D
projected into XY. Components, blocks and nested xrefs are not traversed.
Flags: New, Missing, Status, Material, Properties, Geometry, Split, Merged,
Review, Coverage, Identifier, Delivery, Layer, TextFormat. Flags can overlap.
Filter categories combine with OR. Utility and text filters narrow those results.
Select a row to inspect qualified Property Set names and before/after values.
Double-click a row or use Zoom to result to find its routes.

The overlay initially shows the selected result. Turn off Selected result only
to display every filtered result. Old/new routes and changed vertices/edges can
be toggled separately. Clear overlay removes previews; Show overlay restores them.
Closing the palette or changing drawings clears the temporary graphics. Reopen
the palette, or return to the comparison drawing and use Show overlay.
Graphics are transient and are not added to model space, saved or plotted.

Colors: New green, Missing red, Status yellow, Material cyan, Geometry blue,
Review magenta, Coverage orange. Review takes color priority, followed by Coverage,
New, Missing, Status, Material, Geometry and Split/Merged. All flags remain in the
table. Matched old routes use grey; changed old vertices/edges red, new ones cyan.

MATCHING
The geometry tolerance defaults to 1 cm. A moved vertex marks its connected
edges; vertex insertion/deletion and changed arc bulges are also detected.
Reversed route direction and rotated closed-loop start indices are normalized.
The default candidate matching radius is 1 m, separate from the change tolerance.
It can be adjusted. Probable geometry matches, ambiguous routes, duplicate or
conflicting IDs, and owner changes remain Review results.

Matching uses geometry, utility, owner, GmlId and owner/utility-scoped LerId.
Drawing handles are never used as cross-file identities. Splits and merges
require complete route coverage within tolerance without gaps or overlaps.
Their properties are compared as value sets; JSON retains each original section.

Shortened, extended and partially resegmented routes may fail a whole-route
match despite following existing pipes. Substantial parallel overlap within the
candidate radius is grouped under Review with Geometry and Property Set flags.
Routes sharing geometry with an already matched entity, and short nearby routes
with uncertain identity, retain explicit candidates under Review. Candidate
handles are prefixed with ? and do not establish identity or consume an entity
twice. New / unmatched and Missing / unmatched mean no accepted correspondence;
they are not confirmed physical additions or removals.

PROPERTY SETS AND COVERAGE
All readable fields retain their Property Set names. Added/removed sets and
fields are detected; absent differs from empty. JSON uses OldPresent/NewPresent
to distinguish absence. Material/status changes have separate flags. Delivery
fields (LerNummer/GmlBemærkning), identifiers (GmlId/LerId), and case-only text
changes are initially unchecked to make other changes easier to inspect.
Equivalent localized boolean/date representations are normalized.

Coverage uses hatches on GraveforespPolygon, configurable in the palette.
Polyline/bulge loops, holes, polygon unions and crossing routes are supported.
Coverage means a route has a portion in an area requested in only one delivery.
New/Missing flags are retained. Missing or unsupported coverage hatches produce
a warning and the comparison falls back to New/Missing.

Units are normalized into the newer drawing's units. Use the explicit units
override only when both files use those units and their INSUNITS are incorrect.
Both drawings must use the same coordinate reference system; projections are
not inferred or transformed. Fitted/non-horizontal polylines and property read
failures receive Review flags.

EXPORT
Export report saves a full JSON report with geometry and original properties,
or a CSV with one row per difference. Reports retain paths, settings, match
methods and warnings. New/Missing describes delivery differences; it does not
establish physical installation or removal.

CRASH CORRECTION
Windows logged an AccessViolationException at PaletteSet.Visible during the
standalone LERCOMPARE_RUN. The earlier UI test loaded multiple copies with the
same command names/palette GUID and disposed test palettes without clearing
their ownership. That is the likely trigger; the stack alone does not prove it.
The integrated feature uses existing IntersectUtilities command registration,
one owned palette, an IsDisposed check before reuse, ownership reset before
disposal, named event handlers, and Intersect.Terminate cleanup. It has a new
palette GUID distinct from the retired prototype. It does not add another
IExtensionApplication or standalone CommandClass registration.

BUILD AND TESTS
Source is in Acad-C3D-Tools/IntersectUtilities/LerCompare in the existing repository.
The seven source files are compiled automatically by IntersectUtilities.csproj.
Intersect.Terminate contains the reset hook. Tests are included in the existing
IntersectUtilities.Geometry.Tests project: 42 comparison cases plus 5 palette
ownership cases; the complete suite has 67 passing tests.

Use VS 2022 Community MSBuild for the IntersectUtilities project:
  MSBuild.exe IntersectUtilities.csproj /t:Build /p:Configuration=Debug /p:Platform=x64
Run the test project with dotnet test. The comparison engine tests run without
AutoCAD. Compilation succeeded with zero errors; existing project warnings remain.

The provided pair has 25,805 old and 26,191 new polylines. The original Water/New
count of 928 was inflated by missed partial and uncertain route correspondences.
The corrected counts and selected handle 26C6 audit are in the supplied reports.
The virtual result table was also checked outside Civil 3D with the complete
report, including filtering, selection, empty results and clearing/rebinding.
See validation.json for final live Civil 3D checks and assembly identity.
