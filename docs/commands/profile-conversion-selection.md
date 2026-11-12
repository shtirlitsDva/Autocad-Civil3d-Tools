# Profile conversion and QA: automatic profile view selection

`MYPFP`, `POLYLINEFROMPROFILE`, and the five `CHECKPROFILE...` QA commands share an
Auto/Manual setting. It starts in Auto when IntersectUtilities loads and remains
shared across drawings and these commands until the plugin is unloaded or Civil 3D closes.

- Pick the polyline or profile to use the closest profile view automatically.
- Enter **Manual** at that first selection prompt to restore the separate profile
  view prompt. Enter **Auto** at the first prompt of any of these commands to enable it again.
- Changing mode re-displays the source selection prompt; no source has to be picked first.
- The command line reports the name of the automatically chosen view.

Distance is measured in drawing XY coordinates from the click to the grid rectangle,
not to the view's origin or the centre of the source entity. A click inside a grid
has distance zero. Grid corners come from station/elevation conversion, excluding
the labels and bands that inflate the entity's geometric extents. The click is
transformed from the current UCS to WCS before comparison.

Candidates belong to the same space as the source. For a selected Civil 3D profile,
only views for that profile's alignment are candidates. Multiple containing views,
equally close views (within AutoCAD's point tolerance), and no suitable views use
the existing manual selection prompt. Manual fallback leaves Auto enabled.

If an eligible view is split or its grid cannot be read, selection also falls back
to manual. Split views have multiple elevation datums and cannot be reliably ranked
with one grid rectangle. Selection reads candidate views without changing them.

The conversion commands reuse their existing profile builder, tiny tangent
workaround, trimming, station/elevation conversion, and `Profile.ToPolyline`
implementation. `MYPFPOLD` is unchanged.

## QA selection and defaults

`CHECKPROFILERADII`, `CHECKPROFILEOVERLAP`, `CHECKPROFILECROSSINGS`,
`CHECKPROFILETANGENCY`, and `CHECKPROFILECOVER` now ask for the casing-centreline
polyline or Civil 3D profile first. They use the same view selector described above,
then check only that view's overlapping station range. A manually selected view
must belong to the selected native profile's alignment.

The crossing command automatically selects a block reference whose definition name
matches the chosen profile view's name in the same drawing space. It reports the
chosen block and its handle. Missing or duplicate matching references prompt for
manual selection; a manual pick must have the matching name and drawing space.

The cover check defaults to **Every5m**. Pressing Enter checks station multiples
of five plus the exact selected endpoints; **Continuous** remains available.
The cover-value prompt remembers its last valid value while the plugin is loaded,
starting at 0.60 m. Tangency stations display one decimal; station-gap and elevation-gap
columns and the original tangency tolerance remain in use.

## Loading the updated commands

Debug builds suppress AutoCAD's automatic command scanning using an empty
`CommandClass`. DevReload registers and unregisters the actual command methods.
Release builds keep normal AutoCAD/NETLOAD registration.

If a reload reports success but MYPFP still displays only `Select a polyline:`
and POLYLINEFROMPROFILE only `Select a Profile:`, older command handlers remain
registered in that AutoCAD session. Save the drawing and restart AutoCAD once,
then load the Debug plugin through DevReload. Avoid loading a Release copy in
the same session. The new first prompt must include `(profile view: Auto)` and
`[Auto/Manual]`; fallback additionally prints its reason before asking for a view.

## Verification

Run the existing geometry test project; it links the independent proximity selector:

```powershell
dotnet test .\Acad-C3D-Tools\IntersectUtilities.Geometry.Tests\IntersectUtilities.Geometry.Tests.csproj --nologo -v q
```

Tests cover containing grids, nearest edges and corners, overlapping grids, ties,
shared boundaries, enumeration-order independence, invalid inputs, and survey coordinates.

Interactive smoke checks (require Civil 3D):

1. Place two profile views apart and run each command, clicking inside each view.
   Verify one source click chooses the expected view and the conversion matches Manual.
2. Show one profile in two views of the same alignment; click it in each view and
   verify the picked location chooses that view.
3. Place a different alignment's view closer to the picked profile; verify it is
   excluded from automatic selection.
4. Switch to Manual in one command, invoke the other, and verify it also prompts
   for a view. Switch back to Auto and verify both commands use it.
5. Pick outside the grids near a long view's far edge; verify selection follows
   the grid edge, not the origin. Repeat under a translated/rotated UCS.
6. Overlap two grids, pick on their shared boundary, and pick equally far between
   them. Verify the manual fallback, including cancellation without conversion.
7. Cancel source selection and the manual view prompt. Verify no conversion occurs.
8. Use a split view or a drawing without candidate views; verify manual fallback.

## API evidence

Official Autodesk references consulted 2026-10-26 (2026 API documentation):

| Claim | Source | Confidence |
| --- | --- | --- |
| [Verified] The source selection result exposes the entity pick point. | [PromptEntityResult.PickedPoint](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_EditorInput_PromptEntityResult_PickedPoint.html) | 1.0 |
| [Verified] Station/elevation coordinates can be converted to grid XY coordinates. | [FindXYAtStationAndElevation](https://help.autodesk.com/cloudhelp/2026/ENU/Civil3D-API/files/html/b496edf6-6f68-1430-a50c-85e93c02635e.htm) | 1.0 |
| [Verified] Profile views expose their alignment, station range, elevation range, and split state. | [ProfileView](https://help.autodesk.com/cloudhelp/2026/ENU/Civil3D-API/files/html/64929981-c3ef-1482-9f0e-2d50562e71d1.htm) | 1.0 |
| [Verified] A profile exposes its parent alignment ID. | [Profile.AlignmentId](https://help.autodesk.com/cloudhelp/2026/ENU/Civil3D-API/files/html/831fbb9d-baf1-4088-1ee9-06e634ef2c44.htm) | 1.0 |

The project build checks API availability against its installed AutoCAD/Civil 3D
2025 references. Automated proximity tests do not verify interactive AutoCAD input.
