using System.Text;

using Autodesk.AutoCAD.DatabaseServices;

using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using NorsynDistrictZones.Topology;

namespace NorsynDistrictZones.Acad;

/// <summary>
/// Persists a zone's full state — identity (GUID, number, name, stable colour) AND
/// geometry (polygon WKT, chunked into ≤255-char XData strings) — on its rendered
/// container. This makes the container self-describing: zones can be reconstructed
/// on drawing open (cross-session persistence) and grip-edited without any external
/// store. Appearance is re-applied from here on every rebuild (P0 spike: child
/// colour does not survive the container clone).
/// </summary>
internal static class ZoneXData
{
    public const string AppName = "NDZ_ZONE";
    private const int ChunkSize = 250;

    public static void EnsureRegApp(Database db, Transaction tx)
    {
        var rat = (RegAppTable)tx.GetObject(db.RegAppTableId, OpenMode.ForWrite);
        if (!rat.Has(AppName))
        {
            var rec = new RegAppTableRecord { Name = AppName };
            rat.Add(rec);
            tx.AddNewlyCreatedDBObject(rec, true);
        }
    }

    public static void Write(Entity ent, ZoneFace face)
    {
        string wkt = new WKTWriter().Write(face.Polygon);
        var items = new List<TypedValue>
        {
            new((int)DxfCode.ExtendedDataRegAppName, AppName),
            new((int)DxfCode.ExtendedDataAsciiString, face.Id.ToString()),
            new((int)DxfCode.ExtendedDataInteger32, face.Number),
            new((int)DxfCode.ExtendedDataInteger32, face.ColorArgb),
            new((int)DxfCode.ExtendedDataAsciiString, face.Name ?? string.Empty),
        };
        for (int i = 0; i < wkt.Length; i += ChunkSize)
            items.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString,
                wkt.Substring(i, Math.Min(ChunkSize, wkt.Length - i))));

        using var rb = new ResultBuffer(items.ToArray());
        ent.XData = rb;
    }

    /// <summary>Identity only (cheap) — used where geometry isn't needed.</summary>
    public static ZoneRecord? Read(Entity ent)
    {
        ResultBuffer? rb = ent.GetXDataForApplication(AppName);
        if (rb is null) return null;
        TypedValue[] d = rb.AsArray();
        if (d.Length < 5 || !ZoneId.TryParse(d[1].Value as string, out var id)) return null;
        return new ZoneRecord(id, Convert.ToInt32(d[2].Value), d[4].Value as string ?? string.Empty,
            Convert.ToInt32(d[3].Value));
    }

    /// <summary>Full reconstruction: identity + geometry (from the chunked WKT). Null if not a zone.</summary>
    public static ZoneFace? ReadFace(Entity ent)
    {
        ResultBuffer? rb = ent.GetXDataForApplication(AppName);
        if (rb is null) return null;
        TypedValue[] d = rb.AsArray();
        if (d.Length < 5 || !ZoneId.TryParse(d[1].Value as string, out var id)) return null;

        int number = Convert.ToInt32(d[2].Value);
        int color = Convert.ToInt32(d[3].Value);
        string name = d[4].Value as string ?? string.Empty;

        var sb = new StringBuilder();
        for (int i = 5; i < d.Length; i++)
            if (d[i].TypeCode == (short)DxfCode.ExtendedDataAsciiString) sb.Append(d[i].Value as string);
        if (sb.Length == 0) return null;

        Polygon? poly;
        try { poly = new WKTReader().Read(sb.ToString()) as Polygon; }
        catch { return null; }
        if (poly is null) return null;

        return new ZoneFace(id, poly) { Number = number, Name = name, ColorArgb = color };
    }
}

/// <summary>The decoded XData identity of a rendered zone (geometry excluded).</summary>
internal readonly record struct ZoneRecord(ZoneId Id, int Number, string Name, int ColorArgb);
