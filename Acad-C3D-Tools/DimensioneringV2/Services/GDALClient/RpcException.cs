using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.GDALClient
{
    internal class RpcException : Exception
    {
        public string Command { get; }
        public string RpcId { get; }
        public int Status { get; }

        public RpcException(string command, string rpcId, int status, string message)
            : base($"{command} failed (status {status}): {message}")
        {
            Command = command;
            RpcId = rpcId;
            Status = status;
        }
    }
}
