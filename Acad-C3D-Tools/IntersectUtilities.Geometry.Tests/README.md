# Tangent arc from two lines

`TANGENTARCFROMTWOLINES` is part of IntersectUtilities. Reload the rebuilt
IntersectUtilities plugin through the usual project loader before using it.

1. Enter the required **arc length**, in drawing units. Enter reuses the last value.
2. Select the first LINE away from the intersection, on the desired side of the corner.
3. Select the second LINE on the other side of that corner.

The command adds a minor circular ARC in the current space, with current database
defaults. The selection points determine the corner. The lines remain unchanged;
the command reports when either tangent point lies beyond a selected segment.
Coplanar lines in an arbitrary 3D plane are supported. Parallel, collinear,
zero-length, and skew lines, and picks at the intersection, are rejected.

If the angle between the selected rays is alpha, the arc sweep is pi - alpha
(radians), and its radius is arc length / sweep. This determines the radius;
the user does not enter a radius or fix a tangent point at an existing endpoint.

## Automated verification

The geometry source is linked into a small test project that does not load AutoCAD.
From the repository root:

```powershell
dotnet test .\Acad-C3D-Tools\IntersectUtilities.Geometry.Tests\IntersectUtilities.Geometry.Tests.csproj --nologo -v q
```

The suite covers four corners, acute/obtuse angles, endpoint/selection reversal,
line extensions, input failures, and 300 deterministic cases in a tilted plane at
survey coordinates. It independently checks radius, arc length, tangent
orthogonality, and the end point produced by rotating through the returned sweep.

## Civil 3D smoke checks

These interactive checks have not been run as part of this change:

- Draw perpendicular lines along the positive X and Y axes; use length 6.283185307179586.
  Pick on both positive rays. Expect radius 4, center (4, 4, 0), and endpoints (4, 0, 0)
  and (0, 4, 0). Properties should report the requested arc length.
- Extend the input lines across the origin and pick each of the other three corners.
  Verify placement follows the picks. Reverse the order of selection and repeat.
- Repeat at nonzero elevation, with a rotated UCS, and in a tilted plane/oblique view.
  Verify the ARC lies in the lines' plane and is tangent to both.
- Use short segments whose supports intersect outside the segments. Verify the extension
  message appears and that neither input LINE is trimmed or extended.
- Cancel each prompt, select the same line twice, and try parallel/skew lines. Verify
  that no ARC is added. A successful command should undo as a single operation.

## API references

Official Autodesk references consulted on 2026-09-30:

| API | Source | Reference edition |
| --- | --- | --- |
| ARC construction | [Arc constructors](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-__OVERLOADED_Arc_Autodesk_AutoCAD_DatabaseServices_Arc.html) | 2022 |
| Mapping the local arc plane into WCS | [Matrix3d.AlignCoordinateSystem](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_Geometry_Matrix3d_AlignCoordinateSystem_Point3d_Vector3d_Vector3d_Vector3d_Point3d_Vector3d_Vector3d_Vector3d.html) | 2022 |
| Resolving selection points against the view | [Curve.GetClosestPointTo](https://help.autodesk.com/cloudhelp/2022/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_Curve_GetClosestPointTo_Point3d_Vector3d__MarshalAsUnmanagedType_U1__bool.html) | 2022 |

Compilation against the installed AutoCAD 2025 assemblies verifies that these API
signatures are available in the target version. Interactive input and database
behavior still require the smoke checks above.
