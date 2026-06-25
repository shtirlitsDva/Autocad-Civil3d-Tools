using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Geometries;

using NorsynDistrictZones.Topology;

using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcTransparency = Autodesk.AutoCAD.Colors.Transparency;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using ColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;
using NsContainer = NorsynObjectsInterop.NorsynContainer;

namespace NorsynDistrictZones.Acad;

/// <summary>
/// Renders a <see cref="ZoneFace"/> as ONE NorsynContainer holding an MPolygon
/// (transparent solid fill, supports holes) + two MText labels (number/name, price).
/// Appearance is always (re)applied here from the face record (P0 spike: child colour
/// does not survive the container clone). <see cref="Update"/> rebuilds the children of
/// an EXISTING container in place — used by grip editing so ObjectIds stay stable.
/// </summary>
internal static class ZoneRenderer
{
    public const string ZoneLayer = "NDZ-ZONES";
    private static readonly double Tol = Tolerance.Global.EqualPoint;

    public static ObjectId Render(Database db, Transaction tx, BlockTableRecord ms, ZoneFace face, string priceText)
    {
        ZoneXData.EnsureRegApp(db, tx);
        EnsureLayer(db, tx, ZoneLayer);

        var (mp, top, price) = BuildChildren(face, priceText);
        var nc = new NsContainer();
        nc.Add(mp); nc.Add(top); nc.Add(price);
        ((Entity)nc).Layer = ZoneLayer;

        ObjectId id = ms.AppendEntity(nc);
        tx.AddNewlyCreatedDBObject(nc, true);
        ZoneXData.Write(nc, face);

        mp.Dispose(); top.Dispose(); price.Dispose();
        return id;
    }

    /// <summary>Rebuild an existing container's children + XData in place (same ObjectId).</summary>
    public static void Update(Database db, Transaction tx, NsContainer nc, ZoneFace face, string priceText)
    {
        ZoneXData.EnsureRegApp(db, tx);
        EnsureLayer(db, tx, ZoneLayer);

        var (mp, top, price) = BuildChildren(face, priceText);
        while (nc.Count > 0) nc.RemoveAt(0);
        nc.Add(mp); nc.Add(top); nc.Add(price);
        ((Entity)nc).Layer = ZoneLayer;
        ZoneXData.Write(nc, face);

        mp.Dispose(); top.Dispose(); price.Dispose();
    }

    /// <summary>
    /// Live grip-drag preview: swap ONLY the boundary MPolygon (child 0) on a
    /// transaction-less, non-database-resident container clone, leaving the labels in
    /// place. AutoCAD draws the clone as the drag image, so the boundary follows the
    /// cursor. The committed move (full rebuild + repricing on the real entity) happens
    /// later via <see cref="Update"/>. Render order is (MPolygon, title, price), so the
    /// boundary is always child 0.
    /// </summary>
    public static void ReplaceBoundaryPreview(NsContainer nc, ZoneFace face)
    {
        if (nc.Count == 0) return;
        nc.RemoveAt(0);
        MPolygon mp = BuildMPolygon(face);
        nc.Add(mp);
        mp.Dispose();
    }

    private static MPolygon BuildMPolygon(ZoneFace face)
    {
        AcColor color = ToAcColor(face.ColorArgb);

        var mp = new MPolygon();
        mp.AppendLoopFromBoundary(RingToPolyline(face.Polygon.ExteriorRing), false, Tol);
        for (int i = 0; i < face.Polygon.NumInteriorRings; i++)
            mp.AppendLoopFromBoundary(RingToPolyline(face.Polygon.GetInteriorRingN(i)), false, Tol);
        mp.SetPattern(HatchPatternType.PreDefined, "SOLID");
        mp.PatternColor = color;
        mp.Color = color;
        mp.Transparency = new AcTransparency(AlphaFromPercent(GlobalSettings.ZoneTransparencyPercent));
        mp.Layer = ZoneLayer;
        return mp;
    }

    /// <summary>AutoCAD transparency percent (0 = opaque, 90 = faintest) → alpha byte (255 = opaque).</summary>
    private static byte AlphaFromPercent(int percent) =>
        (byte)Math.Round(255.0 * (100 - Math.Clamp(percent, 0, 90)) / 100.0);

    private static (MPolygon, MText, MText) BuildChildren(ZoneFace face, string priceText)
    {
        MPolygon mp = BuildMPolygon(face);

        double h = LabelHeight(face);
        var anchor = new Point3d(face.LabelPoint.X, face.LabelPoint.Y, 0);
        var up = new Vector3d(0, h * 0.85, 0);

        MText top = MakeLabel(FormatTitle(face), h, anchor + up);
        MText price = MakeLabel(priceText, h, anchor - up);
        return (mp, top, price);
    }

    public static string FormatTitle(ZoneFace face) =>
        string.IsNullOrWhiteSpace(face.Name) ? $"#{face.Number}" : $"#{face.Number}  {face.Name}";

    /// <summary>
    /// Label text height: the user's global <see cref="GlobalSettings.LabelHeight"/> when set,
    /// otherwise auto — a fraction of the zone's smaller extent (floored). Shared by the live
    /// render and the AutoCAD export so both honour the remembered size.
    /// </summary>
    public static double LabelHeight(ZoneFace face)
    {
        if (GlobalSettings.LabelHeight is double h && h > 0) return h;
        Envelope env = face.Polygon.EnvelopeInternal;
        return Math.Max(Math.Min(env.Width, env.Height) / 12.0, 0.5);
    }

    private static MText MakeLabel(string text, double height, Point3d location) => new()
    {
        Contents = text,
        TextHeight = height,
        Location = location,
        Attachment = AttachmentPoint.MiddleCenter,
        Color = AcColor.FromColorIndex(ColorMethod.ByAci, 7),
        Layer = ZoneLayer,
    };

    private static AcPolyline RingToPolyline(LineString ring)
    {
        var pl = new AcPolyline();
        Coordinate[] c = ring.Coordinates;
        int count = c.Length > 1 && c[0].Equals2D(c[^1]) ? c.Length - 1 : c.Length;
        for (int i = 0; i < count; i++)
            pl.AddVertexAt(i, new Point2d(c[i].X, c[i].Y), 0, 0, 0);
        pl.Closed = true;
        return pl;
    }

    private static AcColor ToAcColor(int argb)
    {
        byte r = (byte)((argb >> 16) & 0xFF), g = (byte)((argb >> 8) & 0xFF), b = (byte)(argb & 0xFF);
        return AcColor.FromRgb(r, g, b);
    }

    private static void EnsureLayer(Database db, Transaction tx, string name)
    {
        var lt = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (lt.Has(name)) return;
        lt.UpgradeOpen();
        var rec = new LayerTableRecord { Name = name };
        lt.Add(rec);
        tx.AddNewlyCreatedDBObject(rec, true);
    }
}
