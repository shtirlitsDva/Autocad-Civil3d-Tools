using Autodesk.AutoCAD.Geometry;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// The result of resolving a click in a profile view. Holds no open DBObject, so it stays valid after
/// the local read transaction closes, until FV_Fremtid's command writes the placement.
/// </summary>
/// <param name="AlignmentId">
/// The alignment in the længdeprofil drawing. An ObjectId is safe here: it belongs to an open
/// document and stays valid while that document is open, including while FV_Fremtid's command
/// builds the pipeline size array from it to resolve the pipe size at the station.
/// </param>
/// <param name="PlanPoint">The station's location in the plan drawing's WCS.</param>
/// <param name="Rotation">Alignment tangent at the station, radians, direction of increasing station.</param>
internal sealed record LongitudinalBlockPick(
    string AlignmentName,
    Oid AlignmentId,
    string ProfileViewName,
    double Station,
    double Elevation,
    Point3d PlanPoint,
    double Rotation);
