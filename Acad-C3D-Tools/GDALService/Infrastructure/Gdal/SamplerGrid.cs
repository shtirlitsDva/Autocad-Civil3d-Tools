using GDALService.Common;
using GDALService.Domain.Models;

using OSGeo.GDAL;

using System.Collections.Concurrent;

namespace GDALService.Infrastructure.Gdal
{
    internal static partial class Sampler
    {

        public static (List<PointOut> rows, Summary sum) SampleGrid(
            Dataset ds,
            double gridDist,
            int? threads,
            IProgressReporter progress)
        {
            if (gridDist <= 0) throw new ArgumentOutOfRangeException(nameof(gridDist));

            var gt = new double[6];
            ds.GetGeoTransform(gt);

            // helper to convert pixel -> geo
            static (double X, double Y) PxToGeo(double[] g, double px, double py)
                => (g[0] + px * g[1] + py * g[2], g[3] + px * g[4] + py * g[5]);

            // corners in geo space
            var c00 = PxToGeo(gt, 0, 0);
            var c10 = PxToGeo(gt, ds.RasterXSize, 0);
            var c01 = PxToGeo(gt, 0, ds.RasterYSize);
            var c11 = PxToGeo(gt, ds.RasterXSize, ds.RasterYSize);

            double minX = new[] { c00.X, c10.X, c01.X, c11.X }.Min();
            double maxX = new[] { c00.X, c10.X, c01.X, c11.X }.Max();
            double minY = new[] { c00.Y, c10.Y, c01.Y, c11.Y }.Min();
            double maxY = new[] { c00.Y, c10.Y, c01.Y, c11.Y }.Max();

            int nx = Math.Max(1, (int)Math.Floor((maxX - minX) / gridDist) + 1);
            int ny = Math.Max(1, (int)Math.Floor((maxY - minY) / gridDist) + 1);
            int total = checked(nx * ny);

            IEnumerable<PointIn> Generate()
            {
                int seq = 0;
                for (int iy = 0; iy < ny; iy++)
                {
                    double y = minY + iy * gridDist;
                    for (int ix = 0; ix < nx; ix++)
                    {
                        double x = minX + ix * gridDist;
                        yield return new PointIn { GeomId = 0, Seq = seq++, S = 0.0, X = x, Y = y };
                    }
                }
            }

            var bag = new ConcurrentBag<PointOut>();
            var sum = new Summary();
            int done = 0;

            var po = new ParallelOptions
            {
                MaxDegreeOfParallelism = threads.HasValue && threads.Value > 0
                    ? threads.Value : Math.Max(1, Environment.ProcessorCount - 1)
            };

            var inv = new double[6];
            if (OSGeo.GDAL.Gdal.InvGeoTransform(gt, inv) == 0)
                throw new InvalidOperationException("Cannot invert geotransform.");

            Parallel.ForEach(Partitioner.Create(Generate()), po,
                () =>
                {
                    var band = ds.GetRasterBand(1) ?? throw new InvalidOperationException("No band 1.");
                    band.GetNoDataValue(out double nodata, out int hasNd);
                    return new WorkerState(ds, band, inv, hasNd != 0, nodata);
                },
                (p, loopState, local) =>
                {
                    try
                    {
                        OSGeo.GDAL.Gdal.ApplyGeoTransform(local.Inv, p.X, p.Y, out double px, out double py);
                        int ix = (int)Math.Floor(px), iy = (int)Math.Floor(py);

                        if (ix < 0 || iy < 0 || ix >= local.Ds.RasterXSize || iy >= local.Ds.RasterYSize)
                        {
                            Interlocked.Increment(ref sum.Outside);
                            bag.Add(new PointOut { X = p.X, Y = p.Y, Elev = double.NaN, Status = "OUTSIDE" });
                        }
                        else
                        {
                            double[] buf = new double[1];
                            try
                            {
                                var err = local.Band.ReadRaster(ix, iy, 1, 1, buf, 1, 1, 0, 0);
                                if (err != CPLErr.CE_None)
                                    throw new ApplicationException("Raster read error.");
                            }
                            catch
                            {
                                Interlocked.Increment(ref sum.Err);
                                bag.Add(new PointOut { X = p.X, Y = p.Y, Elev = double.NaN, Status = "ERR" });
                                goto REPORT;
                            }

                            var v = buf[0];
                            var status = (local.HasNoData && v.Equals(local.NoData)) ? "NODATA" : "OK";
                            if (status == "NODATA") Interlocked.Increment(ref sum.NoData);
                            else Interlocked.Increment(ref sum.Ok);
                            bag.Add(new PointOut { X = p.X, Y = p.Y, Elev = v, Status = status });
                        }

                    REPORT:
                        var d = Interlocked.Increment(ref done);
                        progress.MaybeReport(d, total);
                        return local;
                    }
                    catch
                    {
                        Interlocked.Increment(ref sum.Err);
                        bag.Add(new PointOut { X = p.X, Y = p.Y, Elev = double.NaN, Status = "ERR" });
                        var d = Interlocked.Increment(ref done);
                        progress.MaybeReport(d, total);
                        return local;
                    }
                },
                _ => { });

            var rows = bag.ToList();
            rows.Sort((a, b) => a.Seq.CompareTo(b.Seq)); // row-major order
            sum.Total = total;
            return (rows, sum);
        }        
    }
}
