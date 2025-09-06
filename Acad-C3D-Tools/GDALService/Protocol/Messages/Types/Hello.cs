using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Protocol.Messages.Types
{
    internal sealed class HelloReq { }
    internal sealed class HelloRes { public string Msg { get; set; } = "HELLO_ACK"; }
}