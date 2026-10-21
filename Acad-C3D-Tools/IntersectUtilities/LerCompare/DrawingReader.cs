using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Aec.PropertyData.DatabaseServices;

namespace IntersectUtilities.LerCompare;

public static class DrawingReader
{
    public static Snapshot FileSnapshot(string path, string coverageLayer)
    {
        using var db = new Database(false, true);
        db.ReadDwgFile(path, FileShare.ReadWrite, true, "");
        db.CloseInput(true);
        return Read(db, path, coverageLayer);
    }
    public static Snapshot Read(Database db, string path, string coverageLayer)
    {
        var snapshot = new Snapshot { Path = path, Units = db.Insunits.ToString(), MetersPerUnit = UnitScale(db.Insunits), CoverageLayer = coverageLayer };
        using var tx = db.TransactionManager.StartTransaction();
        var bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity e)
                continue;
            if (e is Polyline or Polyline2d or Polyline3d)
            {
                var pipe = new Pipe { Handle = e.Handle.ToString(), Layer = e.Layer, EntityType = e.GetType().Name };
                if (e is Polyline p)
                {
                    pipe.Closed = p.Closed;
                    for (int i = 0; i < p.NumberOfVertices; i++)
                    {
                        var v = p.GetPoint3dAt(i);
                        pipe.Points.Add(new(v.X, v.Y));
                        pipe.Bulges.Add(p.GetBulgeAt(i));
                    }
                    if (Math.Abs(p.Normal.Z - 1) > 1e-8 || Math.Abs(p.Normal.X) > 1e-8 || Math.Abs(p.Normal.Y) > 1e-8)
                        pipe.Errors.Add("Non-horizontal polyline: arc projection needs review.");
                }
                else if (e is Polyline2d p2)
                {
                    pipe.Closed = p2.Closed;
                    foreach (ObjectId vid in p2)
                    {
                        var v = (Vertex2d)tx.GetObject(vid, OpenMode.ForRead);
                        var w = p2.VertexPosition(v);
                        pipe.Points.Add(new(w.X, w.Y));
                        pipe.Bulges.Add(v.Bulge);
                    }
                    if (p2.PolyType != Poly2dType.SimplePoly)
                        pipe.Errors.Add("Fitted legacy polyline: control vertices need review.");
                }
                else if (e is Polyline3d p3)
                {
                    pipe.Closed = p3.Closed;
                    foreach (ObjectId vid in p3)
                    {
                        var v = (PolylineVertex3d)tx.GetObject(vid, OpenMode.ForRead);
                        pipe.Points.Add(new(v.Position.X, v.Position.Y));
                        pipe.Bulges.Add(0);
                    }
                    if (p3.PolyType != Poly3dType.SimplePoly)
                        pipe.Errors.Add("Fitted 3D polyline: control vertices need review.");
                }
                ReadProperties(e, tx, pipe);
                if (pipe.Points.Count < 2)
                    pipe.Errors.Add("Polyline has fewer than two vertices.");
                if (pipe.Points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
                    throw new InvalidDataException("Non-finite coordinates on handle " + pipe.Handle);
                snapshot.Pipes.Add(pipe);
            }
            if (e is Hatch hatch && hatch.Layer.Equals(coverageLayer, StringComparison.OrdinalIgnoreCase))
            {
                ReadCoverage(hatch, snapshot).Match(error =>
                {
                    snapshot.Warnings.Add("Coverage hatch " + hatch.Handle + ": " + error + ". Coverage classification disabled for this drawing.");
                    snapshot.Coverage.Clear();
                    snapshot.CoverageLayer = "INVALID";
                    return false;
                }, () => true);
            }
        }
        if (snapshot.CoverageLayer == "INVALID")
            snapshot.Coverage.Clear();
        return snapshot;
    }
    private static CompareOption<string> ReadCoverage(Hatch hatch, Snapshot snapshot)
    {
        try
        {
            var polygon = new CoveragePolygon();
            for (int index = 0; index < hatch.NumberOfLoops; index++)
            {
                var loop = hatch.GetLoopAt(index);
                if (!loop.IsPolyline)
                    return CompareOption<string>.Some("Non-polyline coverage loop");
                var points = new List<XY>();
                var bulges = new List<double>();
                foreach (BulgeVertex vertex in loop.Polyline)
                {
                    points.Add(new(vertex.Vertex.X, vertex.Vertex.Y));
                    bulges.Add(vertex.Bulge);
                }
                if (points.Count < 3)
                    return CompareOption<string>.Some("Coverage loop has fewer than three vertices");
                var ring = Geometry.Flatten(points, bulges, true, .001 / (snapshot.MetersPerUnit > 0 ? snapshot.MetersPerUnit : 1));
                polygon.Rings.Add(ring.Select(point =>
                {
                    var transformed = new Autodesk.AutoCAD.Geometry.Point3d(point.X, point.Y, hatch.Elevation).TransformBy(hatch.Ecs);
                    return new XY(transformed.X, transformed.Y);
                }).ToList());
            }
            snapshot.Coverage.Add(polygon);
            return CompareOption<string>.None;
        }
        catch (System.Exception error) { return CompareOption<string>.Some(error.Message); }
    }
    private static void ReadProperties(Entity entity, Transaction tx, Pipe pipe)
    {
        if (entity.ExtensionDictionary.IsNull)
            return;
        ObjectIdCollection ids;
        try
        {
            ids = PropertyDataServices.GetPropertySets(entity);
        }
        catch (System.Exception ex) { pipe.Errors.Add("Could not enumerate Property Sets: " + ex.Message); return; }
        foreach (ObjectId id in ids)
        {
            try
            {
                var set = (PropertySet)tx.GetObject(id, OpenMode.ForRead);
                var def = (PropertySetDefinition)tx.GetObject(set.PropertySetDefinition, OpenMode.ForRead);
                foreach (PropertyDefinition prop in def.Definitions)
                {
                    string key = def.Name + "/" + prop.Name;
                    try
                    {
                        object? value = set.GetAt(prop.Id);
                        pipe.Properties[key] = value switch
                        {
                            null => "",
                            DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
                            _ => value.ToString() ?? ""
                        };
                    }
                    catch (System.Exception ex) { pipe.Errors.Add("Cannot read " + key + ": " + ex.Message); }
                }
            }
            catch (System.Exception ex) { pipe.Errors.Add("Could not open a Property Set: " + ex.Message); }
        }
    }
    public static double UnitScale(UnitsValue u) => u switch
    {
        UnitsValue.Meters => 1,
        UnitsValue.Millimeters => .001,
        UnitsValue.Centimeters => .01,
        UnitsValue.Decimeters => .1,
        UnitsValue.Kilometers => 1000,
        UnitsValue.Feet => .3048,
        UnitsValue.Inches => .0254,
        _ => 0
    };
    public static CompareOption<string> Normalize(Snapshot old, Snapshot current, Options opt, CompareOption<double> overrideScale = default)
    {
        double a = overrideScale.Match(v => v, () => old.MetersPerUnit), b = overrideScale.Match(v => v, () => current.MetersPerUnit);
        if (a <= 0 || b <= 0)
            return CompareOption<string>.Some("Drawing units are unspecified/unsupported. Select an explicit units override in the palette.");
        if (Math.Abs(a - b) > 1e-12)
        {
            double scale = a / b;
            foreach (var p in old.Pipes)
            {
                p.Points = p.Points.Select(v => v * scale).ToList();
                p.Samples.Clear();
            }
            foreach (var poly in old.Coverage)
                poly.Rings = poly.Rings.Select(r => r.Select(v => v * scale).ToList()).ToList();
            old.Warnings.Add("Old drawing coordinates scaled into the new drawing units.");
        }
        opt.ToleranceUnits = opt.ToleranceMeters / b;
        opt.MatchRadiusUnits = opt.MatchRadiusMeters / b;
        return CompareOption<string>.None;
    }
}
