using DimensioneringV2.Common;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.GDALClient
{
    internal sealed class SampleGridDispatcher : DispatcherBase
    {
        private sealed record Payload(double gridDist, int threads);
        private sealed record GridRowsDto(Row[] rows);
        private sealed record Row(double x, double y, double z);

        public async Task<OpResult<int>> SampleGridAsync(
            List<(double X, double Y, double E)> gridPoints,
            double gridDist,
            int? maxThreads = null,
            IProgress<(int done, int total)>? progress = null,
            CancellationToken ct = default)
        {
            if (gridPoints is null) return OpResult<int>.Fail("gridPoints must not be null.");
            if (gridDist <= 0) return OpResult<int>.Fail("Grid distance skal være > 0 m.");

            var ensure = await EnsureReadyAndProjectAsync(ct).ConfigureAwait(false);
            if (!ensure.Ok) return OpResult<int>.Fail(ensure.Error!);

            try
            {
                var dto = await Rpc.CallAsync<GridRowsDto>(
                    type: "SAMPLE_GRID",
                    payload: new Payload(gridDist, threads: maxThreads ?? 0),
                    ct: ct,
                    progress: progress // <-- THIS routes stderr PROGRESS to your UI
                ).ConfigureAwait(false);

                gridPoints.Clear();
                if (dto.rows is { Length: > 0 })
                {
                    gridPoints.Capacity = Math.Max(gridPoints.Capacity, dto.rows.Length);
                    foreach (var r in dto.rows)
                        gridPoints.Add((r.x, r.y, r.z));
                }

                return OpResult<int>.Success(gridPoints.Count);
            }
            catch (RpcException ex)
            {
                return OpResult<int>.Fail($"SAMPLE_GRID fejlede: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                return OpResult<int>.Fail("SAMPLE_GRID blev annulleret.");
            }
            catch (Exception ex)
            {
                return OpResult<int>.Fail($"SAMPLE_GRID fejl: {ex.Message}");
            }
        }        
    }
}
