using System.IO;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

using PipeSchedule = IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities.MPE.NSAlignmentCrawl;

/// <summary>
/// Reads the FJV network out of the FV_Fremtid xref (attached in the host/alignments drawing)
/// into transaction-free POCOs. Everything is brought to host WCS so the crawl graph and the
/// output polyline live in the same coordinate system as the host model space.
///
/// FJV detection mirrors GraphViewV3.SnapshotReader: a polyline is a pipe iff its layer maps to
/// a known pipe system; a block is a component iff it resolves to a known pipe type. Connection
/// ports are the nested MuffeIntern* block positions.
/// </summary>
internal static class NSAlignmentCrawlNetworkReader
{
    public static bool TryRead(Database hostDb, Transaction tr, string xrefName, out NSAlignmentCrawlSnapshot snapshot, out string error)
    {
        snapshot = new NSAlignmentCrawlSnapshot();
        error = string.Empty;

        BlockReference? xrefBr = FindXref(hostDb, tr, xrefName);
        if (xrefBr is null)
        {
            error = $"Fandt ikke xref '{xrefName}' i tegningen. Er den vedhæftet?";
            return false;
        }

        Matrix3d xform = xrefBr.BlockTransform;
        WarnIfNonUniform(xform);

        BlockTableRecord xrefBtr = (BlockTableRecord)tr.GetObject(xrefBr.BlockTableRecord, OpenMode.ForRead);
        foreach (ObjectId id in xrefBtr)
        {
            DBObject ent = tr.GetObject(id, OpenMode.ForRead);
            switch (ent)
            {
                case Polyline pl when IsFjvPipe(pl):
                    snapshot.Pipes.Add(ReadPipe(pl, xform));
                    break;

                // A block is a crawl component iff it exposes MuffeIntern ports. This is layer- and
                // name-agnostic (the component blocks are anonymous dynamic blocks on layer 0) and
                // works through the xref, unlike the dynamic-property/CSV classification.
                case BlockReference br:
                    List<Point2d> ports = ReadMuffePorts(br, tr, xform);
                    if (ports.Count >= 1)
                    {
                        Point3d center = br.Position.TransformBy(xform);
                        List<IReadOnlyList<(Point2d Pt, double Bulge)>> centerlines = ReadCenterlines(br, tr, xform);
                        snapshot.Components.Add(new CrawlComponent(
                            new Point2d(center.X, center.Y), ports, centerlines, IsWeldStud(br, tr)));
                    }

                    break;
            }
        }

        return true;
    }

