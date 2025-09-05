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
        public LineString? Stik { get; set; }
        public LineString? Vej { get; set; }
        public double Length { get; }
        public OriginalGeometry(LineString? stik, LineString? vej)
        { Stik = stik; Vej = vej; Length = (Stik != null ? Stik.Length : 0) + (Vej != null ? Vej.Length : 0); }
    }
}