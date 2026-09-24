using System.Text;

using GDALService.Common;
using GDALService.Hosting;
using GDALService.Terrain;
using GDALService.Terrain.GdalBackend;

using Microsoft.Extensions.DependencyInjection;

namespace GDALService;

internal static class Program
{
    private static int Main()
    {
        // Before Console.In is first touched: a basePath with æ/ø/å arrives as
        // UTF-8 from the client and must not be read in the OEM code page.
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        var streams = new ServiceStreams(Console.In, Console.Out, Console.Error);
        using var provider = ServiceComposition.Build(streams, GdalBootstrap.Initialise(), new SamplingOptions(), _ => { });
        return ServiceComposition.Loop(provider) switch
        {
            Ok<ServiceLoop> loop => loop.Value.Run(),
            Fault fault => Refuse(provider.GetRequiredService<ServiceLog>(), fault),
        };
    }

    private static int Refuse(ServiceLog log, Fault fault)
    {
        log.Bug(fault.Message);
        return 1;
    }
}
