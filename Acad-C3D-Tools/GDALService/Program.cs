using System.Text;

using GDALService.Hosting;
using GDALService.Raster;

namespace GDALService;

internal static class Program
{
    private static int Main()
    {
        // Before Console.In is first touched: a basePath with æ/ø/å arrives as
        // UTF-8 from the client and must not be read in the OEM code page.
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        return new ServiceLoop(Console.In, Console.Out, Console.Error, GdalEdge.Initialise()).Run();
    }
}
