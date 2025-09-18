using GDALService.Domain.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Protocol.Messages.SamplePoints
{
    internal sealed class SamplePointsReq
    {
        public List<PointIn> Points { get; set; } = new();
        public int? Threads { get; set; }
    }
    internal sealed class SamplePointsRes
    {
        public int Total { get; set; }
        public int Ok { get; set; }
        public int Outside { get; set; }
        public int NoData { get; set; }
        public int Err { get; set; }
        public List<PointOut> Rows { get; set; } = new();
    }
}
