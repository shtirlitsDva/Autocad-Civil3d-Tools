using System;
using System.Reflection;
using System.IO;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities
{
    public partial class Intersect
    {
#if DEBUG
        private static Assembly Debug_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string assemblyFolder = @"X:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools\Acad-C3D-Tools\IntersectUtilities\bin\Debug";
            prdDbg($"Asked for assembly: {args.Name}!");

            var name = args.Name.Split(',')[0];
            name += ".dll";
            string filePath = Path.Combine(assemblyFolder, name);
            if (File.Exists(filePath)) return Assembly.LoadFrom(filePath);
            else { prdDbg($"File not found: {filePath}!"); return null; }

            //switch (name)
            //{
            //    case "QuikGraph":
            //        {
            //            string filePath = Path.Combine(assemblyFolder, "QuikGraph.dll");
            //            return Assembly.LoadFrom(filePath);
            //        }
            //    case "QuikGraph.Graphviz":
            //        {
            //            string filePath = Path.Combine(assemblyFolder, "QuikGraph.Graphviz.dll");
            //            return Assembly.LoadFrom(filePath);
            //        }
            //    case "NetTopologySuite":
            //        {
            //            string filePath = Path.Combine(assemblyFolder, "NetTopologySuite.dll");
            //            return Assembly.LoadFrom(filePath);
            //        }
            //    case "Accord.MachineLearning":
            //        {
            //            string filePath = Path.Combine(assemblyFolder, "Accord.MachineLearning.dll");
            //            return Assembly.LoadFrom(filePath);
            //        }
            //    default:
            //        break;
            //}
        }
#endif
    }
}