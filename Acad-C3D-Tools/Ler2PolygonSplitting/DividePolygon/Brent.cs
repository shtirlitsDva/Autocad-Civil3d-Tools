using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Operation.Union;
using static Ler2PolygonSplitting.Utils;

namespace Ler2PolygonSplitting.Brent
{
    public struct BrentResult
    {
        public OperationResult Result { get; set; }
        public string Message { get; set; }
        public double ResultValue { get; set; }
    }
    static class BrentSolver
    {
        public static BrentResult Brent(double xa, double xb, double xtol, double ftol, int max_iter, Geometry geom,
                            double fixedCoord1, double fixedCoord2, double targetArea,
                            bool horizontal, bool forward)
        {
            double EPS = double.Epsilon;

            if (Math.Abs(xb - xa) < EPS)
            {
                return new BrentResult()
                {
                    Result = OperationResult.Failure,
                    Message = "Initial bracket smaller than system epsilon.",
                    ResultValue = double.NaN
                };
            }

            double fa = f(xa, geom, fixedCoord1, fixedCoord2, targetArea, horizontal, forward);
            if (Math.Abs(fa) < ftol)
            {
                return new BrentResult()
                {
                    Result = OperationResult.Failure,
                    Message = "Root is equal to the lower bracket.",
                    ResultValue = double.NaN
                };
            }

            double fb = f(xb, geom, fixedCoord1, fixedCoord2, targetArea, horizontal, forward);
            if (Math.Abs(fb) < ftol)
            {
                return new BrentResult()
                {
                    Result = OperationResult.Failure,
                    Message = "Root is equal to the upper bracket.",
                    ResultValue = double.NaN
                };
            }

            if (fa * fb > 0.0)
            {
                return new BrentResult()
                {
                    Result = OperationResult.Failure,
                    Message = "Root is not bracketed.",
                    ResultValue = double.NaN
                };
            }

            if (Math.Abs(fa) < Math.Abs(fb))
            {
                (xa, xb) = (xb, xa);
                (fa, fb) = (fb, fa);
            }

            double xc = xa;
            double fc = fa;

            bool mflag = true;
            double xs = 0, fs = 0, d = 0;

            for (int i = 0; i < max_iter; i++)
            {
                if (fa != fc && fb != fc)
                {
                    xs = (xa * fb * fc / ((fa - fb) * (fa - fc)) +
                          xb * fa * fc / ((fb - fa) * (fb - fc)) +
                          xc * fa * fb / ((fc - fa) * (fc - fb)));
                }
                else xs = xb - fb * (xb - xa) / (fb - fa);

                if (((xs < ((3 * xa + xb) / 4) || xs > xb) ||
                     (mflag && Math.Abs(xs - xb) >= (Math.Abs(xb - xc) / 2)) ||
                     (!mflag && Math.Abs(xs - xb) >= (Math.Abs(xc - d) / 2)) ||
                     (mflag && Math.Abs(xb - xc) < EPS) ||
                     (!mflag && Math.Abs(xc - d) < EPS)))
                {
                    xs = (xa + xb) / 2;
                    mflag = true;
                }
                else mflag = false;

                fs = f(xs, geom, fixedCoord1, fixedCoord2, targetArea, horizontal, forward);

                if (Math.Abs(fs) < ftol)
                {
                    return new BrentResult()
                    {
                        Result = OperationResult.Success,
                        Message = "Root found.",
                        ResultValue = xs
                    };
                }

                if (Math.Abs(xb - xa) < xtol)
                {
                    return new BrentResult()
                    {
                        Result = OperationResult.Failure,
                        Message = "Bracket is smaller than tolerance.",
                        ResultValue = double.NaN
                    };
                }

                d = xc;
                (xc, fc) = (xb, fb);

                if (fa * fs < 0) (xb, fb) = (xs, fs);
                else (xa, fa) = (xs, fs);

                if (Math.Abs(fa) < Math.Abs(fb))
                {
                    (xa, xb) = (xb, xa);
                    (fa, fb) = (fb, fa);
                }
            }

            return new BrentResult()
            {
                Result = OperationResult.Success,
                Message = "Maximum number of iterations reached.",
                ResultValue = xs
            };
        }

