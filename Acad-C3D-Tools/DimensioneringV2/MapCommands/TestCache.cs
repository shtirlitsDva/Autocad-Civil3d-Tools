using DimensioneringV2.UI;

using DimensioneringV2.UI.CacheTest;
using System.Threading.Tasks;

namespace DimensioneringV2.MapCommands
{
    /// <summary>
    /// Opens the cache performance test window.
    /// </summary>
    internal class TestCache
    {
        internal Task Execute()
        {
            var vm = new CacheTestViewModel();
            var window = new CacheTestWindow(vm);
            window.Show();
            return Task.CompletedTask;
        }
    }
}
