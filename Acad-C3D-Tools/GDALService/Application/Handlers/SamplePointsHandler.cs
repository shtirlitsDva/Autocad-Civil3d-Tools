using GDALService.Application.Abstractions;
using GDALService.Common;
using GDALService.Configuration;
using GDALService.Domain.Models;
using GDALService.Infrastructure.Gdal;
using GDALService.Protocol.Messages.Types;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Application.Handlers
{
    internal class SamplePointsHandler : IRequestHandler<SamplePointsReq, Result<SamplePointsRes>>
    {
        private readonly DatasetCache _cache;
        private readonly ServiceOptions _options;
        public SamplePointsHandler(DatasetCache cache, ServiceOptions options)
        { 
            _cache = cache;
            _options = options;
        }

        public Task<Result<SamplePointsRes>> HandleAsync(SamplePointsReq req, CancellationToken ct = default)
        {
            var ctxRes = _cache.GetCurrent();
            if (!ctxRes.Ok)
                return Task.FromResult(Result<SamplePointsRes>.Fail(ctxRes.Status, ctxRes.Error));

            var ds = ctxRes.Value!.Dataset;

            var reporter = new BatchProgressReporter(_options.ProgressEvery); // or inject via options
            var (rows, sum) = Sampler.Sample(
                ds: ds,
                pts: req.Points.Select(q => new PointIn { GeomId = q.GeomId, Seq = q.Seq, S_M = q.S_M, X = q.X, Y = q.Y }),
                threads: req.Threads,
                progress: reporter
            );

            var outRows = rows.Select(r => new Domain.Models.PointOut
            { GeomId = r.GeomId, Seq = r.Seq, S_M = r.S_M, X = r.X, Y = r.Y, Elev = r.Elev, Status = r.Status }).ToList();

            var res = new SamplePointsRes
            {
                Total = sum.Total,
                Ok = sum.Ok,
                Outside = sum.Outside,
                NoData = sum.NoData,
                Err = sum.Err,
                Rows = outRows
            };

            return Task.FromResult(Result<SamplePointsRes>.Success(res));
        }
    }
}
