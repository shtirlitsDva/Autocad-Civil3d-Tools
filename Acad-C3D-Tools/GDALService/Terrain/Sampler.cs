using System.Globalization;

using GDALService.Common;
using GDALService.Domain;

namespace GDALService.Terrain;

// Turns plan points into Samples against any IRaster. It knows nothing about
// GDAL: the pixel reads come through the raster's readers, so every failure a
// read can have is testable with a fake raster.
//
// `sampled` is called once per point or cell, from the worker threads, so the
// caller can report progress; it must be thread-safe.
internal sealed class Sampler
{
    private readonly SamplingOptions _options;

    public Sampler(SamplingOptions options) { _options = options; }

    // The single place a plan point becomes a Sample. A point is inside when its
    // pixel coordinate lies in [0, width) x [0, height); the comparisons are
    // written so that a NaN pixel coordinate also lands outside.
    public static Sample At(RasterInfo info, IPixelReader reader, double x, double y)
    {
        var (u, v) = info.ToPixel.Apply(x, y);
        if (!(u >= 0 && v >= 0 && u < info.Width && v < info.Height)) { return Outside.Instance; }

        return reader.Read((int)Math.Floor(u), (int)Math.Floor(v)) switch
        {
            Ok<double> pixel => Sample.Classify(pixel.Value, info.NoData),
            Fault fault => new ReadFailed(fault.Message),
        };
    }

    // Rows come back in request order; the client matches them by seq.
    public IReadOnlyList<SampledPoint> Points(IRaster raster, IReadOnlyList<PointQuery> points, Action sampled)
    {
        var rows = new SampledPoint[points.Count];
        ForEachIndex(raster, points.Count, sampled, (i, reader) =>
        {
            var q = points[i];
            rows[i] = new SampledPoint(q, SampleWith(raster.Info, reader, q.X, q.Y));
        });
        return rows;
    }

    // A regular grid over the raster's extent, row-major from the south-west
    // corner: rows by ascending y, and within a row by ascending x. The grid's
    // size is only known here, so the caller passes how to report progress for
    // a given total rather than a reporter.
    public Result<IReadOnlyList<GridSample>> Grid(IRaster raster, double gridDist, Func<int, Action> sampledOf)
    {
        var info = raster.Info;
        var corners = new[]
        {
            info.ToGeo.Apply(0, 0),
            info.ToGeo.Apply(info.Width, 0),
            info.ToGeo.Apply(0, info.Height),
            info.ToGeo.Apply(info.Width, info.Height),
        };
        double minX = corners.Min(c => c.X), maxX = corners.Max(c => c.X);
        double minY = corners.Min(c => c.Y), maxY = corners.Max(c => c.Y);

        double columns = Math.Floor((maxX - minX) / gridDist) + 1;
        double rowsCount = Math.Floor((maxY - minY) / gridDist) + 1;
        if (!(columns * rowsCount <= _options.MaxGridPoints))
        {
            // Invariant: a wire message never carries the PC's decimal comma.
            return new Fault(FaultKind.InvalidArgs, string.Create(CultureInfo.InvariantCulture,
                $"gridDist {gridDist} m asks for {columns * rowsCount:0} points; at most {_options.MaxGridPoints} are allowed"));
        }

        int nx = (int)columns, ny = (int)rowsCount;
        var cells = new GridSample[nx * ny];
        ForEachIndex(raster, cells.Length, sampledOf(cells.Length), (i, reader) =>
        {
            double x = minX + (i % nx) * gridDist, y = minY + (i / nx) * gridDist;
            cells[i] = new GridSample(x, y, SampleWith(info, reader, x, y));
        });
        return new Ok<IReadOnlyList<GridSample>>(cells);
    }

    // A worker whose reader could not be opened still answers every point it is
    // given - as a read failure - so one bad worker never fails the batch.
    private static Sample SampleWith(RasterInfo info, Result<IPixelReader> reader, double x, double y) => reader switch
    {
        Ok<IPixelReader> ok => At(info, ok.Value, x, y),
        Fault fault => new ReadFailed(fault.Message),
    };

    // Each worker opens its own reader and releases it when the worker finishes.
    private void ForEachIndex(IRaster raster, int count, Action sampled, Action<int, Result<IPixelReader>> body)
    {
        var options = new ParallelOptions { MaxDegreeOfParallelism = _options.EffectiveWorkers };
        Parallel.For(0, count, options,
            raster.OpenReader,
            (i, _, reader) =>
            {
                body(i, reader);
                sampled();
                return reader;
            },
            reader => reader.Switch(open => open.Dispose(), _ => { }));
    }
}
