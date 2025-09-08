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
        private readonly VrtManager _vrt;

        public SetProjectHandler(VrtManager vrt)
        { _vrt = vrt; }

        public Task<Result<SetProjectRes>> HandleAsync(SetProjectReq req, CancellationToken ct = default)
        {
            var r = _vrt.SetProject(req.ProjectId, req.BasePath);
            if (!r.Ok)
                return Task.FromResult(Result<SetProjectRes>.Fail(r.Status, r.Error));

            var ctx = r.Value!;
            var res = new SetProjectRes
            {                
                ProjectId = ctx.ProjectId,
                ElevationsDir = ctx.ElevationsDir,
                VrtPath = ctx.VrtPath,
                Width = ctx.RasterXSize,
                Height = ctx.RasterYSize,
                Bands = ctx.RasterCount,
                Projection = ctx.Projection,
            };
            return Task.FromResult(Result<SetProjectRes>.Success(res));
        }        
    }
}
