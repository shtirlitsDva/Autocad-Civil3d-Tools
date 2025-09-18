using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static DimensioneringV2.Services.GDALClient.GdalRpcTransport;

namespace DimensioneringV2.Services.GDALClient
{
    internal interface IRpcTransport : IAsyncDisposable
    {
        Task EnsureServerAsync(CancellationToken ct = default);
        /// Sends a command and returns the **raw** response envelope (status/result/error).
        Task<RpcResp> CallAsync(string type, object? payload, CancellationToken ct = default, IProgress<(int done, int total)>? progress = null);

        /// Sends a command and returns a **typed** `result` (throws on nonzero status).
        Task<T> CallAsync<T>(string type, object? payload, CancellationToken ct = default, IProgress<(int done, int total)>? progress = null);
    }
}
