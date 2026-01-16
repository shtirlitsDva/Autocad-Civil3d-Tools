using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <summary>
        /// Regex pattern to match versioned files like "Krydsninger.v1.csv", "Lag-Ler2.0.v2.csv"
        /// </summary>
        private static readonly Regex VersionPattern = new(@"\.v(\d+)\.csv$", RegexOptions.IgnoreCase);

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
            var configurations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(ConfPath))
            {
                prdDbg($"Warning: CSV configuration folder not found: {ConfPath}");
                _availableConfigurations = new List<string>();
                return;
            }

            foreach (string file in Directory.GetFiles(ConfPath, "*.csv"))
            {
                string fileName = Path.GetFileName(file);
                
                // Skip the register file
                if (fileName.Equals(RegisterFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = VersionPattern.Match(fileName);
                if (match.Success)
                {
                    string version = match.Groups[1].Value;
                    configurations.Add($"V{version}");
                }
            }

            _availableConfigurations = configurations.OrderBy(c => 
            {
                if (c.StartsWith("V") && int.TryParse(c.Substring(1), out int num))
                    return num;
                return int.MaxValue;
            }).ToList();

            prdDbg($"Discovered CSV configurations: {string.Join(", ", _availableConfigurations)}");
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
        /// </summary>
        /// <param name="baseName">The base name without version (e.g., "Krydsninger")</param>
        /// <param name="extension">The file extension pattern (e.g., ".csv" or ".0.csv" for "Lag-Ler2.0")</param>
        /// <returns>The full path to the versioned file.</returns>
        public static string GetVersionedFilePath(string baseName, string extension = ".csv")
        {
            ConfigurationManager.EnsureConfigurationSet();
            
            int? version = ConfigurationManager.GetVersionNumber();
            if (!version.HasValue)
            {
                throw new InvalidOperationException("Could not determine version number from configuration.");
            }

            // Build the versioned filename
            // For "Krydsninger" -> "Krydsninger.v1.csv"
            // For "Lag-Ler2.0" -> "Lag-Ler2.0.v1.csv"
            string fileName = $"{baseName}.v{version}{extension}";
            return Path.Combine(ConfPath, fileName);
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
