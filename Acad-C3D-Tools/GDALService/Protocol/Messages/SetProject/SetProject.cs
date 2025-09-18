using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Protocol.Messages.SetProject
{
    internal sealed class SetProjectReq
    {
        public string ProjectId { get; set; } = "";
        public string BasePath { get; set; } = "";
    }
    internal sealed class SetProjectRes
    {
        public string ProjectId { get; set; } = "";
        public string ElevationsDir { get; set; } = "";
        public string VrtPath { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int Bands { get; set; }
        public string Projection { get; set; } = "";
    }
}
