using System.Collections.Generic;

using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.SplitStik
{
    /// <summary>
    /// The subset of a DimensioneringV2 .d2r result file that SPLITSTIK needs.
    /// Deliberately not a mirror of DimensioneringV2's HydraulicNetworkDto — only
    /// the graph topology, the geometry and the pipe dimension are modelled.
    /// </summary>
    internal sealed record D2rNetwork(
        int FormatVersion,
        string? Id,
        string? CalculatedAt,
        IReadOnlyList<D2rGraph> Graphs);

    internal sealed record D2rGraph(
        IReadOnlyList<D2rNode> Vertices,
        IReadOnlyList<D2rEdge> Edges);

    internal sealed record D2rNode(
        int Index,
        Point2d Location,
        bool IsRootNode,
        bool IsBuildingNode,
        int Degree,
        string NodeId);

    internal sealed record D2rEdge(
        int Index,
        int SourceIndex,
        int TargetIndex,
        D2rFeature Feature);

    /// <summary>
    /// The pipe dimension. <see cref="FamilyName"/> is NorsynHydraulicShared's
    /// PipeType.ToString() (e.g. "Stål", "AluPEXSL") — see
    /// <see cref="SplitStikWriter.TranslateFamilyNameToSystem"/>.
    /// </summary>
    internal sealed record D2rDim(
        string FamilyName,
        string Size,
        string DimName,
        int NominalDiameter);

    /// <summary>
    /// One AnalysisFeature. <see cref="Type"/> is kept as the raw wire tag so an
    /// unrecognised future tag can be reported instead of crashing the import.
    /// <see cref="Dim"/> is null when the feature was never sized — in the .d2r
    /// that shows up as an entirely absent "Dim" key, not as IsNone: true.
    /// </summary>
    internal sealed record D2rFeature(
        string Type,
        Point2d[] Geometry,
        D2rDim? Dim,
        int SubGraphId,
        double Length)
    {
        public bool IsMainLine => Type is "FL" or "TL";
        public bool IsServiceLine => Type is "SL" or "FS";
        public bool IsKnownType => IsMainLine || IsServiceLine;
    }
}
