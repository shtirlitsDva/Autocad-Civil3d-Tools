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

            if (args.Name.Contains("QuikGraph"))
            {
                string filePath = Path.Combine(assemblyFolder, "QuikGraph.dll");
                return Assembly.LoadFrom(filePath);
            }
            else if (args.Name.Contains("QuikGraph.Graphviz"))
            {
                string filePath = Path.Combine(assemblyFolder, "QuikGraph.Graphviz.dll");
                return Assembly.LoadFrom(filePath);
            }
            else if (args.Name.Contains("NetTopologySuite"))
            {
                string filePath = Path.Combine(assemblyFolder, "NetTopologySuite.dll");
                return Assembly.LoadFrom(filePath);
            }

            return null;
        }
#endif
    }
}