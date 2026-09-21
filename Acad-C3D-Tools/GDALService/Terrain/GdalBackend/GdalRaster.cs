using GDALService.Common;

using OSGeo.GDAL;

namespace GDALService.Terrain.GdalBackend;

// A GDAL dataset opened thread-safe over an in-memory VRT, and checked once: it
// has a band 1 and an invertible geotransform. Everything a sampler needs is
// decided here, so the per-point loop never meets a missing band or a singular
// transform. Disposing it closes the dataset and releases the in-memory VRT.
internal sealed class GdalRaster : IRaster
{
    private readonly Dataset _dataset;
    private readonly ServiceLog _log;

    public RasterInfo Info { get; }

    private GdalRaster(Dataset dataset, RasterInfo info, ServiceLog log)
    {
        _dataset = dataset;
        Info = info;
        _log = log;
    }

    // Takes ownership of the VRT at vrtPath: released on failure here, or when
    // the raster is disposed.
    public static Result<IRaster> Open(string vrtPath, ServiceLog log) =>
        GdalEdge.OpenThreadSafe(vrtPath) switch
        {
            Ok<Dataset> opened => Describe(opened.Value, vrtPath) switch
            {
                Ok<RasterInfo> info => new Ok<IRaster>(new GdalRaster(opened.Value, info.Value, log)),
                Fault fault => CloseAndFail(opened.Value, vrtPath, fault, log),
            },
            Fault fault => ReleaseAndFail(vrtPath, fault, log),
        };

    public Result<IPixelReader> OpenReader() =>
        GdalEdge.FirstBand(_dataset).Map(band => (IPixelReader)new GdalPixelReader(band));

    public void Dispose()
    {
        _dataset.Dispose();
        Release(Info.Source, _log);
    }

    private static Result<RasterInfo> Describe(Dataset ds, string path) =>
        GdalEdge.GeoTransformOf(ds).Bind(toGeo =>
            toGeo.Invert() switch
            {
                Some<GeoTransform> toPixel => NoDataOfFirstBand(ds).Map(noData =>
                    new RasterInfo(path, ds.RasterXSize, ds.RasterYSize, ds.RasterCount,
                                   toGeo, toPixel.Value, noData, GdalEdge.ProjectionOf(ds))),
                None => new Fault(FaultKind.Gdal, "the raster's geotransform cannot be inverted"),
            });

    private static Result<Option<double>> NoDataOfFirstBand(Dataset ds) =>
        GdalEdge.FirstBand(ds).Bind(band =>
        {
            using (band) { return GdalEdge.NoDataOf(band); }
        });

    private static Result<IRaster> CloseAndFail(Dataset ds, string vrtPath, Fault fault, ServiceLog log)
    {
        ds.Dispose();
        return ReleaseAndFail(vrtPath, fault, log);
    }

    private static Result<IRaster> ReleaseAndFail(string vrtPath, Fault fault, ServiceLog log)
    {
        Release(vrtPath, log);
        return fault;
    }

    // An in-memory VRT that cannot be released only costs memory; it is logged,
    // not treated as a failure of whatever released it.
    private static void Release(string vrtPath, ServiceLog log) =>
        GdalEdge.Unlink(vrtPath).Switch(_ => { }, fault => log.Warn(fault.Message));
}
