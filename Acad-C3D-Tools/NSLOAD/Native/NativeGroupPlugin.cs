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
    /// Companions (the managed trace UI, pinned DLLs) are load-only: a change to
    /// them reaches the drafter at the next Civil start, not at a reload.
    /// Design authority: NorsynDrawingTools
    /// <c>docs/shared-understanding/module-loading.md</c>, ndh-pipeline-user-delivery.
    /// </remarks>
    internal sealed class NativeGroupPlugin : ILoadablePlugin
    {
        private readonly string _name;
        private readonly string _manifestPath;

        // The manifest as it was when this group last put modules into AutoCAD.
        // Unload walks THESE modules, not a manifest OneDrive may have changed since.
        private NativeGroupManifest? _loaded;
        private string? _loadedVersion;

        // The version whose images stayed in memory after an unload released
        // them; null when the last unload left nothing behind. Those files stay
        // locked until Civil restarts.
        private string? _heldVersion;

        // The manifest as last read for the palette, re-read only when the file
        // changes: the palette asks every few seconds, on AutoCAD's main thread.
        private (DateTime WriteTime, NativeGroupManifest Manifest)? _manifestCache;

        // FileVersionInfo on a cloud-only file makes OneDrive download it, and the
        // palette asks every few seconds; read each file again only when it changes.
        private readonly Dictionary<string, (DateTime WriteTime, string Version)> _versionCache =
            new(StringComparer.OrdinalIgnoreCase);

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

            foreach (string module in manifest.Modules)
            {
                string fileName = Path.GetFileName(module);
                if (OarxModuleHost.IsLoaded(fileName))
                    throw new OarxModuleException(
                        $"{fileName} is already loaded by another loader (for example the " +
                        "other NDH release channel, or a registry demand-load). Unload it " +
                        $"there first, then load {_name}.");
            }

            // Before the pair check: a module held in memory keeps its file locked,
            // so OneDrive may have updated only the others, and the pair on disk
            // then describes neither what is in memory nor a whole release.
            RequireHeldImagesReusable(manifest, say);

            string version = RequireConsistentVersion(manifest);

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

                // The files are locked now and cannot change under us. If OneDrive
                // swapped one between the check above and its load, the pair in
                // memory is mixed and the arx would switch itself off: undo it.
                string loadedVersion = RequireConsistentVersion(manifest);
                if (loadedVersion != version)
                    throw new OarxModuleException(
                        $"OneDrive updated {_name} to v{loadedVersion} while v{version} was " +
                        "loading. Load it again.");
            }
            catch (Exception)
            {
                // Leave nothing half-loaded: an arx without its dbx (or the
                // reverse) is worse than no group at all.
                var leftBehind = new List<string>();
                foreach (string fileName in Enumerable.Reverse(loadedSoFar))
                {
                    try { OarxModuleHost.Unload(fileName); }
                    catch (Exception undoEx)
                    {
                        leftBehind.Add(fileName);
                        say($"{_name}: WARNING - could not unload {fileName} after the " +
                            $"failed load: {undoEx.Message}");
                    }
                }
                if (leftBehind.Count > 0)
                {
                    // Still ours: keep them tracked so IsLoaded tells the truth and
                    // Unload can be retried from the manager. Their files are
                    // locked, so the disk says what is in memory; null when those
                    // disagree (the row then shows a mixed pair).
                    _loaded = manifest;
                    _loadedVersion = SingleVersion(manifest.Modules
                        .Where(m => leftBehind.Contains(Path.GetFileName(m), StringComparer.OrdinalIgnoreCase)));
                    say($"{_name}: {string.Join(", ", leftBehind)} is still loaded by this " +
                        "group. Unload it from NSLOADMGR before trying again.");
                }
                else
                {
                    // Unloaded, but an image may have stayed behind, exactly as
                    // after an Unload; its locked file names the version in memory.
                    var held = StillMapped(loadedSoFar);
                    if (held.Count > 0)
                        _heldVersion = ReadVersion(OarxModuleHost.MappedPathInThisProcess(held[0])!);
                    WarnIfHeld(held, say);
                }
                throw;
            }

            _loaded = manifest;
            _loadedVersion = version;
            _heldVersion = null;
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

            var stillMapped = StillMapped(_loaded.Modules.Select(m => Path.GetFileName(m)));

            _heldVersion = stillMapped.Count > 0 ? _loadedVersion : null;
            _loaded = null;
            _loadedVersion = null;

            if (!WarnIfHeld(stillMapped, say))
                say($"{_name} unloaded. Load it again once NSLOADMGR shows the new version.");
        }

        // The modules, of those named, whose images are still in this process
        // although the linker has released them.
        private static List<string> StillMapped(IEnumerable<string> fileNames)
            => fileNames.Where(f => OarxModuleHost.MappedPathInThisProcess(f) != null).ToList();

        // Tells the drafter a released module never left memory. True when it did.
        private bool WarnIfHeld(List<string> stillMapped, Action<string> say)
        {
            if (stillMapped.Count == 0) return false;
            say($"{_name}: WARNING - AutoCAD released {string.Join(", ", stillMapped)} " +
                "but it is still held in memory, so its file stays locked and OneDrive " +
                "cannot update it. Restart Civil to get the new version.");
            return true;
        }

        // The one version these module files carry, or null when they disagree
        // or one is missing.
        private static string? SingleVersion(IEnumerable<string> paths)
        {
            var versions = paths.Select(ReadVersion).Distinct().ToList();
            return versions.Count == 1 ? versions[0] : null;
        }

        // AutoCAD tears native modules down itself at exit. Unloading them here
        // would turn every entity in the still-open drawings into a proxy for no
        // benefit, so shutdown deliberately does nothing.
        public void Shutdown() { }

        public string? VersionStatus
        {
            get
            {
                List<string?> onDisk;
                try
                {
                    var modules = _loaded?.Modules ?? ReadManifestCached().Modules;
                    onDisk = modules.Select(ReadVersionCached).ToList();
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

                // Only after a failed load whose rollback could not unload a
                // mixed pair: nothing sensible runs until it is unloaded.
                if (IsLoaded && _loadedVersion == null)
                    return "Mixed versions in memory — Unload, then Load";

                if (IsLoaded)
                {
                    if (disk == null)
                        return $"v{_loadedVersion} · OneDrive syncing…";
                    return disk == _loadedVersion
                        ? $"v{_loadedVersion}"
                        : $"v{_loadedVersion} — v{disk} ready: Unload, then Load";
                }

                if (_heldVersion != null)
                    return "Held in memory — restart Civil to update";
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

            bool missing = versions.Any(v => v.Version == null);
            if (missing || versions.Select(v => v.Version).Distinct().Count() > 1)
                throw new OarxModuleException(
                    $"OneDrive is still syncing {_name}: " +
                    string.Join(", ", versions.Select(v =>
                        $"{v.File} {(v.Version == null ? "missing" : "v" + v.Version)}")) +
                    ". Try again in a moment.");

            return versions[0].Version!;
        }

        /// <summary>
        /// Lets a load go ahead over modules whose images never left memory only
        /// when that is harmless: they are this group's own files, and the disk
        /// still carries exactly the version in memory. Warns that the image is
        /// reused. Throws, telling the drafter to restart, in every other case:
        /// loading beside another folder's image, or loading a release OneDrive
        /// has (partly) delivered beside an old image, would run a mixed or stale
        /// pair while claiming the new one.
        /// </summary>
        private void RequireHeldImagesReusable(NativeGroupManifest manifest, Action<string> say)
        {
            var held = manifest.Modules
                .Select(m => (Module: m, Mapped: OarxModuleHost.MappedPathInThisProcess(Path.GetFileName(m))))
                .Where(h => h.Mapped != null)
                .ToList();
            if (held.Count == 0) return;

            foreach (var (module, mapped) in held)
            {
                if (!FileIdentity.Same(mapped!, module))
                    throw new OarxModuleException(
                        $"{Path.GetFileName(module)} from {Path.GetDirectoryName(mapped)} is still " +
                        $"held in memory, so {_name} cannot load its own copy beside it. " +
                        $"Restart Civil, then load {_name}.");
            }

            // What is in memory is what this group loaded before it was unloaded.
            // An image this group never loaded (another loader's, earlier) is taken
            // at its file's word, which is all there is to go on.
            string? inMemory = _heldVersion ?? ReadVersion(held[0].Mapped!);
            var onDisk = manifest.Modules
                .Select(m => (File: Path.GetFileName(m), Version: ReadVersion(m)))
                .ToList();
            if (onDisk.Any(d => d.Version != inMemory))
                throw new OarxModuleException(
                    $"{string.Join(", ", held.Select(h => Path.GetFileName(h.Module)))} " +
                    $"never left memory after the last unload, so v{inMemory} is still running, " +
                    "and OneDrive has delivered " +
                    string.Join(", ", onDisk.Select(d =>
                        $"{d.File} {(d.Version == null ? "missing" : "v" + d.Version)}")) +
                    $". Restart Civil to load the new version of {_name}.");

            say($"{_name}: WARNING - {string.Join(", ", held.Select(h => Path.GetFileName(h.Module)))} " +
                $"never left memory after the last unload, so v{inMemory} is used again. " +
                "Restart Civil to get a new version.");
        }

        private NativeGroupManifest ReadManifestCached()
        {
            DateTime written = File.GetLastWriteTimeUtc(_manifestPath);
            if (_manifestCache is { } hit && hit.WriteTime == written)
                return hit.Manifest;

            var manifest = NativeGroupManifest.Read(_manifestPath);
            _manifestCache = (written, manifest);
            return manifest;
        }

        private string? ReadVersionCached(string path)
        {
            if (!File.Exists(path)) return null;
            DateTime written = File.GetLastWriteTimeUtc(path);
            if (_versionCache.TryGetValue(path, out var hit) && hit.WriteTime == written)
                return hit.Version;

            string? version = ReadVersion(path);
            if (version != null) _versionCache[path] = (written, version);
            return version;
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
