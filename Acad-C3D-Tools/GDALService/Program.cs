using System.Text;

using GDALService.Configuration;
using GDALService.Hosting;
using System.Threading.Tasks;

namespace GDALService
{
    internal static class Program
    {
        static async Task<int> Main()
        {
            try
            {
                var options = new ServiceOptions();

                GdalConfiguration.ConfigureGdal();

                var loop = new ServiceLoop(options);
                return await loop.RunAsync();
            }
            catch (Exception ex)
            {

                throw;
            }            
        }
    }
}
