using GDALService.Application.Abstractions;
using GDALService.Common;
using GDALService.Configuration;
using GDALService.Domain.Models;
using GDALService.Infrastructure.Gdal;
using GDALService.Protocol.Messages.SampleGrid;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Application.Handlers
{
    internal class SampleGridHandler : IRequestHandler<SampleGridReq, Result<SampleGridRes>>
    {
        private readonly ServiceOptions _options;
        private readonly VrtManager _vrt;
        private readonly string _reqId;
        public SampleGridHandler(VrtManager vrt, ServiceOptions options, string requestId)
        { 
            _vrt = vrt;
            _options = options;
            _reqId = requestId;
        }

        public Task<Result<SampleGridRes>> HandleAsync(SampleGridReq req, CancellationToken ct = default)
        {
            if (req.GridDist <= 0)
                return Task.FromResult(Result<SampleGridRes>.Fail(
                    StatusCode.InvalidArgs, "Grid distance must be > 0 m."));

            var ctxRes = _vrt.TryGetDataset();
            if (!ctxRes.Ok)
                return Task.FromResult(Result<SampleGridRes>.Fail(ctxRes.Status, ctxRes.Error));

            var ctx = ctxRes.Value!;
            var ds = ctx.Ds;
            var reporter = new JsonProgressReporter(_reqId, _options.ProgressEvery);

            var (rows, sum) = Sampler.SampleGrid(
                ds: ctx.Ds,
                gridDist: req.GridDist,
                threads: req.Threads,
                progress: reporter
            );            

            var res = new SampleGridRes
            {
                Total = sum.Total,
                Ok = sum.Ok,
                Outside = sum.Outside,
                NoData = sum.NoData,
                Err = sum.Err,
                Rows = rows.Select(r => new GridPointDto
                {
                    X = r.X,
                    Y = r.Y,
                    Z = r.Elev,
                    Status = r.Status
                }).ToList()
            };

            return Task.FromResult(Result<SampleGridRes>.Success(res));
        }
    }
}
