using System;
using System.Collections.Generic;
using System.Linq;

namespace NSLOAD.Native
{
    /// <summary>
    /// A set of module files taken together: either one whole release, every file
    /// carrying the same version, or a mixed set (a file missing, or versions that
    /// disagree). Used for what is on disk and for what is in memory alike.
    /// </summary>
    internal abstract record Release
    {
        /// <summary>A whole release when every version is present and equal;
        /// otherwise mixed. A null version is a missing file.</summary>
        public static Release Of(IEnumerable<string?> versions)
        {
            var distinct = versions.Distinct().ToList();
            return distinct.Count == 1 && distinct[0] is string version
                ? new WholeRelease(version)
                : new MixedRelease();
        }

        /// <summary>How the drafter reads it in a sentence.</summary>
        public abstract string Describe();
    }

    internal sealed record WholeRelease(string Version) : Release
    {
        public override string Describe() => $"v{Version}";
    }

    internal sealed record MixedRelease : Release
    {
        public override string Describe() => "a mixed pair";
    }

    /// <summary>
    /// What a native group has in AutoCAD right now. Each state answers for
    /// itself - its palette row, what it tracks, what an unload leaves - so the
    /// group never branches on which state it is in.
    /// </summary>
    internal abstract class GroupState
    {
        public static readonly GroupState NotLoaded = new NotLoadedState();

        /// <summary>The state for modules this group loaded and still answers for.</summary>
        public static GroupState Loaded(NativeGroupManifest manifest, Release inMemory) =>
            inMemory is WholeRelease whole
                ? new LoadedState(manifest, whole.Version)
                : new LoadedMixedState(manifest);

        /// <summary>The state for images released by the linker that stayed in
        /// memory, holding <paramref name="inMemory"/>.</summary>
        public static GroupState Held(Release inMemory) => new HeldState(inMemory);

        /// <summary>The modules this group loaded and still answers for; empty
        /// when it answers for none.</summary>
        public virtual IReadOnlyList<string> TrackedModules => Array.Empty<string>();

        /// <summary>The palette row, given the release on disk.</summary>
        public abstract string Row(Release onDisk);

        /// <summary>The state once the tracked modules have been released by the
        /// linker; <paramref name="imageStayed"/> when one never left memory.</summary>
        public virtual GroupState AfterRelease(bool imageStayed) => NotLoaded;

        /// <summary>What an image found in memory holds. Only a group that put it
        /// there and saw it stay knows; otherwise the mapped files are all there
        /// is to go on.</summary>
        public virtual Release InMemory(Func<Release> fromMappedFiles) => fromMappedFiles();

        private sealed class NotLoadedState : GroupState
        {
            public override string Row(Release onDisk) =>
                onDisk is WholeRelease disk ? $"on disk v{disk.Version}" : "OneDrive syncing…";
        }

        private sealed class LoadedState : GroupState
        {
            private readonly NativeGroupManifest _manifest;
            private readonly string _version;

            public LoadedState(NativeGroupManifest manifest, string version)
            {
                _manifest = manifest;
                _version = version;
            }

            public override IReadOnlyList<string> TrackedModules => _manifest.Modules;

            public override string Row(Release onDisk) => onDisk switch
            {
                WholeRelease disk when disk.Version == _version => $"v{_version}",
                WholeRelease disk => $"v{_version} — v{disk.Version} ready: Unload, then Load",
                _ => $"v{_version} · OneDrive syncing…",
            };

            public override GroupState AfterRelease(bool imageStayed) =>
                imageStayed ? new HeldState(new WholeRelease(_version)) : NotLoaded;
        }

        // Only after a failed load whose rollback could not unload a mixed pair:
        // nothing sensible runs until it is unloaded.
        private sealed class LoadedMixedState : GroupState
        {
            private readonly NativeGroupManifest _manifest;

            public LoadedMixedState(NativeGroupManifest manifest) => _manifest = manifest;

            public override IReadOnlyList<string> TrackedModules => _manifest.Modules;

            public override string Row(Release onDisk) => "Mixed versions in memory — Unload, then Load";

            public override GroupState AfterRelease(bool imageStayed) =>
                imageStayed ? new HeldState(new MixedRelease()) : NotLoaded;
        }

        // Released by the linker, but an image stayed in memory: its file stays
        // locked until Civil restarts.
        private sealed class HeldState : GroupState
        {
            private readonly Release _inMemory;

            public HeldState(Release inMemory) => _inMemory = inMemory;

            public override string Row(Release onDisk) => "Held in memory — restart Civil to update";

            public override Release InMemory(Func<Release> fromMappedFiles) => _inMemory;
        }
    }
}
