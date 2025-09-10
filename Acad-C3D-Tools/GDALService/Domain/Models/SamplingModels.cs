using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Domain.Models
{
    internal sealed class PointIn
    {
        public long GeomId { get; set; }
        public int Seq { get; set; }
        public double S { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
    internal sealed class PointOut
    {
        public long GeomId { get; set; }
        public int Seq { get; set; }
        public double S { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Elev { get; set; }
        public string Status { get; set; } = "OK"; // OK|OUTSIDE|NODATA|ERR
    }
}
