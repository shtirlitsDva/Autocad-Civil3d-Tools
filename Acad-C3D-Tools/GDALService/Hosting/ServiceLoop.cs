using GDALService.Application.Abstractions;
using GDALService.Application.Handlers;
using GDALService.Common;
using GDALService.Configuration;
using GDALService.Domain.Models;
using GDALService.Infrastructure.Gdal;
using GDALService.Protocol.Messages;
using GDALService.Protocol.Messages.Types;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GDALService.Hosting
{
    internal sealed class ServiceLoop
    {
        private readonly ServiceOptions _opt;
        private readonly DatasetCache _cache = new();
        private readonly VrtManager _vrt = new();

        public ServiceLoop(ServiceOptions opt) { _opt = opt; }

        public async Task<int> RunAsync()
        {
            StdIo.WriteErr($"READY gdal={OSGeo.GDAL.Gdal.VersionInfo("RELEASE_NAME")}");

            string? line;
            while ((line = Console.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                Response resp;
                try
                {
                    var req = JsonSerializer.Deserialize<Request>(line, Protocol.Json.Options)!;
                    resp = await DispatchAsync(req);
                }
                catch (Exception ex)
                {
                    resp = new Response { Id = "(none)", Status = (int)StatusCode.Error, Error = "Bad JSON: " + ex.Message };
                }

                StdIo.WriteOut(JsonSerializer.Serialize(resp, Protocol.Json.Options));
            }
            return 0;
        }

        private async Task<Response> DispatchAsync(Request req)
        {
            try
            {
                switch (req.Type)
                {
                    case "HELLO":
                        return new Response { Id = req.Id, Status = (int)StatusCode.Success, Result = new HelloRes() };

                    case "SET_PROJECT":
                        {
                            var p = Protocol.Json.Extract<SetProjectReq>(req.Payload);
                            IRequestHandler<SetProjectReq, Result<SetProjectRes>> h
                                = new SetProjectHandler(_cache, _vrt);
                            var r = await h.HandleAsync(p);
                            return new Response { Id = req.Id, Status = (int)r.Status, Result = r.Value, Error = r.Error };
                        }

                    case "SAMPLE_POINTS":
                        {
                            var p = Protocol.Json.Extract<SamplePointsReq>(req.Payload);
                            IRequestHandler<SamplePointsReq, Result<SamplePointsRes>> h
                                = new SamplePointsHandler(_cache, _opt);
                            var r = await h.HandleAsync(p);
                            return new Response { Id = req.Id, Status = (int)r.Status, Result = r.Value, Error = r.Error };
                        }

                    case "SHUTDOWN":
                        return new Response { Id = req.Id, Status = (int)StatusCode.Success, Result = new { msg = "BYE" } };

                    default:
                        return new Response { Id = req.Id, Status = (int)StatusCode.InvalidArgs, Error = $"Unknown type '{req.Type}'" };
                }
            }
            catch (Exception ex)
            {
                // Should be rare; most paths now return Result<> instead of throwing
                return new Response { Id = req.Id, Status = (int)StatusCode.Error, Error = ex.ToString() };
            }
        }
    }
}
