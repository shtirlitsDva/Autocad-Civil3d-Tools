using System;
using System.Reflection;
using System.IO;
using static IntersectUtilities.UtilsCommon.Utils;

namespace DimensioneringV2
{
    public static class MissingAssemblyLoaderDimV2
    {
#if DEBUG
        public static Assembly Debug_AssemblyResolveV2(object sender, ResolveEventArgs args)
        {
            string assemblyFolder = @"X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\DimensioneringV2\bin\Debug";
            prdDbg($"Asked for assembly: {args.Name}!");

            var name = args.Name.Split(',')[0];
            name += ".dll";
            string filePath = Path.Combine(assemblyFolder, name);
            if (File.Exists(filePath)) return Assembly.LoadFrom(filePath);
            else { prdDbg($"File not found: {filePath}!"); return null; }
        }
#endif
#if RELEASE
        public static Assembly Debug_AssemblyResolveV2(object sender, ResolveEventArgs args)
        {
            string assemblyFolder = @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\2025\DimensioneringV2";
            
            var name = args.Name.Split(',')[0];
            name += ".dll";
            string filePath = Path.Combine(assemblyFolder, name);
            if (File.Exists(filePath))
            {
                prdDbg($"Found requested assembly: {args.Name}!");
                return Assembly.LoadFrom(filePath);
            }
            else { return null; }
        }
#endif
    }
}