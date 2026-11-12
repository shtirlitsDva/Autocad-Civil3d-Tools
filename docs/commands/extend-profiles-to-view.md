# EXTENDPROFILESTOVIEW

Run `EXTENDPROFILESTOVIEW` in the active Civil 3D drawing. No selection or tolerance prompt is required.

The command finds profile views in all drawing layouts and checks their associated TOP, MIDT and BUND profiles, including hidden profiles. Names are matched as case-insensitive tokens separated by spaces, underscores or hyphens, for example `004 MIDT` or `004_TOP`. External-reference block contents are outside the active drawing.

For each view/profile pair, the command checks the actual view start and end station independently. A missing length greater than zero and no more than **1.000 m** is extended to that exact boundary using `new elevation = old elevation + end grade * (new station - old station)`. A numerical station tolerance of 0.00000001 m handles coincident boundaries and floating-point rounding at 1 m. Ends already covering the view are left as they are; overhangs are not trimmed.

A gap over 1 m is left unchanged and reported in the command history with the view name/handle, profile name, START or END, and gap in metres. A valid opposite end can still be extended. Empty, unrelated, non-editable, referenced or non-layout profiles and view boundaries outside the parent alignment are reported without geometry changes. The drawing must use Civil drawing units in metres.

Profiles shared by several views are checked against their original station range. Eligible requests are combined into at most one extension per end; requests over tolerance cannot become eligible through a chain of smaller extensions. Extending a shared Civil profile affects every view displaying that profile.

Civil 3D adds the new endpoint PVI. The command verifies the straight extension's elevation and grade, existing PVIs, original geometry samples, and the identities and station bounds of existing vertical curves before committing that end. A failed native edit rolls back that end and is reported. The remaining eligible ends continue. One `UNDO` reverts the command. The final line reports view/profile counts, unique extended ends, already covered profile/view ends and skipped profile/view ends. Use F2 to inspect the detailed command history.

The command is part of the existing IntersectUtilities assembly and requires that library to be rebuilt and reloaded. It adds no persistent state or cleanup hooks.

Crash correction (2026-11-11): Windows application event 1026 traced the original command crash to `Feature.get_IsEditable`, through native `AeccFeatureUtils.isEditableDbFeature`. The command no longer calls this getter. It checks the reference/profile type and lets the transaction's write-open operation validate editing access. Progress is printed before each attempted endpoint extension. This correction identifies and removes the observed crash call; build and rule tests do not establish a live native geometry test.

API references, checked 2026-11-11 against Autodesk's documentation and the installed Civil 3D 2025 API:

- [Verified] [Profile.GradeAt](https://help.autodesk.com/cloudhelp/2026/ENU/Civil3D-API/files/html/0f748473-9b36-a8df-7caf-8f039b32ca8d.htm): instantaneous grade at a station; confidence 1.0.
- [Verified] [ProfilePVICollection.AddPVI](https://help.autodesk.com/cloudhelp/2026/ENU/Civil3D-API/files/html/dd4274b5-a665-eb50-89f3-1db8f249701c.htm): tangent PVI creation; alignment bounds apply; confidence 1.0.
- [Verified] [SettingsUnitZone.DrawingUnits](https://help.autodesk.com/cloudhelp/2026/ENU/Civil3D-API/files/html/55a64796-1017-6157-d12f-6ef934f3c739.htm): Civil drawing linear units; confidence 1.0.
