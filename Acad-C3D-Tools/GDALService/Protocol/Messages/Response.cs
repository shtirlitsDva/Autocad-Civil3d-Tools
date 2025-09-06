using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Protocol.Messages
{
    internal sealed class Response
    {
        public string Id { get; set; } = "";
        public int Status { get; set; } = 0; // OK|ERROR
        public object? Result { get; set; }
        public string? Error { get; set; }
    }
}
