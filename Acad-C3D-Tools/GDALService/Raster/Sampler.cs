using System.Globalization;

using GDALService.Common;
using GDALService.Domain;
using GDALService.Hosting;

using OSGeo.GDAL;

namespace GDALService.Raster;

internal static class Sampler
{
    // SAMPLE_GRID refuses a spacing that would ask for more points than this,
    // before anything is allocated.
    public const int MaxGridPoints = 5_000_000;

    // The single place a plan point becomes a Sample. A point is inside when its
    // pixel coordinate lies in [0, width) x [0, height); the comparisons are
    // written so that a NaN pixel coordinate also lands outside.
    public static Sample At(OpenRaster raster, Band band, double x, double y)
    {
        var (u, v) = raster.ToPixel.Apply(x, y);
        if (!(u >= 0 && v >= 0 && u < raster.Width && v < raster.Height)) { return Outside.Instance; }

        return GdalEdge.ReadPixel(band, (int)Math.Floor(u), (int)Math.Floor(v)) switch
        {
            Ok<double> pixel => Sample.Classify(pixel.Value, raster.NoData),
            Fault fault => new ReadFailed(fault.Message),
        };
    }

    // Rows come back in request order; the client matches them by seq.
    public static IReadOnlyList<SampledPoint> Points(OpenRaster raster, IReadOnlyList<PointQuery> points, Progress progress)
    {
        var rows = new SampledPoint[points.Count];
        ForEachIndex(raster, points.Count, progress, (i, band) =>
        {
            var q = points[i];
            rows[i] = new SampledPoint(q, SampleWith(raster, band, q.X, q.Y));
        });
        return rows;
    }

    // A regular grid over the raster's extent, row-major from the south-west
    // corner: rows by ascending y, and within a row by ascending x.
    // The grid's size is only known here, so the caller passes how to make a
    // progress reporter for it rather than a reporter.
    public static Result<IReadOnlyList<GridSample>> Grid(OpenRaster raster, double gridDist, Func<int, Progress> progressFor)
    {
        var corners = new[]
        {
            raster.ToGeo.Apply(0, 0),
            raster.ToGeo.Apply(raster.Width, 0),
            raster.ToGeo.Apply(0, raster.Height),
            raster.ToGeo.Apply(raster.Width, raster.Height),
        };
        double minX = corners.Min(c => c.X), maxX = corners.Max(c => c.X);
        double minY = corners.Min(c => c.Y), maxY = corners.Max(c => c.Y);

        double columns = Math.Floor((maxX - minX) / gridDist) + 1;
        double rowsCount = Math.Floor((maxY - minY) / gridDist) + 1;
        if (!(columns * rowsCount <= MaxGridPoints))
        {
            // Invariant: a wire message never carries the PC's decimal comma.
            return new Fault(FaultKind.InvalidArgs, string.Create(CultureInfo.InvariantCulture,
                $"gridDist {gridDist} m asks for {columns * rowsCount:0} points; at most {MaxGridPoints} are allowed"));
        }

        int nx = (int)columns, ny = (int)rowsCount;
        var cells = new GridSample[nx * ny];
        ForEachIndex(raster, cells.Length, progressFor(cells.Length), (i, band) =>
        {
            double x = minX + (i % nx) * gridDist, y = minY + (i / nx) * gridDist;
            cells[i] = new GridSample(x, y, SampleWith(raster, band, x, y));
        });
        return new Ok<IReadOnlyList<GridSample>>(cells);
    }

    // A worker whose band could not be fetched still answers every point it is
    // given - as a read failure - so one bad worker never fails the batch.
    private static Sample SampleWith(OpenRaster raster, Result<Band> band, double x, double y) => band switch
    {
        Ok<Band> ok => At(raster, ok.Value, x, y),
        Fault fault => new ReadFailed(fault.Message),
    };

    // Each worker fetches its own band from the thread-safe dataset, as GDAL's
    // OF_THREAD_SAFE mode expects, and releases it when the worker finishes.
    private static void ForEachIndex(OpenRaster raster, int count, Progress progress, Action<int, Result<Band>> body)
    {
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
        Parallel.For(0, count, options,
            () => GdalEdge.FirstBand(raster.Dataset),
            (i, _, band) =>
            {
                body(i, band);
                progress.Advance();
                return band;
            },
            band => band.Switch(ok => ok.Dispose(), _ => { }));
    }
}
