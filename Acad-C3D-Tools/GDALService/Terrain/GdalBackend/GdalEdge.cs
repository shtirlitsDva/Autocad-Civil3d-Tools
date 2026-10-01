using System.Globalization;

using GDALService.Common;

using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;

using static OSGeo.GDAL.GdalConst;

namespace GDALService.Terrain.GdalBackend;

// The boundary to GDAL. GDAL's C# bindings report failure by throwing
// ApplicationException (with UseExceptions on) or by handing back a null handle,
// and this is the only file that sees either: every call used by the service is
// wrapped once here and comes back as a Result. Nothing above this file catches
// a GDAL exception or tests a GDAL handle for null.
internal static class GdalEdge
{
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

    // gdalwarp from one source into a new file at outPath; the file is complete
    // when this returns (the written dataset is closed here).
    public static Result<ClippedRaster> Warp(string sourcePath, string outPath, string[] arguments)
    {
        try
        {
            using var source = Gdal.OpenEx(sourcePath, (uint)OF_RASTER, null, null, null);
            if (source is null) { return new Fault(FaultKind.Gdal, "GDAL could not open " + sourcePath); }
            using var options = new GDALWarpAppOptions(arguments);
            using var warped = Gdal.Warp(outPath, [source], options, null, null);
            return warped is null
                ? new Fault(FaultKind.Gdal, "gdalwarp wrote nothing to " + outPath)
                : new Ok<ClippedRaster>(new ClippedRaster(outPath, warped.RasterXSize, warped.RasterYSize));
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, "gdalwarp to " + outPath + " failed: " + ex.Message);
        }
    }

    // gdal_contour into a new GeoJSON file at outPath (an existing one is
    // replaced): 3D lines, the height as z and in the field "elev", the
    // raster's NoData left out. The file is complete when this returns.
    public static Result<ContourLines> Contour(string sourcePath, string outPath, double interval)
    {
        try
        {
            using var source = Gdal.OpenEx(sourcePath, (uint)OF_RASTER, null, null, null);
            if (source is null) { return new Fault(FaultKind.Gdal, "GDAL could not open " + sourcePath); }
            using var band = source.GetRasterBand(1);
            band.GetNoDataValue(out double noData, out int hasNoData);

            using var driver = Ogr.GetDriverByName("GeoJSON");
            if (driver is null) { return new Fault(FaultKind.Gdal, "OGR has no GeoJSON driver"); }
            ReplaceTarget(driver, outPath);

            // The GeoJSON writer cannot count what it wrote, so the file is
            // closed first and the lines counted when it is read back.
            using (var target = driver.CreateDataSource(outPath, null))
            {
                if (target is null) { return new Fault(FaultKind.Gdal, "OGR could not create " + outPath); }
                using var srs = new SpatialReference(source.GetProjectionRef());
                using var layer = target.CreateLayer("contour", srs, wkbGeometryType.wkbLineString25D, ["COORDINATE_PRECISION=3"]);
                using (var id = new FieldDefn("ID", FieldType.OFTInteger)) { layer.CreateField(id, 1); }
                using (var elev = new FieldDefn("elev", FieldType.OFTReal)) { layer.CreateField(elev, 1); }

                string[] options =
                [
                    "LEVEL_INTERVAL=" + interval.ToString("R", CultureInfo.InvariantCulture),
                    "ID_FIELD=0", "ELEV_FIELD=1",
                    .. hasNoData != 0 ? ["NODATA=" + noData.ToString("R", CultureInfo.InvariantCulture)] : Array.Empty<string>(),
                ];
                var err = (CPLErr)Gdal.ContourGenerateEx(band, layer, options, null, null);
                if (err != CPLErr.CE_None) { return new Fault(FaultKind.Gdal, $"gdal_contour on {sourcePath} failed: {err}"); }
            }

            using var written = Ogr.Open(outPath, 0);
            if (written is null) { return new Fault(FaultKind.Gdal, "OGR could not reopen " + outPath); }
            using var lines = written.GetLayerByIndex(0);
            long count = lines.GetFeatureCount(1);
            return new Ok<ContourLines>(new ContourLines(outPath, count));
        }
        catch (ApplicationException ex)
        {
            return new Fault(FaultKind.Gdal, "gdal_contour on " + sourcePath + " failed: " + ex.Message);
        }
    }

    // The GeoJSON driver will not overwrite: an old file at the path is
    // deleted first. No file there is the normal case, not a failure.
    private static void ReplaceTarget(OSGeo.OGR.Driver driver, string path)
    {
        try
        {
            using var existing = Ogr.Open(path, 0);
            if (existing is null) { return; }
        }
        catch (ApplicationException)
        {
            return;
        }
        driver.DeleteDataSource(path);
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