        public static (Geometry left, Geometry right, List<Geometry> nonContiguous) SplitPoly(
        Geometry polygon,
        LineString splitter,
        bool horizontal,
        bool forward)
        {
            Geometry poly = polygon.Copy();
            var polys = new List<Geometry>();

            // Split the geometry
            var splitGeometry = SplitPolygon((Polygon)poly, splitter);
            if (splitGeometry is MultiPolygon multiPolygon) polys.AddRange(multiPolygon.Geometries);
            else polys.Add(splitGeometry);

            //Why is the original poly added to the polys list?
            if (poly is MultiPolygon multiPoly) polys.AddRange(multiPoly.Geometries);
            else polys.Add(poly);

            // Sort into left, right, and residual
            Geometry left;
            Geometry right;
            List<Geometry> nonContiguous = new List<Geometry>();

            if (polys.Count > 1)
            {
                if (forward)
                {
                    if (horizontal)
                    {
                        left = GetExtremeGeometry(polys, "MaxY", true);
                        right = GetExtremeGeometry(polys, "MinY", false);
                    }
                    else
                    {
                        left = GetExtremeGeometry(polys, "MaxX", true);
                        right = GetExtremeGeometry(polys, "MinX", false);
                    }
                }
                else
                {
                    if (horizontal)
                    {
                        left = GetExtremeGeometry(polys, "MinY", false);
                        right = GetExtremeGeometry(polys, "MaxY", true);
                    }
                    else
                    {
                        left = GetExtremeGeometry(polys, "MinX", false);
                        right = GetExtremeGeometry(polys, "MaxX", true);
                    }
                }

                // Check contiguous and non-contiguous polygons
                foreach (var polyGeom in polys)
                {
                    if (left.Touches(polyGeom))
                    {
                        left = left.Union(polyGeom);
                    }
                    else
                    {
                        nonContiguous.Add(polyGeom);
                    }
                }

                return (left, right, nonContiguous);
            }
            else
            {
                throw new Exception("Polygon division failed.");
            }
        }

        private static Geometry Polygonize(Geometry geometry)
        {
            var lines = LineStringExtracter.GetLines(geometry);
            var polygonizer = new Polygonizer();
            polygonizer.Add(lines);
            var polys = polygonizer.GetPolygons();
            var polyArray = GeometryFactory.ToGeometryArray(polys);
            return geometry.Factory.CreateGeometryCollection(polyArray);
        }

        private static Geometry SplitPolygon(Geometry polygon, Geometry line)
        {
            var nodedLinework = polygon.Boundary.Union(line);
            var polygons = Polygonize(nodedLinework);

            // only keep polygons which are inside the input
            var output = new List<Geometry>();
            for (var i = 0; i < polygons.NumGeometries; i++)
            {
                var candpoly = (Polygon)polygons.GetGeometryN(i);
                if (polygon.Contains(candpoly.InteriorPoint))
                    output.Add(candpoly);
            }
            return polygon.Factory.BuildGeometry(output);
        }

        private static Geometry GetExtremeGeometry(List<Geometry> geometries, string coordinateType, bool remove)
        {
            double extremeValue = coordinateType.StartsWith("Max") ? double.NegativeInfinity : double.PositiveInfinity;
            Geometry extremeGeometry = null;
            int index = -1;

            for (int i = 0; i < geometries.Count; i++)
            {
                var geom = geometries[i];
                var env = geom.EnvelopeInternal;
                double value = 0;

                switch (coordinateType)
                {
                    case "MaxX": value = env.MaxX; break;
                    case "MaxY": value = env.MaxY; break;
                    case "MinX": value = env.MinX; break;
                    case "MinY": value = env.MinY; break;
                }

                if (coordinateType.StartsWith("Max"))
                {
                    if (value > extremeValue)
                    {
                        extremeValue = value;
                        extremeGeometry = geom;
                        index = i;
                    }
                }
                else
                {
                    if (value < extremeValue)
                    {
                        extremeValue = value;
                        extremeGeometry = geom;
                        index = i;
                    }
                }
            }

            if (remove && index != -1)
            {
                geometries.RemoveAt(index);
            }

            return extremeGeometry;
        }

        public static double GetSliceArea(double sliceCoord, Geometry poly, double fixedCoord1, double fixedCoord2, bool horizontal, bool forward)
        {
            Coordinate[] splitterCoords;

            if (horizontal)
            {
                splitterCoords = new Coordinate[]
                {
                    new Coordinate(fixedCoord1, sliceCoord),
                    new Coordinate(fixedCoord2, sliceCoord)
                };
            }
            else
            {
                splitterCoords = new Coordinate[]
                {
                    new Coordinate(sliceCoord, fixedCoord1),
                    new Coordinate(sliceCoord, fixedCoord2)
                };
            }

            LineString splitter = new LineString(splitterCoords);

            (_, Geometry right, _) = SplitPoly(poly, splitter, horizontal, forward);

            return right.Area;
        }

