using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NorsynDistrictZones.Model;
using NorsynDistrictZones.Pricing;

using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using NhsSegmentType = NorsynHydraulicCalc.SegmentType;
using NhsPipeType = NorsynHydraulicCalc.PipeType;

namespace NorsynDistrictZones.Acad;

/// <summary>
/// Reads pipes from attached Xref(s). Pipe identity is parsed from the PSv2
/// <c>FJV-</c> layer name (see <see cref="PipeTypeTranslator"/>); geometry is
/// transformed to WCS by the Xref's block transform. FL/SL is left provisional
/// until the DimV2 export carries it as XData (P12) — see the pricing blocker.
/// </summary>
internal static class PipeReader
{
    /// <summary>Names of every attached (top-level) Xref block definition in the drawing.</summary>
    public static List<string> ListXrefNames(Database db, Transaction tx)
    {
        var names = new List<string>();
        var bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId id in bt)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is BlockTableRecord btr && btr.IsFromExternalReference)
                names.Add(btr.Name);
        }
        return names.Distinct().OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Enumerate FJV- pipes from the chosen Xref(s) (null = all attached Xrefs) and
    /// project each to a <see cref="PipeSegment"/> with WCS geometry.
    /// </summary>
    public static List<PipeSegment> ReadFromXrefs(Database db, Transaction tx, ISet<string>? xrefNames)
    {
        var result = new List<PipeSegment>();
        var ms = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);

        foreach (ObjectId id in ms)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not BlockReference br) continue;
            if (tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) is not BlockTableRecord btr) continue;
            if (!btr.IsFromExternalReference) continue;
            if (xrefNames is not null && !xrefNames.Contains(btr.Name)) continue;

            Matrix3d xform = br.BlockTransform;
            foreach (ObjectId eid in btr)
            {
                Entity? ent = tx.GetObject(eid, OpenMode.ForRead) as Entity;
                if (ent is null) continue;
                // The layer only gates "is this an FJV pipe?" and yields the DN; the pipe's
                // type and FL/SL role come authoritatively from the export XData below.
                if (!PipeTypeTranslator.TryParseLayer(ent.Layer, out _, out _, out int dn))
                    continue;

                NetTopologySuite.Geometries.LineString? ls = ent switch
                {
                    AcPolyline lw => AcadNts.ToLineString(lw, xform),
                    Polyline2d p2 => AcadNts.ToLineString(p2, tx, xform),
                    _ => null,
                };
                if (ls is null || ls.Length <= 0) continue;

                // Authoritative NHS pipe type + FL/SL role from XData (written by the DimV2
                // export). These are the SINGLE TRUTH for pricing — NEVER re-derive either from
                // (layer-system, role): that collapse mis-prices Fællesstikledning (an SL-typed
                // pipe in the Fordelingsledning role) as the FL variant. An unstamped pipe is
                // unidentifiable: it carries null identity and the zone reports incomplete data.
                (bool hasId, NhsPipeType nhsType, NhsSegmentType seg) = TryReadIdentity(ent);

                result.Add(new PipeSegment(
                    Dn: dn,
                    Segment: hasId ? seg : (NhsSegmentType?)null,
                    NhsType: hasId ? nhsType : (NhsPipeType?)null,
                    Geometry: ls,
                    FullLength: ls.Length));
            }
        }
        return result;
    }

    /// <summary>
    /// The XData app name the DimV2 export uses to stamp authoritative NorsynHydraulicShared
    /// (NHS) pipe identity onto each exported pipe polyline.
    /// </summary>
    public const string PipeIdentityApp = "NORSYN_NHS_PIPE";

    /// <summary>
    /// Read the AUTHORITATIVE NHS identity the export stamped on the pipe
    /// (schema: [regapp, pipeTypeName, segmentTypeName, dn]). The pipe-type name is the
    /// exact NorsynHydraulicShared <see cref="NhsPipeType"/> that DimV2 sized and priced
    /// against (e.g. AluPEXSL for a Fællesstikledning); pricing keys on it directly so the
    /// two apps agree. Returns false (⇒ provisional) only if the XData is absent or either
    /// value is unparseable — pipe type and FL/SL role are recognised regardless of order.
    /// </summary>
    private static (bool ok, NhsPipeType type, NhsSegmentType seg) TryReadIdentity(Entity ent)
    {
        ResultBuffer? rb = ent.GetXDataForApplication(PipeIdentityApp);
        if (rb is null) return (false, default, default);

        NhsPipeType? type = null;
        NhsSegmentType? seg = null;
        foreach (TypedValue tv in rb)
        {
            if (tv.TypeCode != (short)DxfCode.ExtendedDataAsciiString) continue;
            string s = (tv.Value as string) ?? string.Empty;
            if (type is null && Enum.TryParse(s, ignoreCase: true, out NhsPipeType pt)) { type = pt; continue; }
            if (s.Equals("Stikledning", StringComparison.OrdinalIgnoreCase)) seg = NhsSegmentType.Stikledning;
            else if (s.Equals("Fordelingsledning", StringComparison.OrdinalIgnoreCase)) seg = NhsSegmentType.Fordelingsledning;
        }
        if (type is null || seg is null) return (false, default, default);
        return (true, type.Value, seg.Value);
    }
}
