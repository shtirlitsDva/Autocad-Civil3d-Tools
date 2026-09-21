using GDALService.Common;

using OSGeo.GDAL;

namespace GDALService.Raster;

// A raster that has been opened and checked once: it has a band 1 and an
// invertible geotransform. Everything a sampler needs is decided here, so the
// per-point loop never meets a missing band or a singular transform.
internal sealed class OpenRaster : IDisposable
{
    public Dataset Dataset { get; }
    public string Path { get; }
    public GeoTransform ToGeo { get; }
    public GeoTransform ToPixel { get; }
    public int Width { get; }
    public int Height { get; }
    public int Bands { get; }
    public Option<double> NoData { get; }
    public Option<string> Projection { get; }

    private OpenRaster(Dataset dataset, string path, GeoTransform toGeo, GeoTransform toPixel,
                       Option<double> noData, Option<string> projection)
    {
        Dataset = dataset;
        Path = path;
        ToGeo = toGeo;
        ToPixel = toPixel;
        Width = dataset.RasterXSize;
        Height = dataset.RasterYSize;
        Bands = dataset.RasterCount;
        NoData = noData;
        Projection = projection;
    }

    public static Result<OpenRaster> Open(string path) =>
        GdalEdge.OpenThreadSafe(path) switch
        {
            Ok<Dataset> opened => Describe(opened.Value, path) switch
            {
                Ok<OpenRaster> raster => raster,
                Fault fault => DisposeAndFail(opened.Value, fault),
            },
            Fault fault => fault,
        };

    private static Result<OpenRaster> Describe(Dataset ds, string path) =>
        GdalEdge.GeoTransformOf(ds).Bind(toGeo =>
            toGeo.Invert() switch
            {
                Some<GeoTransform> toPixel => NoDataOfFirstBand(ds).Map(noData =>
                    new OpenRaster(ds, path, toGeo, toPixel.Value, noData, GdalEdge.ProjectionOf(ds))),
                None => new Fault(FaultKind.Gdal, "the raster's geotransform cannot be inverted"),
            });

    private static Result<Option<double>> NoDataOfFirstBand(Dataset ds) =>
        GdalEdge.FirstBand(ds).Bind(band =>
        {
            using (band) { return GdalEdge.NoDataOf(band); }
        });

    private static Result<OpenRaster> DisposeAndFail(Dataset ds, Fault fault)
    {
        ds.Dispose();
        return fault;
    }

    public void Dispose() => Dataset.Dispose();
}
