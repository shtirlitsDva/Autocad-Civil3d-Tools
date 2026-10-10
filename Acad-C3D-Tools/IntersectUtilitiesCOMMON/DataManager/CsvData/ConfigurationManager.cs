using System;
using System.IO;
using System.Text.Json;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// Manages the active CSV configuration with persistence to AppData.
    /// This is a static singleton accessible from anywhere in IntersectUtilities.
    /// </summary>
    public static class ConfigurationManager
    {
        private static readonly object _lock = new();
        private static string? _activeConfiguration;

        // Every plugin that compiles this file has its own copy of this class, and they share
        // one config file: NSCONF (NorsynDrawingToolsManaged) writes it, LERImporter reads it.
        // So a copy re-reads the file when its write time moves, at most once per
        // RecheckIntervalMs, because the getter is called inside data loops and AppData can
        // sit on a network share.
        private const long RecheckIntervalMs = 1000;
        // The file's write time when it was last read; null before the first read.
        private static DateTime? _readStamp;
        private static long _checkedAtMs;

        /// <summary>
        /// The path to the configuration file in AppData.
        /// </summary>
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "ApplicationPlugins",
            "IntersectUtilities",
            "config.json");

        /// <summary>
        /// Gets or sets the active configuration (e.g., "V1", "V2").
        /// Returns null if no configuration is set (first run or "(None)" selected).
        /// Setting to null clears the persisted configuration.
        /// </summary>
        public static string? ActiveConfiguration
        {
            get
            {
                bool changedOnDisk;
                string? active;
                lock (_lock)
                {
                    changedOnDisk = ReloadIfFileChanged();
                    active = _activeConfiguration;
                }

                // Outside the lock: subscribers read ActiveConfiguration and take locks of their own.
                if (changedOnDisk)
                {
                    prdDbg($"ConfigurationManager: Configuration changed on disk to '{active}'");
                    ConfigurationChanged?.Invoke(null, EventArgs.Empty);
                }
                return active;
            }
            set
            {
                lock (_lock)
                {
                    // Don't persist null/"(None)" - only valid configurations
                    if (string.IsNullOrEmpty(value))
                    {
                        _activeConfiguration = null;
                        // Don't persist null - leave file as-is or delete it
                    }
                    else
                    {
                        _activeConfiguration = value;
                        PersistConfiguration(value);
                    }

                    // What is on disk now is what this copy holds: its own write must not read
                    // back as a change, and a "(None)" must hold until another copy writes.
                    _readStamp = FileStamp();
                    _checkedAtMs = Environment.TickCount64;

                    // Notify subscribers
                    int subscriberCount = ConfigurationChanged?.GetInvocationList()?.Length ?? 0;
                    prdDbg($"ConfigurationManager: Firing ConfigurationChanged event to {subscriberCount} subscriber(s)");
                    ConfigurationChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Returns true if a valid configuration is currently set.
        /// </summary>
        public static bool IsConfigurationSet => ActiveConfiguration != null;

        /// <summary>
        /// Event raised when the configuration changes.
        /// </summary>
        public static event EventHandler? ConfigurationChanged;

        /// <summary>
        /// Ensures a configuration is set. Throws if not.
        /// Call this at the start of any command that requires CSV data.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no configuration is selected.</exception>
        public static void EnsureConfigurationSet()
        {
            if (ActiveConfiguration == null)
            {
                throw new InvalidOperationException(GetConfigurationRequiredMessage());
            }
        }

        private static string GetConfigurationRequiredMessage()
        {
            return @"
╔═══════════════════════════════════════════════════════════════════════════════════╗
║                                                                                   ║
║     ██████╗ ██████╗ ███╗   ██╗███████╗██╗ ██████╗                                 ║
║    ██╔════╝██╔═══██╗████╗  ██║██╔════╝██║██╔════╝                                 ║
║    ██║     ██║   ██║██╔██╗ ██║█████╗  ██║██║  ███╗                                ║
║    ██║     ██║   ██║██║╚██╗██║██╔══╝  ██║██║   ██║                                ║
║    ╚██████╗╚██████╔╝██║ ╚████║██║     ██║╚██████╔╝                                ║
║     ╚═════╝ ╚═════╝ ╚═╝  ╚═══╝╚═╝     ╚═╝ ╚═════╝                                 ║
║                                                                                   ║
║    ███╗   ███╗██╗███████╗███████╗██╗███╗   ██╗ ██████╗ ██╗██╗██╗                  ║
║    ████╗ ████║██║██╔════╝██╔════╝██║████╗  ██║██╔════╝ ██║██║██║                  ║
║    ██╔████╔██║██║███████╗███████╗██║██╔██╗ ██║██║  ███╗██║██║██║                  ║
║    ██║╚██╔╝██║██║╚════██║╚════██║██║██║╚██╗██║██║   ██║╚═╝╚═╝╚═╝                  ║
║    ██║ ╚═╝ ██║██║███████║███████║██║██║ ╚████║╚██████╔╝██╗██╗██╗                  ║
║    ╚═╝     ╚═╝╚═╝╚══════╝╚══════╝╚═╝╚═╝  ╚═══╝ ╚═════╝ ╚═╝╚═╝╚═╝                  ║
║                                                                                   ║
╠═══════════════════════════════════════════════════════════════════════════════════╣
║                                                                                   ║
║   ⚠️  NO CSV CONFIGURATION SELECTED!                                              ║
║                                                                                   ║
║   This command requires versioned CSV data, but you haven't selected              ║
║   a configuration version yet.                                                    ║
║                                                                                   ║
║   ┌─────────────────────────────────────────────────────────────────────────┐     ║
║   │  HOW TO FIX:                                                            │     ║
║   │                                                                         │     ║
║   │  1. Run NSCONF (or NSCMD in Civil 3D)                                   │     ║
║   │  2. Select a configuration (e.g. DKv1, DKv2, DEv1) from the dropdown    │     ║
║   │  3. Run this command again                                              │     ║
║   │                                                                         │     ║
║   │  Your selection will be saved for future sessions.                      │     ║
║   └─────────────────────────────────────────────────────────────────────────┘     ║
║                                                                                   ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
";
        }

        /// <summary>
        /// Reads the file on the first call, and again when its write time has moved. Called
        /// under the lock. True when a re-read changed the configuration this copy held.
        /// </summary>
        private static bool ReloadIfFileChanged()
        {
            long now = Environment.TickCount64;
            if (_readStamp != null && now - _checkedAtMs < RecheckIntervalMs) return false;
            _checkedAtMs = now;

            DateTime stamp = FileStamp();
            if (_readStamp == stamp) return false;

            bool firstRead = _readStamp == null;
            _readStamp = stamp;
            string? persisted = LoadPersistedConfiguration();
            bool changed = !firstRead && !string.Equals(persisted, _activeConfiguration, StringComparison.Ordinal);
            _activeConfiguration = persisted;
            return changed;
        }

        /// <summary>
        /// The config file's last write time (UTC). A missing file gives 1601-01-01, which is a
        /// stamp like any other; an unreadable one keeps the last stamp, so nothing is re-read.
        /// </summary>
        private static DateTime FileStamp()
        {
            try
            {
                return File.GetLastWriteTimeUtc(ConfigFilePath);
            }
            catch (Exception ex)
            {
                prdDbg($"Warning: Failed to read the CSV configuration file's write time: {ex.Message}");
                return _readStamp ?? DateTime.MinValue;
            }
        }

        private static string? LoadPersistedConfiguration()
        {
            try
            {
                prdDbg($"ConfigurationManager: Looking for config file at: {ConfigFilePath}");
                
                if (!File.Exists(ConfigFilePath))
                {
                    prdDbg($"ConfigurationManager: No persisted configuration found (first run)");
                    return null;
                }

                string json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<ConfigData>(json);
                
                if (config != null && !string.IsNullOrEmpty(config.ActiveConfiguration))
                {
                    prdDbg($"ConfigurationManager: Loaded persisted configuration: {config.ActiveConfiguration}");
                    return config.ActiveConfiguration;
                }
            }
            catch (Exception ex)
            {
                prdDbg($"Warning: Failed to load CSV configuration: {ex.Message}");
            }

            return null;
        }

        private static void PersistConfiguration(string configuration)
        {
            try
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var config = new ConfigData { ActiveConfiguration = configuration };
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);

                prdDbg($"CSV Configuration saved: {configuration}");
            }
            catch (Exception ex)
            {
                prdDbg($"Warning: Failed to save CSV configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Internal class for JSON serialization.
        /// </summary>
        private class ConfigData
        {
            public string? ActiveConfiguration { get; set; }
        }
    }
}
