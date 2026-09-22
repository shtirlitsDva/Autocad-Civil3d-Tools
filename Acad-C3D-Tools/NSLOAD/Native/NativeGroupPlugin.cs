using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Exception = System.Exception;

namespace NSLOAD.Native
{
    /// <summary>
    /// A native group: ObjectARX modules (dbx/arx) plus companions, loaded
    /// straight from the OneDrive share as its <c>*.oarx.json</c> manifest
    /// describes.
    /// </summary>
    /// <remarks>
    /// The share IS the store. While a module is loaded its file is locked, and
    /// OneDrive holds a newer version back until the lock goes; so a drafter
    /// updates by unloading, waiting a moment, and loading again. Everything here
    /// serves that cycle:
    /// <list type="bullet">
    /// <item>a load is refused while the modules on disk carry different versions
    /// (OneDrive is mid-sync), since a mismatched pair only disables itself;</item>
    /// <item>an unload proves the image really left the process, because an image
    /// that stays mapped keeps its file locked and OneDrive can never swap it;</item>
    /// <item>the loaded and on-disk versions are reported, so the drafter can see
    /// when OneDrive has finished.</item>
    /// </list>
    /// Design authority: NorsynDrawingTools
    /// <c>docs/shared-understanding/module-loading.md</c>, ndh-pipeline-user-delivery.
    /// </remarks>
    internal sealed class NativeGroupPlugin : ILoadablePlugin
    {
        private readonly string _name;
        private readonly string _manifestPath;

        // The manifest as it was at the last successful load. Unload walks THESE
        // modules, not a manifest that OneDrive may have changed since.
        private NativeGroupManifest? _loaded;
        private string? _loadedVersion;

        public NativeGroupPlugin(string name, string manifestPath)
        {
            _name = name;
            _manifestPath = manifestPath;
        }

        public bool IsLoaded =>
            _loaded != null &&
            _loaded.Modules.Any(m => OarxModuleHost.IsLoaded(Path.GetFileName(m)));

        public void Load(Action<string> say)
        {
            var manifest = NativeGroupManifest.Read(_manifestPath);
            string version = RequireConsistentVersion(manifest);

            foreach (string module in manifest.Modules)
            {
                string fileName = Path.GetFileName(module);
                if (OarxModuleHost.IsLoaded(fileName))
                    throw new OarxModuleException(
                        $"{fileName} is already loaded by another loader (for example the " +
                        "other NDH release channel, or a registry demand-load). Unload it " +
                        $"there first, then load {_name}.");
            }

            var stillMapped = manifest.Modules
                .Select(Path.GetFileName)
                .Where(f => OarxModuleHost.IsMappedInThisProcess(f!))
                .ToList();
            if (stillMapped.Count > 0)
                say($"{_name}: WARNING - {string.Join(", ", stillMapped)} never left memory " +
                    "after the last unload, so the version already in memory is used again. " +
                    "Restart Civil to get the new version.");

            foreach (string pin in manifest.PreloadNative)
                OarxCompanionHost.PinNative(pin, say);
            foreach (string asm in manifest.PreloadManaged)
                OarxCompanionHost.LoadManaged(asm, say);

            var loadedSoFar = new List<string>();
            try
            {
                foreach (string module in manifest.Modules)
                {
                    OarxModuleHost.Load(module);
                    loadedSoFar.Add(Path.GetFileName(module));
                }
            }
            catch (Exception)
            {
                // Leave nothing half-loaded: an arx without its dbx (or the
                // reverse) is worse than no group at all.
                foreach (string fileName in Enumerable.Reverse(loadedSoFar))
                {
                    try { OarxModuleHost.Unload(fileName); }
                    catch (Exception undoEx)
                    {
                        say($"{_name}: WARNING - could not unload {fileName} after the " +
                            $"failed load: {undoEx.Message}");
                    }
                }
                throw;
            }

            _loaded = manifest;
            _loadedVersion = version;
            say($"{_name} loaded (v{version}).");
        }

