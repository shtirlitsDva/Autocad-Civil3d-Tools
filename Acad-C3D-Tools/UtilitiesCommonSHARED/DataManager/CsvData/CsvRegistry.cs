using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.VisualBasic.FileIO;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// Registry that discovers CSV configurations and manages file paths.
    /// </summary>
    public static class CsvRegistry
    {
        /// <summary>
        /// The base path where CSV files are located.
        /// </summary>
        public const string ConfPath = @"X:\AutoCAD DRI - 01 Civil 3D\Conf";

        /// <summary>
        /// The name of the register file that lists additional CSV files.
        /// </summary>
        private const string RegisterFileName = "_csv_register_add_files.csv";

        private static readonly object _lock = new();
        private static List<string>? _availableConfigurations;
        private static Dictionary<string, string>? _additionalFiles;

        // ── Discovery of configurable doc types ────────────────────────────────────────
        // CsvRegistry hardcodes NO file names. The doc types that take part in configurations
        // declare their own base name (via IConfigurableCsv) in the data source that asks for the
        // file. CsvRegistry finds those implementations by reflection and derives everything else
        // from the file system at runtime:
        //   • which configurations exist          -> tokens found on disk for those base names
        //   • versioned-only vs fallback-capable  -> whether the unversioned "{base}.csv" exists
        //   • whether a configuration is complete -> do all versioned-only docs have its file?
        // Naming contract: base names AND configuration tokens must not contain '.', so that
        // "{base}.{token}.csv" stays unambiguous. Violations are handled GRACEFULLY (a dotted
        // base name is ignored with a warning; a dotted token is listed but flagged invalid),
        // so discovery never throws and the palette never crashes.
        private static List<string>? _configurableBaseNames;

        private static IReadOnlyList<string> ConfigurableBaseNames
        {
            get
            {
                if (_configurableBaseNames != null) return _configurableBaseNames;
                lock (_lock)
                {
                    _configurableBaseNames ??= DiscoverConfigurableBaseNames();
                }
                return _configurableBaseNames;
            }
        }

        private static List<string> DiscoverConfigurableBaseNames()
        {
            var names = new List<string>();
            try
            {
                Type[] types;
                try
                {
                    types = typeof(CsvRegistry).Assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IConfigurableCsv).IsAssignableFrom(type)) continue;

                    try
                    {
                        var instance = (IConfigurableCsv)Activator.CreateInstance(type)!;
                        string baseName = instance.BaseName;

                        if (string.IsNullOrWhiteSpace(baseName)) continue;

                        // Guard: base names must be dot-free, otherwise "{base}.{token}.csv" would be
                        // ambiguous. Ignore a dotted base name (degrade gracefully) rather than let it
                        // corrupt discovery for every configuration.
                        if (baseName.Contains("."))
                        {
                            prdDbg($"Warning: configurable CSV '{type.Name}' has a dotted base name " +
                                   $"'{baseName}' and is ignored. Base names must not contain '.'.");
                            continue;
                        }

                        names.Add(baseName);
                    }
                    catch (Exception ex)
                    {
                        prdDbg($"Warning: could not read base name from '{type?.Name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                prdDbg($"Warning: failed to discover configurable CSV doc types: {ex.Message}");
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static Regex TokenRegex(string baseName) =>
            new("^" + Regex.Escape(baseName) + @"\.(.+)\.csv$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Gets all available configurations discovered from versioned CSV files.
        /// </summary>
        public static IReadOnlyList<string> AvailableConfigurations
        {
            get
            {
                EnsureDiscovered();
                return _availableConfigurations!;
            }
        }

        /// <summary>
        /// Gets additional files registered in _csv_register_add_files.csv.
        /// Key = Name, Value = Full Path
        /// </summary>
        public static IReadOnlyDictionary<string, string> AdditionalFiles
        {
            get
            {
                EnsureDiscovered();
                return _additionalFiles!;
            }
        }

        private static void EnsureDiscovered()
        {
            if (_availableConfigurations != null) return;

            lock (_lock)
            {
                if (_availableConfigurations != null) return;

                DiscoverConfigurations();
                LoadAdditionalFiles();
            }
        }

        private static void DiscoverConfigurations()
        {
            // Hardened: any failure degrades to "no configurations" instead of crashing the palette.
            try
            {
                if (!Directory.Exists(ConfPath))
                {
                    prdDbg($"Warning: CSV configuration folder not found: {ConfPath}");
                    _availableConfigurations = new List<string>();
                    return;
                }

                var fileNames = Directory.GetFiles(ConfPath, "*.csv")
                    .Select(Path.GetFileName)
                    .ToList();

                // Collect EVERY token that appears on any configurable file (union, not intersection).
                // Incomplete or invalid configurations are intentionally NOT filtered out here: the
                // user must be able to select them, see what is wrong in the palette, and act on it.
                // Completeness/validity are checked separately (GetMissingRequiredFiles,
                // IsValidConfigurationName).
                var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string baseName in ConfigurableBaseNames)
                {
                    var regex = TokenRegex(baseName);
                    foreach (string fileName in fileNames)
                    {
                        var match = regex.Match(fileName);
                        if (match.Success)
                            tokens.Add(match.Groups[1].Value);
                    }
                }

                _availableConfigurations = tokens
                    .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                prdDbg($"Discovered CSV configurations: {string.Join(", ", _availableConfigurations)}");
            }
            catch (Exception ex)
            {
                prdDbg($"Warning: failed to discover CSV configurations: {ex.Message}");
                _availableConfigurations = new List<string>();
            }
        }

        private static void LoadAdditionalFiles()
        {
            _additionalFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string registerPath = Path.Combine(ConfPath, RegisterFileName);
            if (!File.Exists(registerPath))
            {
                return;
            }

            try
            {
                using var parser = new TextFieldParser(registerPath);
                parser.CommentTokens = new[] { "#" };
                parser.SetDelimiters(new[] { ";" });
                parser.HasFieldsEnclosedInQuotes = false;

                // Skip header
                if (!parser.EndOfData)
                {
                    parser.ReadFields();
                }

                while (!parser.EndOfData)
                {
                    string[]? fields = parser.ReadFields();
                    if (fields == null || fields.Length < 2) continue;

                    string name = fields[0].Trim();
                    string path = fields[1].Trim();

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(path))
                    {
                        _additionalFiles[name] = path;
                    }
                }
            }
            catch (Exception ex)
            {
                prdDbg($"Warning: Failed to load CSV register file: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the file path for a non-versioned CSV file.
        /// </summary>
        /// <param name="fileName">The CSV file name (e.g., "Distances.csv")</param>
        /// <returns>The full path to the file.</returns>
        public static string GetFilePath(string fileName)
        {
            return Path.Combine(ConfPath, fileName);
        }

        /// <summary>
        /// Gets the file path for a versioned CSV file based on the active configuration.
        /// The active configuration token is used verbatim as the filename suffix.
        /// Throws if no configuration is set (these files are mandatory and have no fallback).
        /// </summary>
        /// <param name="baseName">The base name without the token (e.g., "Krydsninger")</param>
        /// <param name="extension">The file extension (default ".csv")</param>
        /// <returns>The full path to the versioned file.</returns>
        public static string GetVersionedFilePath(string baseName, string extension = ".csv")
        {
            ConfigurationManager.EnsureConfigurationSet();

            string? token = ConfigurationManager.ActiveConfiguration;
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Could not determine configuration token.");
            }

            ValidateConfigurationName(token);

            // Build the versioned filename using the token verbatim:
            // For "Krydsninger" + "DKv1" -> "Krydsninger.DKv1.csv"
            // For "Lag-Ler2.0" + "DEv1" -> "Lag-Ler2.0.DEv1.csv"
            string fileName = $"{baseName}.{token}{extension}";
            string fullPath = Path.Combine(ConfPath, fileName);

            // This doc type has no unversioned fallback, so a missing file means the selected
            // configuration is incomplete. Surface a descriptive error to the AutoCAD command
            // line (only happens now, on an actual data request — not while merely browsing the
            // palette) and abort, so the user gets guidance instead of a bare FileNotFound.
            if (!File.Exists(fullPath))
            {
                string message = DescribeIncompleteConfiguration(token);
                prdDbg(message);
                throw new InvalidOperationException(message);
            }

            return fullPath;
        }

        /// <summary>
        /// Gets the file path for a configuration-aware CSV file that falls back to a shared,
        /// unversioned file when no dedicated file exists for the active configuration.
        /// Used by Distances/Dybde: configurations like "DKv1"/"DKv2" share the unversioned
        /// file, while "DEv1" can ship a dedicated copy (e.g. "Distances.DEv1.csv").
        /// Unlike <see cref="GetVersionedFilePath"/>, this does NOT require a configuration to
        /// be set — with no configuration (or no dedicated file) it returns the fallback.
        /// </summary>
        /// <param name="baseName">The base name without the token (e.g., "Distances")</param>
        /// <param name="fallbackFileName">The shared unversioned file name (e.g., "Distances.csv")</param>
        /// <param name="extension">The file extension (default ".csv")</param>
        /// <returns>The dedicated file path if it exists, otherwise the unversioned fallback path.</returns>
        public static string GetVersionedFilePathOrDefault(
            string baseName, string fallbackFileName, string extension = ".csv")
        {
            string? token = ConfigurationManager.ActiveConfiguration;
            if (!string.IsNullOrEmpty(token))
            {
                // An invalid (dotted) configuration name must supply no data at all, so that an
                // invalid config fails uniformly rather than silently falling back here.
                ValidateConfigurationName(token);

                string candidate = Path.Combine(ConfPath, $"{baseName}.{token}{extension}");
                if (File.Exists(candidate))
                    return candidate;
            }

            return Path.Combine(ConfPath, fallbackFileName);
        }

        /// <summary>
        /// A configurable doc type is "fallback-capable" when an unversioned "{base}.csv" exists.
        /// Such a doc type can serve any configuration (dedicated file when present, otherwise the
        /// unversioned one), so it never makes a configuration incomplete. A doc type without an
        /// unversioned file is "versioned-only" and must provide a dedicated file per configuration.
        /// This classification is derived from the file system, never hardcoded.
        /// </summary>
        public static bool HasUnversionedFallback(string baseName) =>
            File.Exists(Path.Combine(ConfPath, $"{baseName}.csv"));

        /// <summary>
        /// Returns the file names a configuration REQUIRES but is missing. Only versioned-only
        /// doc types can appear here (fallback-capable docs are always satisfiable). An empty
        /// list means the configuration is complete and safe to use.
        /// Re-evaluated against the file system on every call, so adding a missing file (or an
        /// unversioned fallback) is picked up immediately without a restart.
        /// </summary>
        public static IReadOnlyList<string> GetMissingRequiredFiles(string configuration)
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(configuration))
                return missing;

            foreach (string baseName in ConfigurableBaseNames)
            {
                if (HasUnversionedFallback(baseName))
                    continue; // fallback covers any absence -> never required

                string fileName = $"{baseName}.{configuration}.csv";
                if (!File.Exists(Path.Combine(ConfPath, fileName)))
                    missing.Add(fileName);
            }

            return missing;
        }

        /// <summary>
        /// True when the configuration has every file it requires (no versioned-only doc missing).
        /// </summary>
        public static bool IsConfigurationComplete(string configuration) =>
            GetMissingRequiredFiles(configuration).Count == 0;

        /// <summary>
        /// Builds a human-readable description of why a configuration is incomplete, including the
        /// missing files and how to fix it. Used both for the AutoCAD command-line error (on a
        /// data request) and as the basis for the palette warning.
        /// </summary>
        public static string DescribeIncompleteConfiguration(string configuration)
        {
            var missing = GetMissingRequiredFiles(configuration);

            var lines = new List<string>
            {
                $"CSV configuration '{configuration}' is INCOMPLETE and cannot supply data.",
                "Missing required file(s) (these doc types have no unversioned fallback):"
            };
            foreach (string f in missing)
                lines.Add($"    - {f}");
            lines.Add($"Location: {ConfPath}");
            lines.Add("To fix: add the missing versioned file(s), OR add an unversioned base file");
            lines.Add("(e.g. \"Krydsninger.csv\") to enable fallback. Then refresh the configuration");
            lines.Add("in the NSCMD palette (or restart AutoCAD).");

            return string.Join("\n", lines);
        }

        /// <summary>
        /// A configuration name is valid only if it is non-empty and contains no '.'. A dot would
        /// reintroduce the "{base}.{token}.csv" ambiguity, so such names are rejected.
        /// </summary>
        public static bool IsValidConfigurationName(string configuration) =>
            !string.IsNullOrEmpty(configuration) && !configuration.Contains(".");

        /// <summary>
        /// Builds a human-readable description of why a configuration name is invalid and how to fix
        /// it. Used both for the AutoCAD command-line error (on a data request) and the palette.
        /// </summary>
        public static string DescribeInvalidConfigurationName(string configuration)
        {
            var lines = new List<string>
            {
                $"CSV configuration '{configuration}' has an INVALID name and cannot supply data.",
                "Configuration names must not contain '.' (a dot makes \"{base}.{name}.csv\" ambiguous).",
                $"Rename the file(s) in {ConfPath} to a dot-free name (e.g. 'DEv1' instead of 'DE.v1'),",
                "then refresh the configuration in the NSCMD palette (or restart AutoCAD)."
            };
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Throws a descriptive, console-printed error if the configuration name is invalid (dotted).
        /// This is the data-request guard: it never fires during discovery, so the palette never
        /// crashes and other, valid configurations are unaffected.
        /// </summary>
        private static void ValidateConfigurationName(string configuration)
        {
            if (!IsValidConfigurationName(configuration))
            {
                string message = DescribeInvalidConfigurationName(configuration);
                prdDbg(message);
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Gets the file path for an additional CSV file registered in the register.
        /// </summary>
        /// <param name="name">The registered name (e.g., "AnvKoder")</param>
        /// <returns>The full path to the file.</returns>
        public static string GetAdditionalFilePath(string name)
        {
            EnsureDiscovered();

            if (_additionalFiles!.TryGetValue(name, out string? path))
            {
                return path;
            }

            throw new KeyNotFoundException($"Additional CSV file '{name}' not found in register.");
        }

        /// <summary>
        /// Forces re-discovery of configurations.
        /// </summary>
        public static void Refresh()
        {
            lock (_lock)
            {
                _availableConfigurations = null;
                _additionalFiles = null;
            }
        }
    }
}
