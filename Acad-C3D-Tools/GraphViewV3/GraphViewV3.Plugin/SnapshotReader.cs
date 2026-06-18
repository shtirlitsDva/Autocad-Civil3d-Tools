using Autodesk.AutoCAD.DatabaseServices;

using GraphViewV3.Core;

using IntersectUtilities;                    // ComponentSchedule.GetPipeTypeEnum extension
using IntersectUtilities.UtilsCommon.Enums;  // PipeSystemEnum, PipeTypeEnum (Ukendt = not FJV)

using PipeSchedule = IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace GraphViewV3;

/// <summary>
/// Reads the FJV network from a drawing into Core DTOs. Runs on the AutoCAD main thread
/// (called from the Idle pump under a read transaction), gathers the data FAST, then lets
/// AutoCAD go — all graph building happens later on a background thread from the snapshot.
/// Connectivity ports: pipe vertices/endpoints, and component MuffeIntern* nested-block
/// positions. Welded studs (afgrstuds / sh-lige / sh-vinkel) are tagged so the Core can
/// connect their main port mid-span along a pipe.
/// </summary>
internal static class SnapshotReader
{
    public static NetworkSnapshot Read(Database db, Transaction tr)
    {
        var pipes = new List<PipeDto>();
        var components = new List<ComponentDto>();

        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId id in ms)
        {
            var ent = tr.GetObject(id, OpenMode.ForRead);
            switch (ent)
            {
                // Authoritative FJV detection: a pipe's layer maps to a known pipe system.
                case Polyline pl when IsFjvPipe(pl):
                {
                    var (system, size) = FjvLayer.Parse(pl.Layer);
                    var verts = ReadVertices(pl);
                    var s = pl.StartPoint;
                    var e = pl.EndPoint;
                    pipes.Add(new PipeDto(
                        pl.Handle.ToString(), pl.Layer, system, size,
                        new Pt(s.X, s.Y), new Pt(e.X, e.Y), pl.Length, verts));
                    break;
                }
                // Authoritative FJV detection: a component block resolves to a known pipe type.
                case BlockReference br when IsFjvComponent(br):
                {
                    var muffe = ReadMuffePorts(br, tr);
                    string name = EffectiveName(br, tr);
                    bool weldable = IsWeldable(name);
                    Pt? weldPort = weldable ? MainPort(muffe) : null;

                    components.Add(new ComponentDto(
                        br.Handle.ToString(), name,
                        new Pt(br.Position.X, br.Position.Y),
                        muffe.Select(m => m.P).ToList(),
                        weldable, weldPort));
                    break;
                }
            }
        }

        return new NetworkSnapshot(pipes, components);
    }

    // GetPipeSystem returns Ukendt for non-FJV layers; that is the authoritative "not a pipe".
    private static bool IsFjvPipe(Polyline pl)
    {
        try { return PipeSchedule.GetPipeSystem(pl) != PipeSystemEnum.Ukendt; }
        catch { return false; }
    }

    // GetPipeTypeEnum returns Ukendt for non-FJV blocks; that is the authoritative "not a component".
    private static bool IsFjvComponent(BlockReference br)
    {
        try { return br.GetPipeTypeEnum() != PipeTypeEnum.Ukendt; }
        catch { return false; }
    }

    private static List<Pt> ReadVertices(Polyline pl)
    {
        int n = pl.NumberOfVertices;
        var verts = new List<Pt>(n);
        for (int i = 0; i < n; i++)
        {
            var p = pl.GetPoint2dAt(i);
            verts.Add(new Pt(p.X, p.Y));
        }
        return verts;
    }

    private static string EffectiveName(BlockReference br, Transaction tr)
    {
        try
        {
            if (br.IsDynamicBlock)
                return ((BlockTableRecord)tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name;
        }
        catch { }
        return br.Name;
    }

    private static List<(string Name, Pt P)> ReadMuffePorts(BlockReference br, Transaction tr)
    {
        var ports = new List<(string, Pt)>();
        BlockTableRecord btr;
        try { btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead); }
        catch { return ports; }

        var xform = br.BlockTransform;
        foreach (ObjectId nid in btr)
        {
            var nb = tr.GetObject(nid, OpenMode.ForRead) as BlockReference;
            if (nb == null) continue;
            if (!IsMuffe(nb.Name)) continue;
            var p = nb.Position.TransformBy(xform);
            ports.Add((nb.Name, new Pt(p.X, p.Y)));
        }
        return ports;
    }

    private static bool IsMuffe(string name)
    {
        var n = Normalize(name);
        return n.StartsWith("muffeintern");
    }

    private static bool IsWeldable(string effectiveName)
    {
        var n = Normalize(effectiveName);
        return n.Contains("afgrstuds") || n.Contains("afgrenings")
            || n.StartsWith("shlige") || n.StartsWith("shvinkel");
    }

    /// <summary>The welded main port: the MuffeIntern-MAIN if present, else the single port.</summary>
    private static Pt? MainPort(List<(string Name, Pt P)> muffe)
    {
        foreach (var m in muffe)
            if (Normalize(m.Name).Contains("main")) return m.P;
        return muffe.Count > 0 ? muffe[0].P : null;
    }

    private static string Normalize(string name) =>
        name.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
}
