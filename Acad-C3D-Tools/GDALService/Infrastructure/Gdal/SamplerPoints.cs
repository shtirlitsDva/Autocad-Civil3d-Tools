using GDALService.Common;
using GDALService.Domain.Models;

using OSGeo.GDAL;

using System.Collections.Concurrent;

namespace GDALService.Infrastructure.Gdal
{
    internal static partial class Sampler
    {
        internal sealed class Summary { public int Total, Ok, Outside, NoData, Err; }

        public static (List<PointOut> rows, Summary sum) SamplePoints(
            Dataset ds,
            IEnumerable<PointIn> pts,
            int? threads,
            IProgressReporter progress)
        {
            var points = pts.ToList();
            int total = points.Count;

            var bag = new ConcurrentBag<PointOut>();
            var sum = new Summary();

            var po = new ParallelOptions
            {
                MaxDegreeOfParallelism = threads.HasValue && threads.Value > 0
                    ? threads.Value : Math.Max(1, Environment.ProcessorCount - 1)
            };

            int done = 0;

            // Each worker opens its own Dataset/Band and builds its own transforms.
            Parallel.ForEach(Partitioner.Create(points), po,
                () =>
                {                    
                    if (ds == null) throw new InvalidOperationException("Failed to open VRT in worker.");
                    var band = ds.GetRasterBand(1) ?? throw new InvalidOperationException("No band 1.");
                    band.GetNoDataValue(out double nodata, out int hasNd);

                    var gt = new double[6];
                    ds.GetGeoTransform(gt);
                    var inv = new double[6];
                    if (OSGeo.GDAL.Gdal.InvGeoTransform(gt, inv) == 0)
                        throw new InvalidOperationException("Cannot invert geotransform.");

                    return new WorkerState(ds, band, inv, hasNd != 0, nodata);
                },
                // loop body
                (p, loopState, local) =>
                {
                    try
                    {
                        OSGeo.GDAL.Gdal.ApplyGeoTransform(local.Inv, p.X, p.Y, out double px, out double py);
                        int ix = (int)Math.Floor(px), iy = (int)Math.Floor(py);

                        if (ix < 0 || iy < 0 || ix >= local.Ds.RasterXSize || iy >= local.Ds.RasterYSize)
                        {
                            Interlocked.Increment(ref sum.Outside);
                            bag.Add(new PointOut { GeomId = p.GeomId, Seq = p.Seq, S = p.S, X = p.X, Y = p.Y, Elev = double.NaN, Status = "OUTSIDE" });
                        }
                        else
                        {
                            double[] buf = new double[1];
                            try
                            {
                                // Important: wrap to swallow strip read exceptions and keep batch alive
                                var err = local.Band.ReadRaster(ix, iy, 1, 1, buf, 1, 1, 0, 0);
                                if (err != CPLErr.CE_None)
                                    throw new ApplicationException("Raster read error.");
                            }
                            catch
                            {
                                Interlocked.Increment(ref sum.Err);
                                bag.Add(new PointOut { GeomId = p.GeomId, Seq = p.Seq, S = p.S, X = p.X, Y = p.Y, Elev = double.NaN, Status = "ERR" });
                                goto NEXT;
                            }

                            var v = buf[0];
                            var status = (local.HasNoData && v.Equals(local.NoData)) ? "NODATA" : "OK";
                            if (status == "NODATA") Interlocked.Increment(ref sum.NoData);
                            else Interlocked.Increment(ref sum.Ok);
                            bag.Add(new PointOut { GeomId = p.GeomId, Seq = p.Seq, S = p.S, X = p.X, Y = p.Y, Elev = v, Status = status });
                        }

                    NEXT:
                        var d = Interlocked.Increment(ref done);
                        progress.MaybeReport(d, total);
                        return local;
                    }
                    catch
                    {
                        Interlocked.Increment(ref sum.Err);
                        bag.Add(new PointOut { GeomId = p.GeomId, Seq = p.Seq, S = p.S, X = p.X, Y = p.Y, Elev = double.NaN, Status = "ERR" });
                        var d = Interlocked.Increment(ref done);
                        progress.MaybeReport(d, total);
                        return local;
                    }
                },
                // local finally
                local =>
                {
                    
                });

            var rows = bag.ToList();
            rows.Sort((a, b) => a.GeomId == b.GeomId ? a.Seq.CompareTo(b.Seq) : a.GeomId.CompareTo(b.GeomId));
            sum.Total = total;
            return (rows, sum);
        }

        private sealed class WorkerState
        {
            public Dataset Ds { get; }
            public Band Band { get; }
            public double[] Inv { get; }
            public bool HasNoData { get; }
            public double NoData { get; }
            public WorkerState(Dataset ds, Band band, double[] inv, bool hasNd, double nd)
            { Ds = ds; Band = band; Inv = inv; HasNoData = hasNd; NoData = nd; }
        }
    }
}
