using System;
using System.Reflection;
using System.IO;
using static IntersectUtilities.UtilsCommon.Utils;

namespace AcadOverrules
{
    internal static class EventHandlers
    {
#if DEBUG
        internal static Assembly Debug_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            prdDbg($"Asked for assembly: {args.Name}!");

            var name = args.Name.Split(',')[0];
            name += ".dll";

            prdDbg($"First looking here: {assemblyFolder}!");
            string filePath = Path.Combine(assemblyFolder, name);
            if (File.Exists(filePath)) return Assembly.LoadFrom(filePath);

            assemblyFolder = Directory.GetParent(assemblyFolder).FullName;
            prdDbg($"Then looking here: {assemblyFolder}!");
            filePath = Path.Combine(assemblyFolder, name);
            if (File.Exists(filePath)) return Assembly.LoadFrom(filePath);

            prdDbg($"File not found: {filePath}!"); return null;
        }
#endif
    }
}