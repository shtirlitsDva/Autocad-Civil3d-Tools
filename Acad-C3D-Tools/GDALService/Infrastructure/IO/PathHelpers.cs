using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Infrastructure.IO
{
    internal static class PathHelpers
    {
        public static string ElevationsDir(string basePath) => Path.Combine(Path.GetFullPath(basePath), "Elevations");
        public static string VrtPath(string elevationsDir, string projectId) => Path.Combine(elevationsDir, $"{projectId}.vrt");
        public static string[] FindTiles(string elevationsDir, string projectId) =>
            Directory.EnumerateFiles(elevationsDir, $"{projectId}_*.tif", SearchOption.TopDirectoryOnly)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                     .ToArray();
    }
}
