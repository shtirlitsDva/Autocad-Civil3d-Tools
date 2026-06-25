using Autodesk.AutoCAD.DatabaseServices;

using NorsynDistrictZones.Topology;

namespace NorsynDistrictZones.Acad;

/// <summary>
/// Reconstructs zone faces from the NorsynContainers in model space (via their XData
/// WKT). This is the cross-session source of truth — it lets the reactor rebuild its
/// session on drawing open and lets grip editing find every face without an in-memory cache.
/// </summary>
internal static class ZoneReader
{
    public static List<(ObjectId Container, ZoneFace Face)> ReadAll(Database db, Transaction tx)
    {
        var list = new List<(ObjectId, ZoneFace)>();
        var ms = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity e) continue;
            if (e.GetRXClass().Name != "NorsynContainer") continue;
            ZoneFace? face = ZoneXData.ReadFace(e);
            if (face is not null) list.Add((id, face));
        }
        return list;
    }
}
