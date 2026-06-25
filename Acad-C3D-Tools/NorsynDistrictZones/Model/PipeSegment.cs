using NetTopologySuite.Geometries;

using NorsynHydraulicCalc;                     // SegmentType
using NhsPipeType = NorsynHydraulicCalc.PipeType;

namespace NorsynDistrictZones.Model;

/// <summary>
/// A single pipe read from the source Xref. <see cref="NhsType"/> (pipe type) and
/// <see cref="Segment"/> (FL/SL role) are the AUTHORITATIVE NorsynHydraulicShared identity
/// stamped on the pipe by the DimV2 export (XData <c>NORSYN_NHS_PIPE</c>); pricing keys on
/// them directly. They are two INDEPENDENT axes — a Fællesstikledning is an SL-typed pipe in
/// the Fordelingsledning role — so neither is ever derived from the other or from the layer.
/// Both are null exactly when the export never stamped the pipe; such a pipe is
/// UNIDENTIFIABLE and the zone reports incomplete data (no price is guessed). Geometry is the
/// WCS NTS line; zone pricing clips it per face in memory.
/// </summary>
public sealed record PipeSegment(
    int Dn,
    SegmentType? Segment,
    NhsPipeType? NhsType,
    Geometry Geometry,
    double FullLength);
