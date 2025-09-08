using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    public class OriginalGeometry
    {
        public LineString? Stik { get; }
        public LineString? Vej { get; }
        public LineString FullGeometry { get; }
        public double Length => FullGeometry.Length;
        public OriginalGeometry(LineString? stik, LineString? vej, LineString fullGeometry)
        { Stik = stik; Vej = vej; FullGeometry = fullGeometry; }
    }
}