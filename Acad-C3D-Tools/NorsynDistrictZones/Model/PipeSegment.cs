using IntersectUtilities.UtilsCommon.Enums;   // PSv2 enums

using NetTopologySuite.Geometries;

using NorsynHydraulicCalc;                     // SegmentType

namespace NorsynDistrictZones.Model;

/// <summary>
/// A single pipe read from the source Xref. Identity comes from the PSv2 layer
/// name; <see cref="Segment"/> (FL/SL) is provisional until the DimV2 export
/// carries it as XData (see the FL/SL pricing blocker — P12). Geometry is the
/// WCS NTS line; zone pricing clips it per face in memory, never in the drawing.
/// </summary>
public sealed record PipeSegment(
    PipeSystemEnum System,
    PipeTypeEnum PipeType,
    int Dn,
    SegmentType Segment,
    bool SegmentIsProvisional,
    Geometry Geometry,
    double FullLength);
