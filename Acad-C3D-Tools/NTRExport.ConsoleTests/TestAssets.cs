using System;
using System.IO;

namespace NTRExport.ConsoleTests
{
    internal static class TestAssets
    {
        public static string Resolve(string assetName)
        {
            var root = FindSolutionRoot(AppContext.BaseDirectory);
            return Path.Combine(root, "Acad-C3D-Tools", "NTRExport.ConsoleTests", "Assets", assetName);
        }

        private static string FindSolutionRoot(string startDir)
        {
            var dir = startDir;
            while (!string.IsNullOrEmpty(dir))
            {
                var acd = Path.Combine(dir, "Acad-C3D-Tools");
                if (Directory.Exists(acd)) return dir;
                dir = Path.GetDirectoryName(dir)!;
            }
            return startDir;
        }
    }
}

