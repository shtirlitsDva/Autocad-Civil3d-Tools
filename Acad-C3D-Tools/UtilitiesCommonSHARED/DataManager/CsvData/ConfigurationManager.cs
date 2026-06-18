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
        private static bool _isInitialized = false;

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
                EnsureInitialized();
                return _activeConfiguration;
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
║   │  1. Run NSCMD                                                           │     ║
║   │  2. Select DKv1, DKv2 or DEv1 from the dropdown                         │     ║
║   │  3. Run this command again                                              │     ║
║   │                                                                         │     ║
║   │  Your selection will be saved for future sessions.                      │     ║
║   └─────────────────────────────────────────────────────────────────────────┘     ║
║                                                                                   ║
╚═══════════════════════════════════════════════════════════════════════════════════╝
";
        }

        private static void EnsureInitialized()
        {
            if (_isInitialized) return;

            lock (_lock)
            {
                if (_isInitialized) return;

                _activeConfiguration = LoadPersistedConfiguration();
                _isInitialized = true;
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
