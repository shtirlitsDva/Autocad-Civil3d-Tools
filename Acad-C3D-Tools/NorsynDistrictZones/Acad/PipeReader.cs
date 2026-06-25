using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NorsynDistrictZones.Model;
using NorsynDistrictZones.Pricing;

using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using NhsSegmentType = NorsynHydraulicCalc.SegmentType;

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
                if (!PipeTypeTranslator.TryParseLayer(ent.Layer, out var sys, out var typ, out int dn))
                    continue;

                NetTopologySuite.Geometries.LineString? ls = ent switch
                {
                    AcPolyline lw => AcadNts.ToLineString(lw, xform),
                    Polyline2d p2 => AcadNts.ToLineString(p2, tx, xform),
                    _ => null,
                };
                if (ls is null || ls.Length <= 0) continue;

                // Prefer authoritative FL/SL from XData (written by the DimV2 export — P12).
                // Falls back to provisional Fordelingsledning when the export hasn't stamped it yet.
                (bool hasSeg, NhsSegmentType seg) = TryReadSegment(ent);

                result.Add(new PipeSegment(
                    sys, typ, dn,
                    hasSeg ? seg : NhsSegmentType.Fordelingsledning,
                    SegmentIsProvisional: !hasSeg,
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
    /// Read the FL/SL segment type from the pipe's XData if the export stamped it
    /// (schema: [regapp, pipeType, segmentType, dn]). This is the FL/SL-blocker fix
    /// contract — until the DimV2 write side ships, pipes have no such XData and
    /// pricing stays provisional.
    /// </summary>
    private static (bool ok, NhsSegmentType seg) TryReadSegment(Entity ent)
    {
        ResultBuffer? rb = ent.GetXDataForApplication(PipeIdentityApp);
        if (rb is null) return (false, default);
        foreach (TypedValue tv in rb)
        {
            if (tv.TypeCode != (short)DxfCode.ExtendedDataAsciiString) continue;
            string s = (tv.Value as string) ?? string.Empty;
            if (s.Equals("Stikledning", StringComparison.OrdinalIgnoreCase)) return (true, NhsSegmentType.Stikledning);
            if (s.Equals("Fordelingsledning", StringComparison.OrdinalIgnoreCase)) return (true, NhsSegmentType.Fordelingsledning);
        }
        return (false, default);
    }
}
