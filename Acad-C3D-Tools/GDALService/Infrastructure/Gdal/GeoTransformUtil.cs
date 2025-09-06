using OSGeo.GDAL;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Infrastructure.Gdal
{
    internal static class GeoTransformUtil
    {
        public static (double[] gt, double[] inv) GetTransforms(Dataset ds)
        {
            var gt = new double[6];
            ds.GetGeoTransform(gt);
            var inv = new double[6];
            if (OSGeo.GDAL.Gdal.InvGeoTransform(gt, inv) == 0)
                throw new InvalidOperationException("Cannot invert geotransform.");
            return (gt, inv);
        }
    }
}
