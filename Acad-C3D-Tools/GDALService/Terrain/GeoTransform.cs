using GDALService.Common;

namespace GDALService.Terrain;

// GDAL's six-coefficient affine transform, pixel/line <-> georeferenced. Pure
// arithmetic, so it lives outside the GDAL edge and is tested on its own.
internal readonly record struct GeoTransform(double C0, double C1, double C2, double C3, double C4, double C5)
{
    public static GeoTransform FromArray(double[] gt) => new(gt[0], gt[1], gt[2], gt[3], gt[4], gt[5]);

    public (double X, double Y) Apply(double u, double v) =>
        (C0 + u * C1 + v * C2, C3 + u * C4 + v * C5);

    // The same inversion as GDALInvGeoTransform: a transform whose linear part
    // is singular maps the plane onto a line and has no inverse.
    public Option<GeoTransform> Invert()
    {
        double det = C1 * C5 - C2 * C4;
        if (det == 0.0 || !double.IsFinite(det)) { return None.Instance; }

        double inv = 1.0 / det;
        return new Some<GeoTransform>(new GeoTransform(
            (C2 * C3 - C0 * C5) * inv,
            C5 * inv,
            -C2 * inv,
            (-C1 * C3 + C0 * C4) * inv,
            -C4 * inv,
            C1 * inv));
    }
}
