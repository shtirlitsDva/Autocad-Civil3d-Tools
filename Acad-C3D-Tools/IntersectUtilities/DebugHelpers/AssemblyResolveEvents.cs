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

            if (args.Name.Equals("QuikGraph"))
            {
                string filePath = Path.Combine(assemblyFolder, "QuikGraph.dll");
                return Assembly.LoadFrom(filePath);
            }
            else if (args.Name.Equals("QuikGraph.Graphviz"))
            {
                string filePath = Path.Combine(assemblyFolder, "QuikGraph.Graphviz.dll");
                return Assembly.LoadFrom(filePath);
            }
            else if (args.Name.Equals("NetTopologySuite"))
            {
                string filePath = Path.Combine(assemblyFolder, "NetTopologySuite.dll");
                return Assembly.LoadFrom(filePath);
            }
            else if (args.Name.Equals("Accord.MachineLearning"))
            {
                string filePath = Path.Combine(assemblyFolder, "Accord.MachineLearning.dll");
                return Assembly.LoadFrom(filePath);
            }

            return null;
        }
#endif
    }
}