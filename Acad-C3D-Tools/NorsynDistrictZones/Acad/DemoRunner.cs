using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NetTopologySuite.Geometries;

using NorsynDistrictZones.Editing;
using NorsynDistrictZones.Model;
using NorsynDistrictZones.Pricing;
using NorsynDistrictZones.Topology;

using NhsSegmentType = NorsynHydraulicCalc.SegmentType;
using NhsPipeType = NorsynHydraulicCalc.PipeType;

namespace NorsynDistrictZones.Acad;

/// <summary>Dev harness: run the demo render against a given Database and RETURN the outcome (incl. errors).</summary>
public static class DemoRunner
{
    public static string Run(Database db)
    {
        try
        {
            using Transaction tx = db.TransactionManager.StartTransaction();
            var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
            var poly = gf.CreatePolygon(new[]
            {
                new Coordinate(0, 0), new Coordinate(60, 0),
                new Coordinate(60, 40), new Coordinate(0, 40), new Coordinate(0, 0),
            });
            var pipeLine = gf.CreateLineString(new[] { new Coordinate(-10, 20), new Coordinate(70, 20) });
            var pipes = new List<PipeSegment>
            {
                new(50, NhsSegmentType.Fordelingsledning, NhsPipeType.Stål, pipeLine, pipeLine.Length),
            };
            var catalog = PipePriceCatalog.SeedFromDefaults();
            var ms = (BlockTableRecord)tx.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

            var face = ZoneService.CreateAndRender(
                db, tx, ms, poly, catalog, pipes, Guid.NewGuid, new Random());
            tx.Commit();
            return $"OK zone #{face.Number}";
        }
        catch (System.Exception ex)
        {
            return "ERR: " + ex;
        }
    }

    /// <summary>
    /// Grip-adjacency test: render two zones sharing the vertex (25,30), then move that
    /// shared vertex to (35,45) and confirm BOTH faces adapt (shared-edge behaviour).
    /// </summary>
    public static string GripDemo(Database db)
    {
        try
        {
            var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
            Polygon Rect(double x0, double y0, double x1, double y1) => gf.CreatePolygon(new[]
            {
                new Coordinate(x0, y0), new Coordinate(x1, y0),
                new Coordinate(x1, y1), new Coordinate(x0, y1), new Coordinate(x0, y0),
            });

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                var ms = (BlockTableRecord)tx.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                var left = new ZoneFace(new ZoneId(Guid.NewGuid()), Rect(0, 0, 25, 30))
                { Number = 1, ColorArgb = unchecked((int)0xFF3060A0) };
                var right = new ZoneFace(new ZoneId(Guid.NewGuid()), Rect(25, 0, 50, 30))
                { Number = 2, ColorArgb = unchecked((int)0xFF30A060) };
                ZoneRenderer.Render(db, tx, ms, left, "left");
                ZoneRenderer.Render(db, tx, ms, right, "right");
                tx.Commit();
            }

            int changed = ZoneGripOverrule.ApplyMovesAllContainers(db,
                new List<(Point3d, Point3d)> { (new Point3d(25, 30, 0), new Point3d(35, 45, 0)) });

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                var all = ZoneReader.ReadAll(db, tx);
                string s = string.Join(" || ", all.Select(a =>
                    $"#{a.Face.Number} area={a.Face.Polygon.Area:N0} hasMovedVtx={a.Face.Polygon.Coordinates.Any(c => Math.Abs(c.X - 35) < 1e-6 && Math.Abs(c.Y - 45) < 1e-6)}"));
                tx.Commit();
                return $"changed={changed}; {s}";
            }
        }
        catch (System.Exception ex)
        {
            return "ERR: " + ex;
        }
    }
}
