using System.Collections.Generic;

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
/// with <paramref name="Produkt"/> pinned on the junction.
/// </summary>
internal readonly record struct NdhConnectRequest(
    string MainHandle, string BranchHandle, bool BranchAtStart, NdhBranchOutlet Outlet, string Produkt);

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
/// connected end landed (success only).
/// </summary>
internal readonly record struct NdhConnectOutcome(
    NdhConnectStatus Status,
    int MainVertexIndex,
    double DeviationDeg,
    double EndMoveM,
    double LargestMoveM,
    double PortX,
    double PortY,
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
