using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork
{
    // Outcome of trying to connect one 2D line to a 3D main pipe network.
    internal enum LERConnectionStatus
    {
        Connected,
        NoMain,
        NoIntersection,
        Degenerate
    }

    // Why a built (Connected) connector is flagged problematic. The connector is
    // still produced and previewed (in pink), but excluded from Apply.
    [System.Flags]
    internal enum LERConnectionError
    {
        None = 0,
        MissesMain = 1, // the connector's forward extension never reaches the main
        TooLong = 2     // the connector stub is far longer than the check distance
    }

    // A connected component of touching 3D lines. Members keep their real point
    // lists so XY-closest and Z-lift queries can run against the actual geometry.
    internal sealed class LERNetwork
    {
        public LERNetwork(int id, CadColor color)
        {
            Id = id;
            Color = color;
        }

        public int Id { get; }

        public CadColor Color { get; }

        public List<ObjectId> MemberIds { get; } = new();

        public List<IReadOnlyList<Point3d>> MemberPoints { get; } = new();
    }

    // The 3D main a 2D line was matched to, plus which of the 2D line's two
    // endpoints (the connecting end C) is closest to that main.
    internal sealed record LERMainAssignment(
        int NetworkId,
        bool ConnectAtEnd,
        double XyDistance);

    // The rebuilt geometry for one 2D line. NewPoints is null unless Status is
    // Connected. Error flags a Connected connector as problematic (see above).
    internal sealed record LERConnectionResult(
        ObjectId SourceId,
        IReadOnlyList<Point3d>? NewPoints,
        int NetworkId,
        LERConnectionStatus Status,
        LERConnectionError Error = LERConnectionError.None);
}
