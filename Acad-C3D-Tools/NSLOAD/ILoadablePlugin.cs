using System;

namespace NSLOAD
{
    /// <summary>
    /// One kind of thing NSLOAD can load and unload. <see cref="PluginManager"/>
    /// only talks to this surface; which kind a register entry is gets decided
    /// once, in <see cref="PluginKinds.Create"/>.
    /// </summary>
    internal interface ILoadablePlugin
    {
        bool IsLoaded { get; }

        /// <summary>Loads the plugin and reports the outcome through
        /// <paramref name="say"/>. Throws when it cannot load.</summary>
        void Load(Action<string> say);

        /// <summary>Unloads the plugin and reports the outcome through
        /// <paramref name="say"/>. Throws when it cannot unload.</summary>
        void Unload(Action<string> say);

        /// <summary>Called when AutoCAD is shutting down.</summary>
        void Shutdown();

        /// <summary>A one-line version report for the manager palette, or null
        /// when this kind of plugin has none to show.</summary>
        string? VersionStatus { get; }
    }

    /// <summary>
    /// The one place that decides what kind of plugin a register path names:
    /// a native group manifest (<c>*.oarx.json</c>) or a managed plugin DLL.
    /// </summary>
    internal static class PluginKinds
    {
        public static ILoadablePlugin Create(
            string pluginName, string path, CommandRegistrar? registrar)
        {
            return Native.NativeGroupManifest.IsManifestPath(path)
                ? new Native.NativeGroupPlugin(pluginName, path)
                : new ManagedPlugin(pluginName, path, registrar);
        }
    }
}
