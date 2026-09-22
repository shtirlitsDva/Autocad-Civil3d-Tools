using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSLOAD.Native
{
    /// <summary>
    /// The <c>*.oarx.json</c> file that sits beside a native group's modules on
    /// the share and says what to load, in what order. Relative entries resolve
    /// against the manifest's own folder; absolute entries are used as written
    /// (a pin of a canonical DLL that lives elsewhere, such as Appload).
    /// </summary>
    internal sealed class NativeGroupManifest
    {
        public const string FileSuffix = ".oarx.json";

        private static readonly string[] ModuleExtensions = { ".dbx", ".arx", ".crx" };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // A misspelt field would otherwise be silently ignored and the group
            // would load without it. Refuse instead.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        /// <summary>Native modules, full paths, in LOAD order.</summary>
        public IReadOnlyList<string> Modules { get; }

        /// <summary>Native DLLs to pin by full path before the modules load.</summary>
        public IReadOnlyList<string> PreloadNative { get; }

        /// <summary>Managed assemblies to load before the modules load.</summary>
        public IReadOnlyList<string> PreloadManaged { get; }

        private NativeGroupManifest(
            IReadOnlyList<string> modules,
            IReadOnlyList<string> preloadNative,
            IReadOnlyList<string> preloadManaged)
        {
            Modules = modules;
            PreloadNative = preloadNative;
            PreloadManaged = preloadManaged;
        }

        public static bool IsManifestPath(string path)
            => path.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Reads and validates the manifest. Throws <see cref="OarxModuleException"/>
        /// with a sentence naming the problem when it is missing or malformed.
        /// </summary>
        public static NativeGroupManifest Read(string manifestPath)
        {
            ManifestFile? file;
            try
            {
                // Read-only, and we must never be the reason OneDrive (or anyone
                // else) cannot write the file while we have it open.
                using var stream = new FileStream(
                    manifestPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                file = JsonSerializer.Deserialize<ManifestFile>(stream, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new OarxModuleException(
                    $"The manifest {manifestPath} is not valid: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new OarxModuleException(
                    $"The manifest {manifestPath} could not be read: {ex.Message}", ex);
            }

            if (file?.Modules == null || file.Modules.Count == 0)
                throw new OarxModuleException(
                    $"The manifest {manifestPath} lists no modules.");

            bool hasBlank = file.Modules
                .Concat(file.PreloadNative ?? new())
                .Concat(file.PreloadManaged ?? new())
                .Any(string.IsNullOrWhiteSpace);
            if (hasBlank)
                throw new OarxModuleException(
                    $"The manifest {manifestPath} has an empty entry.");

            string folder = Path.GetDirectoryName(manifestPath)!;

            var modules = file.Modules.Select(m => Resolve(folder, m)).ToList();
            foreach (string module in modules)
            {
                string ext = Path.GetExtension(module);
                if (!ModuleExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    throw new OarxModuleException(
                        $"The manifest {manifestPath} lists '{Path.GetFileName(module)}' " +
                        "as a module; modules must be .dbx, .arx or .crx.");
            }

            var duplicate = modules
                .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                throw new OarxModuleException(
                    $"The manifest {manifestPath} lists '{duplicate.Key}' more than once.");

            return new NativeGroupManifest(
                modules,
                (file.PreloadNative ?? new()).Select(p => Resolve(folder, p)).ToList(),
                (file.PreloadManaged ?? new()).Select(p => Resolve(folder, p)).ToList());
        }

        private static string Resolve(string folder, string entry)
            => Path.IsPathFullyQualified(entry)
                ? entry
                : Path.GetFullPath(Path.Combine(folder, entry));

        private sealed class ManifestFile
        {
            public List<string>? Modules { get; set; }
            public List<string>? PreloadNative { get; set; }
            public List<string>? PreloadManaged { get; set; }
        }
    }
}
