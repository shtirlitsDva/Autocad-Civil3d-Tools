using Autodesk.AutoCAD.Runtime;

namespace NSLOAD.Native
{
    /// <summary>Loads a native group's managed companion the way NETLOAD does.
    /// The BricsCAD build has its own copy of this file.</summary>
    internal static class CompanionLoader
    {
        /// <summary>Into the default load context, through AutoCAD's extension
        /// loader, so IExtensionApplication.Initialize runs and [CommandMethod]s
        /// register. Throws when the assembly cannot load.</summary>
        public static void Load(string fullPath) => ExtensionLoader.Load(fullPath);
    }
}
