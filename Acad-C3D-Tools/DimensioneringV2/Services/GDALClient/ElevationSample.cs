using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.GDALClient
{
    internal readonly record struct ElevationSample(
        double Station,
        double Elevation,
        double X,
        double Y
        );

    public readonly record struct PointXY(double X, double Y);
}
