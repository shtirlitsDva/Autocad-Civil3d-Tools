using System.Reflection;

namespace NSLOAD.Native
{
    /// <summary>Loads a native group's managed companion the way NETLOAD does.
    /// The AutoCAD build has its own copy of this file.</summary>
    internal static class CompanionLoader
    {
        /// <summary>
        /// Into the default load context. BricsCAD has no public managed loader
        /// (AutoCAD's ExtensionLoader.Load): its scan, subscribed to
        /// AppDomain.AssemblyLoad, initializes the IExtensionApplication and
        /// registers the [CommandMethod]s of any assembly that references BrxMgd
        /// as it loads, so a plain LoadFrom is NETLOAD.
        /// </summary>
        /// <remarks>
        /// Not BricsCAD's own NETLOAD route (the native export LoadManagedDll):
        /// that loads an assembly with a <c>runtimeconfig.json</c> beside it into
        /// a load context of its own, as every C++/CLI interop has. A companion
        /// there would be a second copy of its types beside the default-context
        /// one the other companions bind to. Throws when the assembly cannot load.
        /// </remarks>
        public static void Load(string fullPath) => Assembly.LoadFrom(fullPath);
    }
}
