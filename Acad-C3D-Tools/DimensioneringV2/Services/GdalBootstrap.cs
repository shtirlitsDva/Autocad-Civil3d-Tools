using OSGeo.GDAL;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal static class GdalBootstrap
    {
        private static bool _init;

        // <-- put your flat folder path here
        public static string RuntimeDir { get; set; } = @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Dependencies\Gdal\";

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void Init()
        {
            if (_init) return;

            Gdal.AllRegister();

            //if (!Directory.Exists(RuntimeDir))
            //    throw new DirectoryNotFoundException($"GDAL flat folder not found: {RuntimeDir}");

            //// 1) Make the Windows DLL loader look in your flat folder first
            //SetDllDirectory(RuntimeDir);
            //var oldPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            //if (!oldPath.Contains(RuntimeDir, StringComparison.OrdinalIgnoreCase))
            //    Environment.SetEnvironmentVariable("PATH", RuntimeDir + ";" + oldPath);

            //// 2) Point GDAL and PROJ to the SAME flat folder (data + proj.db are there)
            //Environment.SetEnvironmentVariable("GDAL_DATA", RuntimeDir);
            //Environment.SetEnvironmentVariable("PROJ_LIB", RuntimeDir);

            //// 3) Let MaxRev wire up import resolvers and config (reads env vars above)
            //Gdal.ConfigureAll();

            //// 4) (Optional) sanity check
            //string ver = Gdal.VersionInfo("RELEASE_NAME"); // e.g. "3.11.0"
            //Utils.prtDbg(ver);
            //if (string.IsNullOrEmpty(ver))
            //    throw new InvalidOperationException("GDAL failed to initialize.");

            _init = true;
        }
    }
}
