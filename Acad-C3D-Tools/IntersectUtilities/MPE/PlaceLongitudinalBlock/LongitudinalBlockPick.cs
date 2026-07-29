using Autodesk.AutoCAD.Geometry;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// The result of resolving a click in a profile view. Holds no open DBObject, so it stays valid after
/// the local read transaction closes and while FV_Fremtid is being opened.
/// </summary>
/// <param name="AlignmentId">
/// The alignment in the ACTIVE længdeprofil drawing. An ObjectId is safe here — unlike the side
/// database's, this one belongs to the open document and stays valid for the whole command. It is
/// needed to build the pipeline size array, which resolves the pipe size at the station.
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