    private static BlockReference? FindXref(Database hostDb, Transaction tr, string xrefName)
    {
        BlockTable bt = (BlockTable)tr.GetObject(hostDb.BlockTableId, OpenMode.ForRead);
        BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not BlockReference br)
            {
                continue;
            }

            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
            if (IsTargetXref(btr, xrefName))
            {
                return br;
            }
        }

        return null;
    }

    // The xref name carries project prefixes/suffixes (e.g. "1234-05_FV_Fremtid"), so match on a
    // case-insensitive substring rather than the bare name.
    private static bool IsTargetXref(BlockTableRecord btr, string xrefName)
    {
        if (!btr.IsFromExternalReference)
        {
            return false;
        }

        if (Contains(btr.Name, xrefName))
        {
            return true;
        }

        try
        {
            Database? xdb = btr.GetXrefDatabase(false);
            if (xdb?.Filename is string filename)
            {
                return Contains(Path.GetFileNameWithoutExtension(filename), xrefName);
            }
        }
        catch
        {
            // Unresolved/unloaded xref — fall through to name mismatch.
        }

        return false;
    }

    private static bool Contains(string? haystack, string needle) =>
        haystack is not null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static CrawlPipe ReadPipe(Polyline pl, Matrix3d xform)
    {
        int n = pl.NumberOfVertices;
        List<(Point2d Pt, double Bulge)> verts = new(n);
        for (int i = 0; i < n; i++)
        {
            Point3d p = pl.GetPoint3dAt(i).TransformBy(xform);
            // Bulge is rotation- and uniform-scale-invariant, so it survives a clean xref transform.
            verts.Add((new Point2d(p.X, p.Y), pl.GetBulgeAt(i)));
        }

        return new CrawlPipe(verts);
    }

    private static List<Point2d> ReadMuffePorts(BlockReference br, Transaction tr, Matrix3d xform)
    {
        List<Point2d> ports = [];
        BlockTableRecord btr;
        try
        {
            btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
        }
        catch
        {
            return ports;
        }

        Matrix3d compXform = br.BlockTransform;
        foreach (ObjectId nid in btr)
        {
            if (tr.GetObject(nid, OpenMode.ForRead) is not BlockReference nb)
            {
                continue;
            }

            if (!IsMuffe(nb.Name))
            {
                continue;
            }

            // Three nested frames: muffe-local → component → xref → host WCS.
            Point3d p = nb.Position.TransformBy(compXform).TransformBy(xform);
            ports.Add(new Point2d(p.X, p.Y));
        }

        return ports;
    }

    /// <summary>
    /// Reads the component's own centreline geometry — the Lines/Arcs/Polylines drawn on a layer whose
    /// name contains "komponent" — as vertex+bulge chains in host WCS. This is the geometry that
    /// connects the block's ports; for BUERØR it is the real arc (the straight chord lives on layer 0
    /// and is deliberately excluded). Arc/polyline bulges are flipped when the instance is mirrored.
    /// </summary>
    private static List<IReadOnlyList<(Point2d Pt, double Bulge)>> ReadCenterlines(BlockReference comp, Transaction tr, Matrix3d xform)
    {
        var result = new List<IReadOnlyList<(Point2d, double)>>();
        BlockTableRecord btr;
        try
        {
            btr = (BlockTableRecord)tr.GetObject(comp.BlockTableRecord, OpenMode.ForRead);
        }
        catch
        {
            return result;
        }

        Matrix3d compXform = comp.BlockTransform;
        double sign = DetSign(compXform) * DetSign(xform) < 0 ? -1.0 : 1.0; // mirrored instance flips bulge

        Point2d ToWcs(Point3d p)
        {
            Point3d w = p.TransformBy(compXform).TransformBy(xform);
            return new Point2d(w.X, w.Y);
        }

        foreach (ObjectId id in btr)
        {
            DBObject e = tr.GetObject(id, OpenMode.ForRead);
            if (e is not Entity ent || !IsKomponentLayer(ent.Layer))
            {
                continue;
            }

            switch (e)
            {
                case Line ln:
                    result.Add([(ToWcs(ln.StartPoint), 0.0), (ToWcs(ln.EndPoint), 0.0)]);
                    break;

                case Arc arc:
                {
                    double sweep = arc.EndAngle - arc.StartAngle;
                    if (sweep < 0)
                    {
                        sweep += 2 * Math.PI;
                    }

                    double bulge = sign * Math.Tan(sweep / 4.0);
                    result.Add([(ToWcs(arc.StartPoint), bulge), (ToWcs(arc.EndPoint), 0.0)]);
                    break;
                }

                case Polyline pl:
                {
                    var verts = new List<(Point2d, double)>(pl.NumberOfVertices);
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        verts.Add((ToWcs(pl.GetPoint3dAt(i)), sign * pl.GetBulgeAt(i)));
                    }

                    if (verts.Count >= 2)
                    {
                        result.Add(verts);
                    }

                    break;
                }
            }
        }

        return result;
    }

    private static bool IsKomponentLayer(string layer)
    {
        int pipe = layer.LastIndexOf('|');
        string local = pipe >= 0 ? layer[(pipe + 1)..] : layer;
        return local.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant().Contains("komponent");
    }

    private static double DetSign(Matrix3d m)
    {
        CoordinateSystem3d cs = m.CoordinateSystem3d;
        return cs.Xaxis.CrossProduct(cs.Yaxis).DotProduct(cs.Zaxis) >= 0 ? 1.0 : -1.0;
    }

    private static void WarnIfNonUniform(Matrix3d xform)
    {
        // Bulges are only preserved under uniform, non-mirrored scale. Rotation + translation are fine.
        double sx = xform.CoordinateSystem3d.Xaxis.Length;
        double sy = xform.CoordinateSystem3d.Yaxis.Length;
        bool uniform = Math.Abs(sx - sy) < 1e-6;
        bool mirrored = xform.CoordinateSystem3d.Xaxis
            .CrossProduct(xform.CoordinateSystem3d.Yaxis)
            .DotProduct(xform.CoordinateSystem3d.Zaxis) < 0.0;
        if (!uniform || mirrored)
        {
            UtilsCommon.Utils.prdDbg(
                "NSAlignmentCrawl: FV_Fremtid xref has non-uniform or mirrored scale — arc geometry may be inaccurate.");
        }
    }

    private static bool IsFjvPipe(Polyline pl)
    {
        try
        {
            return PipeSchedule.GetPipeSystem(pl) != PipeSystemEnum.Ukendt;
        }
        catch
        {
            return false;
        }
    }

    // Nested block names are xref-qualified inside an xref ("FV_Fremtid_…|MuffeIntern"), so strip
    // everything up to the last '|' before testing.
    private static bool IsMuffe(string name)
    {
        int pipe = name.LastIndexOf('|');
        string local = pipe >= 0 ? name[(pipe + 1)..] : name;
        return Normalize(local).StartsWith("muffeintern");
    }

    // Weld-on studs (afgreningsstuds / svejsehoved) attach to a carrier pipe mid-span rather than at
    // a pipe endpoint, so they need the dedicated weld wiring in the graph builder. Mirrors the
    // weldable set in GraphViewV3.SnapshotReader.
    private static bool IsWeldStud(BlockReference br, Transaction tr)
    {
        string raw = EffectiveName(br, tr);
        int pipe = raw.LastIndexOf('|');
        string name = Normalize(pipe >= 0 ? raw[(pipe + 1)..] : raw);
        return name.Contains("afgrstuds") || name.Contains("afgrenings")
            || name.StartsWith("shlige") || name.StartsWith("shvinkel");
    }

    private static string EffectiveName(BlockReference br, Transaction tr)
    {
        try
        {
            if (br.IsDynamicBlock)
            {
                return ((BlockTableRecord)tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name;
            }
        }
        catch
        {
            // Fall back to the reference name below.
        }

        return br.Name;
    }

    private static string Normalize(string name) =>
        name.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
}
