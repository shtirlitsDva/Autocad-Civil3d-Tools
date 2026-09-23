using System.Collections.Generic;

using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.NdhTrace;

/// <summary>How a branch leaves its main (NDH BranchOutlet).</summary>
internal enum NdhBranchOutlet
{
    /// <summary>At 90 degrees to the main.</summary>
    Perpendicular = 0,
    /// <summary>Along the main first (NDH BranchOutlet::ParallelFirst).</summary>
    AlongMain = 1,
}

/// <summary>
/// Connect the end of <paramref name="BranchHandle"/> (its first vertex when
/// <paramref name="BranchAtStart"/>, else its last) to <paramref name="MainHandle"/>,
/// with <paramref name="Produkt"/> pinned on the junction, the tee standing at
/// <paramref name="Site"/> - the seat the main was routed around
/// (<see cref="JunctionSeating"/>).
/// </summary>
internal readonly record struct NdhConnectRequest(
    string MainHandle, string BranchHandle, bool BranchAtStart, NdhBranchOutlet Outlet, string Produkt,
    Point2d Site);

/// <summary>
/// How the branch took the tee's seat (kNsDhCorner*). The tee holds its seat,
/// so the branch gives: its first corner shifts when it stands within the
/// drawing's largest corner shift of the square line, and otherwise the first
/// leg takes an S-offset and the corner stands as drawn.
/// </summary>
internal enum NdhCornerCorrection
{
    Shifted = 0,
    Offset = 1,
    /// <summary>An S-offset was due but could not be laid in the first leg; the corner moved anyway.</summary>
    ShiftedForWantOfRoom = 2,
}

/// <summary>NsDh_ConnectBranch status codes (kNsDhConnect*).</summary>
internal enum NdhConnectStatus
{
    Ok = 0,
    BadArgs = -1,
    NotAPipeline = -2,
    PortOnArc = -3,
    PortOnCorner = -4,
    PortOnMainEnd = -5,
    PortOccupied = -6,
    BranchEndOccupied = -7,
    PipeTypeMismatch = -8,
    NoSuchProdukt = -9,
    ProduktRefused = -10,
    NoLawfulSquare = -11,
    PortOnVerticalElbow = -12,
    Failed = -13,
}

/// <summary>
/// What NDH did with a connect request. <paramref name="DeviationDeg"/> is how
/// far the legacy branch's first leg stood off the required direction;
/// <paramref name="EndMoveM"/> how far the connected end moved onto the main's
/// centreline; <paramref name="LargestMoveM"/> the largest move of any other
/// branch vertex. <paramref name="PortX"/>/<paramref name="PortY"/> is where the
/// connected end landed, <paramref name="Correction"/> how the branch took the
/// seat and <paramref name="CornerOffsetM"/> how far its first corner was drawn
/// off the square line (success only).
/// </summary>
internal readonly record struct NdhConnectOutcome(
    NdhConnectStatus Status,
    int MainVertexIndex,
    double DeviationDeg,
    double EndMoveM,
    double LargestMoveM,
    double PortX,
    double PortY,
    NdhCornerCorrection Correction,
    double CornerOffsetM,
    string Detail)
{
    public bool Success => Status == NdhConnectStatus.Ok;
}

/// <summary>One connection of a pipeline as NDH reads it back.</summary>
internal readonly record struct NdhConnectionRow(
    int RouteIndex,
    bool IsHost,
    NdhBranchOutlet Outlet,
    double Station,
    double X,
    double Y,
    string PartnerHandle,
    string PinnedProdukt,
    string ResolvedProdukt);

/// <summary>Connects branch pipelines to their mains in the working drawing.</summary>
internal interface INdhConnector
{
    NdhConnectOutcome Connect(NdhConnectRequest request);

    /// <summary>Every connection of the pipeline; throws when it cannot be read.</summary>
    IReadOnlyList<NdhConnectionRow> ReadConnections(string pipelineHandle);
}
