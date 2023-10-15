using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;
using System.IO;
using System.Reflection;

using NetTopologySuite.Geometries;
using System.Globalization;
using System.Diagnostics;
using Accord.MachineLearning;
using NetTopologySuite.Triangulate;
using NetTopologySuite;
using Point = NetTopologySuite.Geometries.Point;
using Autodesk.AutoCAD.Colors;
using static Ler2PolygonSplitting.Utils;

namespace Ler2PolygonSplitting
{
    public partial class Ler2PolygonSplitting : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                SystemObjects.DynamicLinker.LoadModule(
                    "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
            }

#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
                new ResolveEventHandler(Debug_AssemblyResolve);
#endif

            prdDbg("Ler2PolygonSplitting loaded!\n");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("LER2SPLITIRREGULAR")]
        public void ler2splitirregular()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            CultureInfo dk = new CultureInfo("da-DK");

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string lyrPolygonSource = "LER2POLYGON-SOURCE";
                    string lyrSplitForGml = "LER2POLYGON-SPLITFORGML";
                    string lyrPolygonProcessed = "LER2POLYGON-PROCESSED";

                    localDb.CheckOrCreateLayer(lyrPolygonSource);
                    localDb.CheckOrCreateLayer(lyrSplitForGml);
                    localDb.CheckOrCreateLayer(lyrPolygonProcessed);

                    var colorGenerator = GetColorGenerator();

                    var plines = localDb.ListOfType<Polyline>(tx).Where(x => x.Layer == lyrPolygonSource);

                    HashSet<Polygon> allPolys = new HashSet<Polygon>();
                    foreach (var pl in plines)
                    {
                        Polygon polygon = NTS.NTSConversion.ConvertClosedPlineToNTSPolygon(pl);
                        double maxArea = 250000; //For later reporting

                        Dictionary<string, int> keywords = new Dictionary<string, int>();
                        int minsplits = polygon.Area % 250000 == 0 ? (int)(polygon.Area / 250000) : (int)(polygon.Area / 250000) + 1;
                        keywords.Add($"N{minsplits}:{(polygon.Area / minsplits).ToString("N0", dk)} m²", minsplits);
                        for (int i = 1; i < 5; i++)
                            keywords.Add($"N{minsplits + i}:{(polygon.Area / (minsplits + i)).ToString("0", dk)} m²", minsplits + i);

                        StringGridForm sgf = new StringGridForm(keywords.Select(x => x.Key).ToList());
                        sgf.ShowDialog();

                        if (sgf.SelectedValue == null)
                        {
                            tx.Abort();
                            prdDbg("User cancelled!");
                            return;
                        }

                        int K = keywords[sgf.SelectedValue];

                        int distance = (int)Math.Sqrt(polygon.EnvelopeInternal.Area / 1350);

                        var allPoints = GeneratePoints(polygon, distance);
                        prdDbg($"Number of Points in grid: {allPoints.NumPoints} with distance {distance}.");

                        if (K < 1) K = 1;

                        double targetArea = polygon.Area / K;

                        Stopwatch sw = Stopwatch.StartNew();
                        var clipPoints = polygon.Intersection(allPoints);
                        sw.Stop();
                        prdDbg($"Number of Points after intersection: {clipPoints.NumPoints}.\n" +
                            $"Intersect time: {sw.Elapsed}.");
                        prdDbg($"Expecting {K} polygons, target area: {(polygon.Area / K).ToString("N0", dk)} m².");

                        //NTSConversion.ConvertNTSMultiPointToDBPoints(clipPoints, localDb);
                        int maxIter = 300;

                        var data = ConvertGeometryToDoubleArray(clipPoints);

                        sw = Stopwatch.StartNew();

                        BalancedKMeans km = new BalancedKMeans(K)
                        {
                            MaxIterations = maxIter,
                            Tolerance = 1.0e-7,
                            UseSeeding = Seeding.Uniform,
                        };

                        var clusters = km.Learn(data);
                        sw.Stop(); prdDbg($"Clustering time: {sw.Elapsed}.");

                        var sites = km.Centroids.Select(x => new Coordinate(x[0], x[1])).ToList();

                        var diagram = GenerateVoronoiPolygons(sites, polygon.EnvelopeInternal);
                        var result = diagram.Select(x => polygon.Intersection(x));

                        prdDbg($"For polygon {pl.Handle} created {diagram.NumGeometries} polygon(s)!");

                        foreach (var face in result.OrderByDescending(x => x.Area))
                        {
                            var clip = polygon.Intersection(face);

                            var mpg = NTS.NTSConversion.ConvertNTSPolygonToMPolygon((Polygon)clip);
                            mpg.AddEntityToDbModelSpace(localDb);
                            mpg.Color = colorGenerator();
                            mpg.Layer = lyrSplitForGml;

                            double actualArea = Math.Abs(mpg.Area);

                            string warning = actualArea > maxArea ? " -> !!!Over MAX!!!" : "";

                            prdDbg($"{actualArea.ToString("N2", dk)} " +
                                $"{((actualArea - maxArea) / maxArea * 100).ToString("N1", dk)}%" +
                                warning);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("LER2SPLITBRENT")]
        public void ler2splitbrent()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            CultureInfo dk = new CultureInfo("da-DK");

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string lyrPolygonSource = "LER2POLYGON-SOURCE";
                    string lyrSplitForGml = "LER2POLYGON-SPLITFORGML";
                    string lyrPolygonProcessed = "LER2POLYGON-PROCESSED";

                    localDb.CheckOrCreateLayer(lyrPolygonSource);
                    localDb.CheckOrCreateLayer(lyrSplitForGml);
                    localDb.CheckOrCreateLayer(lyrPolygonProcessed);

                    var colorGenerator = GetColorGenerator();

                    var plines = localDb.ListOfType<Polyline>(tx).Where(x => x.Layer == lyrPolygonSource);

                    Stopwatch sw = Stopwatch.StartNew();

                    HashSet<Polygon> allPolys = new HashSet<Polygon>();
                    foreach (var pl in plines)
                    {
                        Polygon polygon = NTS.NTSConversion.ConvertClosedPlineToNTSPolygon(pl);
                        double maxArea = 245000;

                        var pd = new Brent.PolygonDivider();
                        //pd.Run(polygon, pl.Handle.ToString(), maxArea, true, Brent.Direction.RightTop);
                        if (pd.Result == Brent.OperationResult.Failure)
                        {
                            prdDbg($"Brent failed for polygon {pl.Handle} with message:\n" +
                                $"{pd.Messages}");
                            continue;
                        }

                        foreach (var face in pd.DividedPolygons.OrderByDescending(x => x.Area))
                        {
                            var mpg = NTS.NTSConversion.ConvertNTSPolygonToMPolygon((Polygon)face);
                            mpg.AddEntityToDbModelSpace(localDb);
                            mpg.Color = colorGenerator();
                            mpg.Layer = lyrSplitForGml;

                            double actualArea = Math.Abs(mpg.Area);

                            string warning = actualArea > 250000 ? " -> !!!Over MAX!!!" : "";

                            prdDbg($"{actualArea.ToString("N2", dk)} " +
                                $"{((actualArea - maxArea) / maxArea * 100).ToString("N1", dk)}%" +
                                warning);
                        }
                    }

                    sw.Stop(); prdDbg($"Processing time: {sw.Elapsed}.");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        static GeometryCollection GenerateVoronoiPolygons(ICollection<Coordinate> sites, Envelope envelope)
        {
            var builder = new VoronoiDiagramBuilder();
            builder.SetSites(sites);
            builder.ClipEnvelope = envelope;
            //builder.ClipEnvelope = envelope;
            builder.Tolerance = 0.0;
            return builder.GetDiagram(NtsGeometryServices.Instance.CreateGeometryFactory());
        }
        static double[][] ConvertGeometryToDoubleArray(Geometry geom)
        {
            var coordinates = geom.Coordinates;
            var array = new double[coordinates.Length][];

            for (int i = 0; i < coordinates.Length; i++)
            {
                array[i] = new double[] { coordinates[i].X, coordinates[i].Y };
            }

            return array;
        }
        private static MultiPoint GeneratePoints(Polygon polygon, int distance)
        {
            int minX = (int)polygon.EnvelopeInternal.MinX - 1;
            int minY = (int)polygon.EnvelopeInternal.MinY - 1;
            int maxX = (int)polygon.EnvelopeInternal.MaxX + 1;
            int maxY = (int)polygon.EnvelopeInternal.MaxY + 1;

            int deltaX = (maxX - minX) / distance + 1;
            int deltaY = (maxY - minY) / distance + 1;

            var coordinates = new HashSet<Point>();

            var pm = new ProgressMeter();
            pm.Start("Creating grid points!");
            pm.SetLimit(deltaX * deltaY);

            for (int i = 0; i < deltaX; i++)
                for (int j = 0; j < deltaY; j++)
                {
                    coordinates.Add(new Point(new Coordinate(minX + i * distance, minY + j * distance)));
                    pm.MeterProgress();
                }

            pm.Stop();

            return new MultiPoint(coordinates.ToArray());
        }
        private static List<Polygon> SplitPolygon(Polygon polygon, double maxArea)
        {
            List<Polygon> result = new List<Polygon>();

            if (polygon.Area <= maxArea)
            {
                result.Add(polygon);
                return result;
            }

            Envelope env = polygon.EnvelopeInternal;
            double dx = env.MaxX - env.MinX;
            double dy = env.MaxY - env.MinY;
            Coordinate c1, c2;

            double ratio = Math.Sqrt(maxArea / polygon.Area);

            if (dx >= dy)
            {
                c1 = new Coordinate(env.MinX + dx * ratio, env.MinY);
                c2 = new Coordinate(env.MinX + dx * ratio, env.MaxY);
            }
            else
            {
                c1 = new Coordinate(env.MinX, env.MinY + dy * ratio);
                c2 = new Coordinate(env.MaxX, env.MinY + dy * ratio);
            }

            LineString line = new LineString(new Coordinate[] { c1, c2 });
            Geometry splitLine = line.Buffer(0.0000001); // Adjust buffer as needed

            Geometry[] pieces = new Geometry[]
            {
                polygon.Difference(splitLine),
                splitLine.Difference(polygon)
            };

            foreach (var piece in pieces)
            {
                if (piece is Polygon part)
                {
                    if (IsValidShape(part))
                    {
                        result.AddRange(SplitPolygon(part, maxArea));
                    }
                }
                else if (piece is MultiPolygon multiPolygon)
                {
                    foreach (Polygon partP in multiPolygon.Geometries)
                    {
                        if (IsValidShape(partP))
                        {
                            result.AddRange(SplitPolygon(partP, maxArea));
                        }
                    }
                }
            }

            return result;
        }
        private static bool IsValidShape(Polygon polygon)
        {
            Envelope env = polygon.EnvelopeInternal;
            double dx = env.MaxX - env.MinX;
            double dy = env.MaxY - env.MinY;

            // Check the aspect ratio to avoid narrow shapes
            if (dx == 0 || dy == 0) return false;

            double aspectRatio = Math.Max(dx / dy, dy / dx);
            return aspectRatio <= 3.0 && polygon.Area > 6.0;
        }

        [CommandMethod("LER2SPLITRECTANGULAR")]
        public void ler2splitrectangular()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            CultureInfo dk = new CultureInfo("da-DK");

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string lyrPolygonSource = "LER2POLYGON-SOURCE";
                    string lyrSplitForGml = "LER2POLYGON-SPLITFORGML";
                    string lyrPolygonProcessed = "LER2POLYGON-PROCESSED";

                    localDb.CheckOrCreateLayer(lyrPolygonSource);
                    localDb.CheckOrCreateLayer(lyrSplitForGml);
                    localDb.CheckOrCreateLayer(lyrPolygonProcessed);

                    var colorGenerator = GetColorGenerator();

                    var plines = localDb.ListOfType<Polyline>(tx).Where(x => x.Layer == lyrPolygonSource);

                    foreach (var pl in plines)
                    {
                        Polygon polygon = NTS.NTSConversion.ConvertClosedPlineToNTSPolygon(pl);
                        double maxArea = 250000;

                        var result = SplitRectangle(polygon, maxArea);

                        prdDbg($"For polygon {pl.Handle} created {result.Count} polygon(s)!");

                        foreach (var split in result.OrderByDescending(x => x.Area))
                        {
                            var mpg = NTS.NTSConversion.ConvertNTSPolygonToMPolygon(split);
                            mpg.AddEntityToDbModelSpace(localDb);
                            mpg.Color = colorGenerator();
                            mpg.Layer = lyrSplitForGml;

                            double actualArea = Math.Abs(mpg.Area);

                            string warning = actualArea > maxArea ? " -> !!!Over MAX!!!" : "";

                            prdDbg($"{actualArea.ToString("N2", dk)} " +
                                $"{((actualArea - maxArea) / maxArea * 100).ToString("N1", dk)}%" +
                                warning);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        public static List<Polygon> SplitRectangle(Polygon rectangle, double targetArea)
        {
            var result = new List<Polygon>();

            double rectWidth = rectangle.EnvelopeInternal.Width;
            double rectHeight = rectangle.EnvelopeInternal.Height;
            double rectMinX = rectangle.EnvelopeInternal.MinX;
            double rectMinY = rectangle.EnvelopeInternal.MinY;

            double aspectRatio = rectWidth / rectHeight;
            int totalDivisions = (int)Math.Ceiling(rectWidth * rectHeight / targetArea);

            int divisionsWidth = (int)Math.Round(Math.Sqrt(totalDivisions * aspectRatio));
            int divisionsHeight = (int)Math.Round((double)totalDivisions / divisionsWidth);

            while (divisionsWidth * divisionsHeight < totalDivisions)
            {
                divisionsWidth++;
                divisionsHeight = (int)Math.Round((double)totalDivisions / divisionsWidth);
            }

            double stepWidth = rectWidth / divisionsWidth;
            double stepHeight = rectHeight / divisionsHeight;

            for (int i = 0; i < divisionsWidth; i++)
            {
                for (int j = 0; j < divisionsHeight; j++)
                {
                    double minX = rectMinX + i * stepWidth;
                    double minY = rectMinY + j * stepHeight;
                    double maxX = minX + stepWidth;
                    double maxY = minY + stepHeight;

                    Polygon subRectangle = new Polygon(new LinearRing(new Coordinate[]
                    {
                    new Coordinate(minX, minY),
                    new Coordinate(minX, maxY),
                    new Coordinate(maxX, maxY),
                    new Coordinate(maxX, minY),
                    new Coordinate(minX, minY)
                    }));

                    result.Add(subRectangle);
                }
            }

            return result;
        }

        [CommandMethod("LER2EXPORT2LER2")]
        public void ler2export2ler2()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            CultureInfo dk = new CultureInfo("da-DK");

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string lyrSplitForGml = "LER2POLYGON-SPLITFORGML";

                    var mpgs = localDb.HashSetOfType<MPolygon>(tx)
                        .Where(x => x.Layer == lyrSplitForGml);

                    List<string> lines = new List<string>();
                    foreach (var mpg in mpgs)
                    {
                        int nrOfLoops = mpg.NumMPolygonLoops;

                        string coords = "";
                        List<BulgeVertex> vertices = new List<BulgeVertex>();

                        for (int i = 0; i < nrOfLoops; i++)
                        {
                            MPolygonLoop loop = mpg.GetMPolygonLoopAt(i);
                            for (int j = 0; j < loop.Count; j++) vertices.Add(loop[j]);
                        }

                        coords = string.Join(" ", vertices.Select(x => $"{x.Vertex.X.ToString()} " +
                        $"{x.Vertex.Y.ToString()}"));
                        lines.Add(coords);
                    }

                    string path = Path.GetDirectoryName(localDb.Filename);
                    string coordsFilename = Path.Combine(path, "GMLCoordinates.txt");

                    File.WriteAllLines(coordsFilename, lines);
                    prdDbg($"Exported coordinates to {coordsFilename}!");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        public static Func<Color> GetColorGenerator()
        {
            List<Color> filteredColors = new List<Color>(AutocadStdColors.Values);

            // Remove first and last entry
            filteredColors.RemoveAt(0);
            filteredColors.RemoveAt(filteredColors.Count - 1);

            int index = 0;
            return () =>
            {
                var color = filteredColors[index];
                index = (index + 1) % filteredColors.Count;
                return color;
            };
        }

        public void ler2createpolygonsOLD()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string lyrPolygonSource = "LER2POLYGON-SOURCE";
                    string lyrSplitForGml = "LER2POLYGON-SPLITFORGML";
                    string lyrPolygonProcessed = "LER2POLYGON-PROCESSED";

                    localDb.CheckOrCreateLayer(lyrPolygonSource);
                    localDb.CheckOrCreateLayer(lyrSplitForGml);
                    localDb.CheckOrCreateLayer(lyrPolygonProcessed);

                    var colorGenerator = GetColorGenerator();

                    var plines = localDb.ListOfType<Polyline>(tx).Where(x => x.Layer == lyrPolygonSource);

                    HashSet<Polygon> allPolys = new HashSet<Polygon>();
                    foreach (var pl in plines)
                    {
                        Polygon polygon = NTS.NTSConversion.ConvertClosedPlineToNTSPolygon(pl);
                        double maxArea = 250000;

                        // Perform the polygon splitting
                        List<Polygon> subPolygons = SplitPolygon(polygon, maxArea);

                        foreach (var subPolygon in subPolygons) allPolys.Add(subPolygon);
                    }

                    prdDbg($"Created {allPolys.Count} polygon(s)!");

                    foreach (var polygon in allPolys)
                    {
                        //Hatch hatch = NTSConversion.ConvertNTSPolygonToHatch(polygon);
                        //hatch.Color = colorGenerator();
                        //hatch.AddEntityToDbModelSpace(localDb);

                        MPolygon mpg = NTS.NTSConversion.ConvertNTSPolygonToMPolygon(polygon);
                        mpg.Color = colorGenerator();
                        mpg.AddEntityToDbModelSpace(localDb);
                    }

                    //foreach (var pline in plines)
                    //{
                    //    pline.CheckOrOpenForWrite();
                    //    pline.Layer = lyrPolygonProcessed;
                    //}
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
#if DEBUG
        private static Assembly Debug_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string assemblyFolder = @"X:\Github\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\Ler2PolygonSplitting\bin\Debug";
            prdDbg($"Asked for assembly: {args.Name}!");

            var name = args.Name.Split(',')[0];

            switch (name)
            {
                case "QuikGraph":
                    {
                        string filePath = Path.Combine(assemblyFolder, "QuikGraph.dll");
                        return Assembly.LoadFrom(filePath);
                    }
                case "QuikGraph.Graphviz":
                    {
                        string filePath = Path.Combine(assemblyFolder, "QuikGraph.Graphviz.dll");
                        return Assembly.LoadFrom(filePath);
                    }
                case "NetTopologySuite":
                    {
                        string filePath = Path.Combine(assemblyFolder, "NetTopologySuite.dll");
                        return Assembly.LoadFrom(filePath);
                    }
                case "Accord.MachineLearning":
                    {
                        string filePath = Path.Combine(assemblyFolder, "Accord.MachineLearning.dll");
                        return Assembly.LoadFrom(filePath);
                    }
                default:
                    break;
            }

            return null;
        }
#endif
    }
}