        public void Unload(Action<string> say)
        {
            if (_loaded == null) return;

            // Reverse of load order: the arx that uses the dbx's classes goes first.
            // A refusal stops here and leaves the group loaded, so Unload can be
            // retried; the modules already gone are skipped next time.
            foreach (string module in _loaded.Modules.Reverse())
                OarxModuleHost.Unload(Path.GetFileName(module));

            var stillMapped = _loaded.Modules
                .Select(Path.GetFileName)
                .Where(f => OarxModuleHost.IsMappedInThisProcess(f!))
                .ToList();

            _loaded = null;
            _loadedVersion = null;

            if (stillMapped.Count > 0)
                say($"{_name}: WARNING - AutoCAD released {string.Join(", ", stillMapped)} " +
                    "but it is still held in memory, so its file stays locked and OneDrive " +
                    "cannot update it. Restart Civil to get the new version.");
            else
                say($"{_name} unloaded. Load it again once NSLOADMGR shows the new version.");
        }

        // AutoCAD tears native modules down itself at exit. Unloading them here
        // would turn every entity in the still-open drawings into a proxy for no
        // benefit, so shutdown deliberately does nothing.
        public void Shutdown() { }

        public string? VersionStatus
        {
            get
            {
                IReadOnlyList<string> modules;
                List<string?> onDisk;
                try
                {
                    modules = _loaded?.Modules ?? NativeGroupManifest.Read(_manifestPath).Modules;
                    onDisk = modules.Select(ReadVersion).ToList();
                }
                catch (Exception ex)
                {
                    // Shown on the row itself, where the drafter is looking; this
                    // runs on a timer, so the command line would be flooded.
                    return ex.Message;
                }

                string? disk = onDisk.Any(v => v == null) || onDisk.Distinct().Count() > 1
                    ? null
                    : onDisk[0];

                if (IsLoaded && _loadedVersion != null)
                {
                    if (disk == null || disk == _loadedVersion)
                        return disk == null
                            ? $"v{_loadedVersion} · OneDrive syncing…"
                            : $"v{_loadedVersion}";
                    return $"v{_loadedVersion} — v{disk} ready: Unload, then Load";
                }

                return disk == null ? "OneDrive syncing…" : $"on disk v{disk}";
            }
        }

        /// <summary>
        /// The one version every module on disk carries. Throws when a module is
        /// missing or the modules disagree — OneDrive is still writing the release.
        /// </summary>
        private string RequireConsistentVersion(NativeGroupManifest manifest)
        {
            var versions = manifest.Modules
                .Select(m => (File: Path.GetFileName(m), Version: ReadVersion(m)))
                .ToList();

            var missing = versions.Where(v => v.Version == null).Select(v => v.File).ToList();
            if (missing.Count > 0)
                throw new OarxModuleException(
                    $"{_name} cannot load: {string.Join(", ", missing)} not found next to " +
                    $"{Path.GetFileName(_manifestPath)}. OneDrive may still be syncing; " +
                    "try again in a moment.");

            if (versions.Select(v => v.Version).Distinct().Count() > 1)
                throw new OarxModuleException(
                    $"OneDrive is still syncing {_name}: " +
                    string.Join(", ", versions.Select(v => $"{v.File} v{v.Version}")) +
                    ". Try again in a moment.");

            return versions[0].Version!;
        }

        /// <summary>The module's FileVersion as four numbers, or null when the
        /// file is not there. Any other failure to read it throws.</summary>
        private static string? ReadVersion(string path)
        {
            if (!File.Exists(path)) return null;
            var info = FileVersionInfo.GetVersionInfo(path);
            return $"{info.FileMajorPart}.{info.FileMinorPart}." +
                   $"{info.FileBuildPart}.{info.FilePrivatePart}";
        }
    }
}
