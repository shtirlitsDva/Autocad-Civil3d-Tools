using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

using NorsynDistrictZones.Pricing;
using NorsynDistrictZones.Topology;

using AcColor = Autodesk.AutoCAD.Colors.Color;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using ColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;

namespace NorsynDistrictZones.Acad;

/// <summary>
/// Exports the zones to two formats: plain AutoCAD geometry (closed polylines per
/// face + name/price MText, independent of the live container topology) and GeoJSON
/// (face polygons with number/name/price/area as feature properties). Prices are
/// recomputed live at export time (never read from a cache).
/// </summary>
internal static class ZoneExporter
{
    public const string ExportLayer = "NDZ-EXPORT";

    /// <summary>Draw each zone as a plain polyline + labels on <see cref="ExportLayer"/>. Returns count.</summary>
    public static int ExportToAutoCad(Database db, Transaction tx, BlockTableRecord ms)
    {
        EnsureLayer(db, tx, ExportLayer);
        var pipes = PipeReader.ReadFromXrefs(db, tx, null);
        var catalog = CatalogStore.GetActive(db);

        int n = 0;
        foreach (var (_, face) in ZoneReader.ReadAll(db, tx))
        {
            DrawRing(ms, tx, face.Polygon.ExteriorRing, face.ColorArgb);
            for (int i = 0; i < face.Polygon.NumInteriorRings; i++)
                DrawRing(ms, tx, face.Polygon.GetInteriorRingN(i), face.ColorArgb);

            string price = ZoneService.PriceFace(face, catalog, pipes, out _);
            double h = ZoneRenderer.LabelHeight(face); // honour the global text-size setting
            var anchor = new Point3d(face.LabelPoint.X, face.LabelPoint.Y, 0);
            var up = new Vector3d(0, h * 0.85, 0);
            AddText(ms, tx, ZoneRenderer.FormatTitle(face), h, anchor + up);
            AddText(ms, tx, price, h, anchor - up);
            n++;
        }
        return n;
    }

    /// <summary>Serialize all zones to a GeoJSON FeatureCollection string.</summary>
    public static string ExportGeoJson(Database db, Transaction tx)
    {
        var pipes = PipeReader.ReadFromXrefs(db, tx, null);
        var catalog = CatalogStore.GetActive(db);

        var fc = new FeatureCollection();
        foreach (var (_, face) in ZoneReader.ReadAll(db, tx))
        {
            ZoneService.PriceFace(face, catalog, pipes, out PriceCalculator.ZonePrice zp);
            var attrs = new AttributesTable
            {
                { "number", face.Number },
                { "name", face.Name ?? string.Empty },
                { "price_dkk", Math.Round(zp.Total, 0) },
                { "pipe_length_m", Math.Round(zp.LengthInside, 1) },
                { "pipe_count", zp.PipeCount },
                { "price_provisional", zp.AnyProvisional },
                { "area_m2", Math.Round(face.Polygon.Area, 1) },
            };
            fc.Add(new Feature(face.Polygon, attrs));
        }
        return new GeoJsonWriter().Write(fc);
    }

    private static void DrawRing(BlockTableRecord ms, Transaction tx, LineString ring, int argb)
    {
        var pl = new AcPolyline();
        Coordinate[] c = ring.Coordinates;
        int count = c.Length > 1 && c[0].Equals2D(c[^1]) ? c.Length - 1 : c.Length;
        for (int i = 0; i < count; i++)
            pl.AddVertexAt(i, new Point2d(c[i].X, c[i].Y), 0, 0, 0);
        pl.Closed = true;
        pl.Layer = ExportLayer;
        pl.Color = ToAcColor(argb);
        ms.AppendEntity(pl);
        tx.AddNewlyCreatedDBObject(pl, true);
    }

    private static void AddText(BlockTableRecord ms, Transaction tx, string text, double height, Point3d loc)
    {
        var t = new MText
        {
            Contents = text,
            TextHeight = height,
            Location = loc,
            Attachment = AttachmentPoint.MiddleCenter,
            Layer = ExportLayer,
        };
        ms.AppendEntity(t);
        tx.AddNewlyCreatedDBObject(t, true);
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
