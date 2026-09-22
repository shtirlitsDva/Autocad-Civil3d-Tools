using GDALService.Common;

using OSGeo.GDAL;

namespace GDALService.Terrain.GdalBackend;

// GDAL's start-up, once per process: loads the native libraries from gdal\ next
// to the executable and switches GDAL to its exception mode, so its errors have
// one known shape for GdalEdge to catch. A GDAL that cannot load is a Fault,
// not a crash - the service then runs without it.
internal static class GdalBootstrap
{
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
}
