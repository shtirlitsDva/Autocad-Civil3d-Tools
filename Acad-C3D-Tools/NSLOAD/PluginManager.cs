using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if BRICSCAD
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
#endif

using Exception = System.Exception;

namespace NSLOAD
{
    public static class PluginManager
    {
        private static readonly Dictionary<string, PluginRegistration> _plugins = new();

        /// <summary>
        /// A plugin has been through a load or an unload, so whatever follows
        /// from its loaded state - the command that loads it, a palette row -
        /// can be put right. Raised after the attempt whether or not it changed
        /// anything: a listener reads the state rather than being told what
        /// happened, so a refused unload leaves the command where it belongs.
        /// </summary>
        public static event Action<string>? PluginStateChanged;

        public static PluginRegistrationBuilder Register(string pluginName)
        {
            return new PluginRegistrationBuilder(pluginName);
        }

        public static void Load(string pluginName)
        {
            try
            {
                var reg = GetRegistration(pluginName);

                if (reg.Plugin.IsLoaded)
                {
                    Say($"{pluginName} is already loaded.");
                    return;
                }

                if (string.IsNullOrEmpty(reg.Path))
                {
                    Say($"{pluginName} has no path configured.");
                    return;
                }

                if (!File.Exists(reg.Path))
                {
                    Say($"{pluginName} not found: {reg.Path}");
                    return;
                }

                reg.Plugin.Load(Say);
            }
            catch (Exception ex)
            {
                ReportFailure($"{pluginName} load error", ex);
            }
            finally
            {
                Announce(pluginName);
            }
        }

        public static void Unload(string pluginName)
        {
            try
            {
                var reg = GetRegistration(pluginName);

                if (!reg.Plugin.IsLoaded)
                {
                    Say($"{pluginName} is not loaded.");
                    return;
                }

                reg.Plugin.Unload(Say);
            }
            catch (Exception ex)
            {
                ReportFailure($"{pluginName} unload error", ex);
            }
            finally
            {
                Announce(pluginName);
            }
        }

        // A listener that throws must not turn a good load into a failed one, so
        // its failure is reported where every other swallowed one is.
        private static void Announce(string pluginName)
        {
            try { PluginStateChanged?.Invoke(pluginName); }
            catch (Exception ex) { ReportFailure($"{pluginName} state change", ex); }
        }

        /// <summary>The host is shutting down: let every plugin do its exit work.</summary>
        public static void ShutdownAll()
        {
            foreach (var reg in _plugins.Values)
            {
                try { reg.Plugin.Shutdown(); }
                catch (Exception ex)
                {
                    // Keep going: one plugin failing its exit work must not stop
                    // the others from doing theirs.
                    NsLoadDiagnostics.Report($"{reg.PluginName} shutdown", ex);
                }
            }
        }

        public static IReadOnlyList<string> GetRegisteredPluginNames()
            => _plugins.Keys.ToList();

        public static bool IsRegistered(string pluginName)
            => _plugins.ContainsKey(pluginName);

        public static bool IsLoaded(string pluginName)
            => _plugins.TryGetValue(pluginName, out var reg) && reg.Plugin.IsLoaded;

        /// <summary>The plugin's version line for the manager palette, or null
        /// when it has none (managed plugins, or an unregistered name).</summary>
        public static string? GetVersionStatus(string pluginName)
            => _plugins.TryGetValue(pluginName, out var reg) ? reg.Plugin.VersionStatus : null;

        /// <summary>Unloads the plugin if needed and forgets it. Returns false,
        /// keeping the registration, when the plugin is still loaded afterwards
        /// (the unload was refused and has been reported).</summary>
        public static bool Unregister(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                return true;

            if (reg.Plugin.IsLoaded)
                Unload(pluginName);
            if (reg.Plugin.IsLoaded)
                return false;

            _plugins.Remove(pluginName);
            return true;
        }

        // A refusal is written for the drafter and shown as it is; anything else
        // is unexpected and keeps its full detail for whoever has to fix it.
        private static void ReportFailure(string what, Exception ex)
        {
            if (ex is PluginRefusedException)
            {
                string cause = ex.InnerException != null ? $" ({ex.InnerException.Message})" : "";
                Say($"{what}: {ex.Message}{cause}");
                return;
            }
            Say($"{what}: {ex.Message}");
            Say(ex.ToString());
        }

        private static PluginRegistration GetRegistration(string pluginName)
        {
            if (!_plugins.TryGetValue(pluginName, out var reg))
                throw new InvalidOperationException(
                    $"Plugin '{pluginName}' is not registered.");
            return reg;
        }

        // One line on the command line of the drawing active NOW, never an editor
        // captured earlier: on BricsCAD a native group's unload closes the drawing
        // it started in, and its editor then throws eInvalidDrawing.
        private static void Say(string line)
        {
            Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n{line}");
        }

        internal static void AddRegistration(PluginRegistration reg)
        {
            _plugins[reg.PluginName] = reg;
        }
    }

    internal class PluginRegistration
    {
        public required string PluginName { get; init; }

        /// <summary>A plugin DLL, or a native group's manifest.</summary>
        public required string Path { get; init; }

        public required string[] SharedAssemblyNames { get; init; }
        public required ILoadablePlugin Plugin { get; init; }
    }

    public class PluginRegistrationBuilder
    {
        private readonly string _pluginName;
        private string? _path;
        private string[] _sharedAssemblyNames = Array.Empty<string>();
        private bool _useCommands;

        internal PluginRegistrationBuilder(string pluginName)
        {
            _pluginName = pluginName;
        }

        /// <summary>A plugin DLL, or a native group's <c>*.oarx.json</c>.</summary>
        public PluginRegistrationBuilder WithPath(string path)
        {
            _path = path;
            return this;
        }

        public PluginRegistrationBuilder WithCommands()
        {
            _useCommands = true;
            return this;
        }

        public PluginRegistrationBuilder WithSharedAssemblies(params string[] assemblyNames)
        {
            _sharedAssemblyNames = assemblyNames;
            return this;
        }

        public void Commit()
        {
            string path = _path ?? "";
            var reg = new PluginRegistration
            {
                PluginName = _pluginName,
                Path = path,
                SharedAssemblyNames = _sharedAssemblyNames,
                Plugin = PluginKinds.Create(_pluginName, path, _useCommands),
            };

            PluginManager.AddRegistration(reg);
        }
    }
}
