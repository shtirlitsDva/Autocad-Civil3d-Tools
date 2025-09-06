using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Protocol.Messages
{
    internal sealed class Request
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Type { get; set; } = "";
        public object? Payload { get; set; }
    }
}
