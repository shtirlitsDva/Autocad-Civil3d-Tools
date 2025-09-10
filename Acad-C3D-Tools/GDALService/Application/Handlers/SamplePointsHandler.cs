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
        private readonly ServiceOptions _options;
        private readonly VrtManager _vrt;
        public SamplePointsHandler(VrtManager vrt, ServiceOptions options)
        { 
            _vrt = vrt;
            _options = options;
        }

        public Task<Result<SamplePointsRes>> HandleAsync(SamplePointsReq req, CancellationToken ct = default)
        {
            var ctxRes = _vrt.TryGetDataset();
            if (!ctxRes.Ok)
                return Task.FromResult(Result<SamplePointsRes>.Fail(ctxRes.Status, ctxRes.Error));

            var ctx = ctxRes.Value!;
            var reporter = new BatchProgressReporter(_options.ProgressEvery); // or inject via options
            var (rows, sum) = Sampler.Sample(
                ds: ctx.Ds,
                pts: req.Points.Select(q => new PointIn { GeomId = q.GeomId, Seq = q.Seq, S = q.S, X = q.X, Y = q.Y }),
                threads: req.Threads,
                progress: reporter
            );

            var outRows = rows.Select(r => new Domain.Models.PointOut
            { GeomId = r.GeomId, Seq = r.Seq, S = r.S, X = r.X, Y = r.Y, Elev = r.Elev, Status = r.Status }).ToList();

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
