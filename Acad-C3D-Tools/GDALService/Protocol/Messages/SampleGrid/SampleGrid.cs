using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Protocol.Messages.SampleGrid
{
    // Request: just the grid distance + optional thread count
    internal sealed class SampleGridReq
    {
        public double GridDist { get; set; }
        public int? Threads { get; set; }
    }

    // Response: summary + collection of sampled points
    internal sealed class SampleGridRes
    {
        public int Total { get; set; }
        public int Ok { get; set; }
        public int Outside { get; set; }
        public int NoData { get; set; }
        public int Err { get; set; }

        public List<GridPointDto> Rows { get; set; } = new();
    }

    internal sealed class GridPointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Status { get; set; } = "OK"; // OK|OUTSIDE|NODATA|ERR
    }
}
