using GDALService.Application.Abstractions;
using GDALService.Common;
using GDALService.Infrastructure.Gdal;
using GDALService.Protocol.Messages.Types;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Application.Handlers
{
    internal sealed class SetProjectHandler : IRequestHandler<SetProjectReq, Result<SetProjectRes>>
    {
        private readonly DatasetCache _cache;
        private readonly VrtManager _vrt;

        public SetProjectHandler(DatasetCache cache, VrtManager vrt)
        { _cache = cache; _vrt = vrt; }

        public Task<Result<SetProjectRes>> HandleAsync(SetProjectReq req, CancellationToken ct = default)
        {
            var r = _cache.SetCurrent(req.ProjectId, req.BasePath, _vrt);
            if (!r.Ok)
                return Task.FromResult(Result<SetProjectRes>.Fail(r.Status, r.Error));

            var ctx = r.Value!;
            var res = new SetProjectRes
            {
                ProjectId = ctx.ProjectId,
                ElevationsDir = ctx.ElevationsDir,
                VrtPath = ctx.VrtPath,
                Width = ctx.Dataset.RasterXSize,
                Height = ctx.Dataset.RasterYSize,
                Bands = ctx.Dataset.RasterCount,
                Projection = ctx.Dataset.GetProjectionRef() ?? ""
            };
            return Task.FromResult(Result<SetProjectRes>.Success(res));
        }
    }
}