        public static double f(double sliceCoord, Geometry poly, double fixedCoord1, double fixedCoord2, double targetArea, bool horizontal, bool forward)
        {
            double sliceArea = GetSliceArea(sliceCoord, poly, fixedCoord1, fixedCoord2, horizontal, forward);
            return sliceArea - targetArea;
        }
    }

    public class PolygonDivider
    {
        public OperationResult Result { get; private set; } = OperationResult.NonInitialized;
        public string Messages { get => string.Join(Environment.NewLine, messages); }
        private List<string> messages = new List<string>();

        public List<Geometry> DividedPolygons { get; private set; } = new List<Geometry>();

        private GeometryFactory factory = new GeometryFactory();

        private static readonly double buffer = 1e-6;
        //private static readonly double tol = 1e-6;
        private static readonly double t = 1e-6;
        //private static readonly double xtol = 1e-6;
        //private static readonly double ftol = 1e-6;

        private bool ERROR_FLAG_0 = false;
        private bool ERROR_FLAG_1 = false;
        private bool ERROR_FLAG_2 = false;
        private bool ERROR_FLAG_3 = false;

        private int candidate1Counter = 0;
        private int candidate2Counter = 0;

        public void Run(Geometry polygonToSplit, string sourceHandle, double idealTargetArea, Direction direction)
        {
            Geometry initialSlice;
            BrentResult result;
            BrentResult sliceResult;
            int nSubdivisions;

            try
            {
                bool horizontalFlag = direction == Direction.BottomLeft || direction == Direction.TopRight;
                bool forwardFlag = direction == Direction.BottomLeft || direction == Direction.LeftBottom;

                List<Geometry> subfeatures = new List<Geometry>();
                int j = 0;

                Geometry bufferedPolygon = polygonToSplit.Buffer(0);

                if (bufferedPolygon.IsEmpty)
                {
                    messages.Add($"ERROR: Buffering failed for {sourceHandle}!");
                    Result = OperationResult.Failure;
                    return;
                }

                 //create a list out of the original polygon
                if (bufferedPolygon is MultiPolygon)
                {
                    MultiPolygon multiGeom = (MultiPolygon)bufferedPolygon;
                    foreach (var geom in multiGeom.Geometries) subfeatures.Add(geom);
                }
                else if (bufferedPolygon is Polygon) subfeatures.Add(bufferedPolygon);
                else
                {
                    messages.Add($"ERROR: {sourceHandle} is not a polygon!");
                    Result = OperationResult.Failure;
                    return;
                }

                Stack<Geometry> stack = new Stack<Geometry>(subfeatures);

                while (stack.Count > 0)
                {
                    Geometry poly = stack.Pop().Copy();

                    int nPolygons = (int)(poly.Area / idealTargetArea);
                    if (nPolygons == 0) nPolygons = 1;

                    double targetArea;
                    if (poly.Area % idealTargetArea < 1e-9)
                        targetArea = idealTargetArea;
                    else
                    {
                        nPolygons++;
                        targetArea = poly.Area / nPolygons;
                    }

                    double sq = Math.Sqrt(targetArea);

                    while (poly.Area > targetArea + t)
                    {
                        var boundsR = poly.EnvelopeInternal;
                        //                      0                1             2             3
                        double[] bounds = { boundsR.MinX, boundsR.MinY, boundsR.MaxX, boundsR.MaxY };

                        double[] interval = new double[2], fixedCoords = new double[2];

                        if (horizontalFlag)
                        {
                            interval[0] = boundsR.MinY + buffer;
                            interval[1] = boundsR.MaxY - buffer;
                            fixedCoords[0] = boundsR.MinX;
                            fixedCoords[1] = boundsR.MaxX;
                        }
                        else
                        {
                            interval[0] = boundsR.MinX + buffer;
                            interval[1] = boundsR.MaxX - buffer;
                            fixedCoords[0] = boundsR.MinY;
                            fixedCoords[1] = boundsR.MaxY;
                        }

                        if ((interval[1] - interval[0]) > sq)
                        {
                            double sqArea = 0;
                            if (forwardFlag)
                                sqArea = BrentSolver.GetSliceArea(
                                    interval[0] + sq - buffer, poly, fixedCoords[0], fixedCoords[1], horizontalFlag, forwardFlag);
                            else
                                sqArea = BrentSolver.GetSliceArea(
                                    interval[1] - sq + buffer, poly, fixedCoords[0], fixedCoords[1], horizontalFlag, forwardFlag);

                            nSubdivisions = (int)Math.Round(sqArea / targetArea);
                            if (nSubdivisions == 0) nSubdivisions = 1;

                            double initialTargeArea = nSubdivisions * targetArea;

                            //make a backup to reset if we move from EF 0 to EF 1
                            int nSubdivisions2 = nSubdivisions;

                            while (true)
                            {
                                //Endless loop candidate 1
                                candidate1Counter++;
                                prdDbgIL("1");
                                if (candidate1Counter % 100 == 0)
                                {
                                    prdDbg($"Candidate 1: {candidate1Counter}\n");
                                    System.Windows.Forms.Application.DoEvents();
                                    if (candidate1Counter == 1000)
                                        throw new Exception("Endless loop candidate 1 forcibly stopped after 1000 iterations!");
                                }

                                result = BrentSolver.Brent(
                                    interval[0], interval[1], xtol, ftol, 500, poly,
                                    fixedCoords[0], fixedCoords[1], initialTargeArea, horizontalFlag, forwardFlag);

                                if (result.Result == OperationResult.Success) break;

                                if (result.Result == OperationResult.Failure)
                                {
                                    if (result.Message == "Bracket is smaller than tolerance.")
                                    {
                                        nSubdivisions++;
                                        continue;
                                    }
                                    else
                                    {
                                        ERROR_FLAG_0 = true;
                                        break;
                                    }
                                }
                            }

                            if (ERROR_FLAG_0)
                            {
                                nSubdivisions = nSubdivisions2; //reset
                                int limit = 1;
                                while (nSubdivisions >= limit)
                                {
                                    if (nSubdivisions == limit) ERROR_FLAG_1 = true;

                                    initialTargeArea = nSubdivisions * targetArea;

                                    result = BrentSolver.Brent(
                                        interval[0], interval[1], xtol, ftol, 500, poly,
                                        fixedCoords[0], fixedCoords[1], initialTargeArea, horizontalFlag, forwardFlag);

                                    if (result.Result == OperationResult.Success) break;
                                    else
                                    {
                                        nSubdivisions--;
                                        continue;
                                    }
                                }
                            }

                            if (ERROR_FLAG_1)
                            {
                                ERROR_FLAG_0 = false;
                                ERROR_FLAG_1 = false;

                                if (ERROR_FLAG_2 == false)
                                {
                                    ERROR_FLAG_2 = true;
                                    forwardFlag = !forwardFlag;
                                    continue;
                                }
                                else if (ERROR_FLAG_3 == false)
                                {
                                    ERROR_FLAG_2 = false;
                                    ERROR_FLAG_3 = true;
                                    horizontalFlag = !horizontalFlag;
                                    continue;
                                }
                                else
                                {
                                    DividedPolygons.Add(poly);
                                    continue;
                                }
                            }

                            ERROR_FLAG_0 = false;
                            ERROR_FLAG_1 = false;
                            ERROR_FLAG_2 = false;
                            ERROR_FLAG_3 = false;

                            LineString line;
                            if (horizontalFlag)
                            {
                                line = new LineString(new Coordinate[]
                                {
                                        new Coordinate(fixedCoords[0], result.ResultValue),
                                        new Coordinate(fixedCoords[1], result.ResultValue)
                                });
                            }
                            else
                            {
                                line = new LineString(new Coordinate[]
                                {
                                        new Coordinate(result.ResultValue, fixedCoords[0]),
                                        new Coordinate(result.ResultValue, fixedCoords[1])
                                });
                            }

                            var (left, right, nonContiguous) = BrentSolver.SplitPoly(poly, line, horizontalFlag, forwardFlag);
                            poly = left;
                            initialSlice = right;
                            foreach (var geom in nonContiguous) stack.Push(geom);
                        }
                        else
                        {
                            initialSlice = poly.Copy();
                            poly = factory.CreatePolygon();

                            nSubdivisions = (int)(initialSlice.Area / targetArea);
                            if (nSubdivisions == 0) nSubdivisions = 1;
                        }

                        for (int k = 0; k < nSubdivisions - 1; k++)
                        {
                            var sliceBoundsR = initialSlice.EnvelopeInternal;
                            double[] sliceBounds = { sliceBoundsR.MinX, sliceBoundsR.MinY, sliceBoundsR.MaxX, sliceBoundsR.MaxY };

                            bool sliceHorizontal = !horizontalFlag;

                            double[] sliceInterval = new double[2], sliceFixedCoords = new double[2];

                            if (sliceHorizontal)
                            {
                                sliceInterval[0] = sliceBounds[1] + buffer;
                                sliceInterval[1] = sliceBounds[3] - buffer;
                                sliceFixedCoords[0] = sliceBounds[0];
                                sliceFixedCoords[1] = sliceBounds[2];
                            }
                            else
                            {
                                sliceInterval[0] = sliceBounds[0] + buffer;
                                sliceInterval[1] = sliceBounds[2] - buffer;
                                sliceFixedCoords[0] = sliceBounds[1];
                                sliceFixedCoords[1] = sliceBounds[3];
                            }

                            double tol = t;

                            while (true)
                            {
                                //Endless loop candidate 2
                                candidate2Counter++;
                                prdDbgIL("2");
                                if (candidate2Counter % 100 == 0)
                                {
                                    prdDbg($"Candidate 2: {candidate2Counter}\n");
                                    System.Windows.Forms.Application.DoEvents();
                                    if (candidate1Counter == 1000)
                                        throw new Exception("Endless loop candidate 1 forcibly stopped after 1000 iterations!");
                                }

                                sliceResult = BrentSolver.Brent(
                                    sliceInterval[0], sliceInterval[1], xtol, ftol, 500, initialSlice,
                                    sliceFixedCoords[0], sliceFixedCoords[1], targetArea, sliceHorizontal, forwardFlag);

                                if (sliceResult.Result == OperationResult.Success) break;
                                else
                                {
                                    if (sliceResult.Message == "Bracket is smaller than tolerance.")
                                    {
                                        tol *= 2;
                                        continue;
                                    }
                                    else
                                    {
                                        ERROR_FLAG_1 = true;
                                        break;
                                    }
                                }
                            }

                            if (ERROR_FLAG_1 && !ERROR_FLAG_2)
                            {
                                ERROR_FLAG_2 = true;
                                forwardFlag = !forwardFlag;
                                continue;
                            }
                            else if (ERROR_FLAG_1 && !ERROR_FLAG_3)
                            {
                                ERROR_FLAG_3 = true;
                                horizontalFlag = !horizontalFlag;
                                break;
                            }

                            ERROR_FLAG_1 = false;
                            ERROR_FLAG_2 = false;
                            ERROR_FLAG_3 = false;

                            LineString sliceLine;
                            if (horizontalFlag)
                            {
                                sliceLine = new LineString(new Coordinate[]
                                {
                                        new Coordinate(sliceResult.ResultValue, sliceFixedCoords[0]),
                                        new Coordinate(sliceResult.ResultValue, sliceFixedCoords[1])
                                });
                            }
                            else
                            {
                                sliceLine = new LineString(new Coordinate[]
                                {
                                    new Coordinate(sliceFixedCoords[0], sliceResult.ResultValue),
                                    new Coordinate(sliceFixedCoords[1], sliceResult.ResultValue)
                                });
                            }

                            var (left, right, nonContiguous) = BrentSolver.SplitPoly(initialSlice, sliceLine, sliceHorizontal, forwardFlag);
                            initialSlice = left.Copy();
                            foreach (var geom in nonContiguous) stack.Push(geom);

                            DividedPolygons.Add(right);
                        }

                        DividedPolygons.Add(initialSlice);
                    }
                    try
                    {
                        DividedPolygons.Add(poly);
                    }
                    catch (Exception) {}
                }

                Result = OperationResult.Success;
                messages.Add($"SUCCESS: {sourceHandle} was successfully divided into {DividedPolygons.Count} polygons.");
            }
            catch (Exception ex)
            {
                Result = OperationResult.Failure;
                messages.Add($"ERROR: Operation for {sourceHandle} threw an Exception!\n{ex.Message}\n{ex}");
                return;
            }
        }
    }

    public enum Direction
    {
        BottomLeft,
        TopRight,
        LeftBottom,
        RightTop
    }
    public enum OperationResult
    {
        NonInitialized,
        Success,
        Failure
    }
}
