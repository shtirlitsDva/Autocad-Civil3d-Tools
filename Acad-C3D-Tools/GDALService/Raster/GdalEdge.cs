using GDALService.Common;

using OSGeo.GDAL;

using static OSGeo.GDAL.GdalConst;

namespace GDALService.Raster;

// The boundary to GDAL. GDAL's C# bindings report failure by throwing
// ApplicationException (with UseExceptions on) or by handing back a null handle,
// and this is the only file that sees either: every call used by the service is
// wrapped once here and comes back as a Result. Nothing above this file catches
// a GDAL exception or tests a GDAL handle for null.
internal static class GdalEdge
{
    // Loads the native libraries, switches GDAL to its exception mode so its
    // errors have one known shape, and returns the GDAL release name.
    public static Result<string> Initialise()
    {
        try
        {
            GdalConfiguration.ConfigureGdal();
            if (!GdalConfiguration.Usable)
            {
                return new Fault(FaultKind.Gdal, "GDAL native libraries are not usable from " + AppContext.BaseDirectory);
            }
            Gdal.UseExceptions();
            return new Ok<string>(Gdal.VersionInfo("RELEASE_NAME"));
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException
                                   or BadImageFormatException or ApplicationException)
        {
            return new Fault(FaultKind.Gdal, "GDAL could not be loaded: " + ex.Message);
        }
    }

    // Builds a VRT mosaic of the tiles at vrtPath (a /vsimem/ path) and closes
    // it again, so the VRT is flushed and can be reopened thread-safe.
    public static Result<string> BuildVrt(string vrtPath, IReadOnlyList<string> tiles)
    {
        try
        {
            using var options = new GDALBuildVRTOptions(["-resolution", "highest"]);
            using var built = Gdal.wrapper_GDALBuildVRT_names(vrtPath, [.. tiles], options, null, null);
            return built is null
                ? new Fault(FaultKind.Gdal, "GDALBuildVRT returned no dataset for " + vrtPath)
                : new Ok<string>(vrtPath);
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, "GDALBuildVRT failed: " + ex.Message);
        }
    }

    public static Result<Dataset> OpenThreadSafe(string path)
    {
        try
        {
            var ds = Gdal.OpenEx(path, (uint)(OF_RASTER | OF_THREAD_SAFE), null, null, null);
            if (ds is null) { return new Fault(FaultKind.Gdal, "GDAL could not open " + path); }
            if (!ds.IsThreadSafe((int)OF_RASTER))
            {
                ds.Dispose();
                return new Fault(FaultKind.Gdal, "GDAL opened " + path + " but not thread-safe");
            }
            return new Ok<Dataset>(ds);
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, "GDAL could not open " + path + ": " + ex.Message);
        }
    }

    public static Result<Band> FirstBand(Dataset ds)
    {
        try
        {
            var band = ds.GetRasterBand(1);
            return band is null
                ? new Fault(FaultKind.Gdal, "the raster has no band 1")
                : new Ok<Band>(band);
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, "the raster has no band 1: " + ex.Message);
        }
    }

    public static Result<GeoTransform> GeoTransformOf(Dataset ds)
    {
        try
        {
            var gt = new double[6];
            ds.GetGeoTransform(gt);
            return new Ok<GeoTransform>(GeoTransform.FromArray(gt));
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, "the raster has no geotransform: " + ex.Message);
        }
    }

    public static Result<Option<double>> NoDataOf(Band band)
    {
        try
        {
            band.GetNoDataValue(out double value, out int hasValue);
            return new Ok<Option<double>>(hasValue != 0 ? new Some<double>(value) : None.Instance);
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, "the band's NoData value cannot be read: " + ex.Message);
        }
    }

    // A raster without a spatial reference answers with an empty string, which
    // is an absence, not a projection.
    public static Option<string> ProjectionOf(Dataset ds)
    {
        try
        {
            return ds.GetProjectionRef() is string wkt && wkt.Length > 0
                ? new Some<string>(wkt)
                : None.Instance;
        }
        catch (ApplicationException)
        {
            return None.Instance;
        }
    }

    public static Result<double> ReadPixel(Band band, int column, int row)
    {
        try
        {
            var buffer = new double[1];
            var err = band.ReadRaster(column, row, 1, 1, buffer, 1, 1, 0, 0);
            return err == CPLErr.CE_None
                ? new Ok<double>(buffer[0])
                : new Fault(FaultKind.Gdal, $"raster read failed at pixel ({column}, {row}): {err}");
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, $"raster read failed at pixel ({column}, {row}): {ex.Message}");
        }
    }

    public static Result<string> Unlink(string path)
    {
        try
        {
            return Gdal.Unlink(path) == 0
                ? new Ok<string>(path)
                : new Fault(FaultKind.Gdal, "could not release " + path);
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, "could not release " + path + ": " + ex.Message);
        }
    }
}
