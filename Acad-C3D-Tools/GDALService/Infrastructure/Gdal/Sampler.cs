using GDALService.Common;
using GDALService.Domain.Models;

using OSGeo.GDAL;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Infrastructure.Gdal
{
    internal static class Sampler
    {
        internal sealed class Summary { public int Total, Ok, Outside, NoData, Err; }

        public static (List<PointOut> rows, Summary sum) Sample(Dataset ds, IEnumerable<PointIn> pts, int? threads, IProgressReporter progress)
        {
            var band = ds.GetRasterBand(1) ?? throw new InvalidOperationException("No band 1.");
            band.GetNoDataValue(out double nodata, out int hasNd);
            bool hasNoData = hasNd != 0;

            var (_, inv) = GeoTransformUtil.GetTransforms(ds);

            var bag = new ConcurrentBag<PointOut>();
            var sum = new Summary();
            var po = new ParallelOptions
            {
                MaxDegreeOfParallelism = threads.HasValue && threads.Value > 0 ? threads.Value : Math.Max(1, Environment.ProcessorCount - 1)
            };

            int total = 0;
            foreach (var _ in pts) total++;
            int done = 0;

            Parallel.ForEach(pts, po, p =>
            {
                try
                {
                    OSGeo.GDAL.Gdal.ApplyGeoTransform(inv, p.X, p.Y, out double px, out double py);
                    int ix = (int)Math.Floor(px), iy = (int)Math.Floor(py);
                    if (ix < 0 || iy < 0 || ix >= ds.RasterXSize || iy >= ds.RasterYSize)
                    {
                        Interlocked.Increment(ref sum.Outside);
                        bag.Add(new PointOut { GeomId = p.GeomId, Seq = p.Seq, S_M = p.S_M, X = p.X, Y = p.Y, Elev = double.NaN, Status = "OUTSIDE" });
                    }
                    else
                    {
                        double[] buf = new double[1];
                        var err = band.ReadRaster(ix, iy, 1, 1, buf, 1, 1, 0, 0);
                        if (err != CPLErr.CE_None)
                        {
                            Interlocked.Increment(ref sum.Err);
                            bag.Add(new PointOut { GeomId = p.GeomId, Seq = p.Seq, S_M = p.S_M, X = p.X, Y = p.Y, Elev = double.NaN, Status = "ERR" });
                        }
                        else
                        {
                            var v = buf[0];
                            var status = (hasNoData && v.Equals(nodata)) ? "NODATA" : "OK";
                            if (status == "NODATA") Interlocked.Increment(ref sum.NoData); else Interlocked.Increment(ref sum.Ok);
                            bag.Add(new PointOut { GeomId = p.GeomId, Seq = p.Seq, S_M = p.S_M, X = p.X, Y = p.Y, Elev = v, Status = status });
                        }
                    }
                }
                finally
                {
                    var d = Interlocked.Increment(ref done);
                    progress.MaybeReport(d, total);
                }
            });

            sum.Total = total;
            var rows = bag.ToList();
            rows.Sort((a, b) => a.GeomId == b.GeomId ? a.Seq.CompareTo(b.Seq) : string.CompareOrdinal(a.GeomId, b.GeomId));
            return (rows, sum);
        }
    }
}
