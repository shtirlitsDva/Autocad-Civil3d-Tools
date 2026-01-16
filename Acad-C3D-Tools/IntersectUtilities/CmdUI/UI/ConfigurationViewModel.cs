using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

using IntersectUtilities.UtilsCommon.DataManager.CsvData;

namespace IntersectUtilities.CmdUI.UI
{
    /// <summary>
    /// ViewModel for the Configuration tab.
    /// </summary>
    public partial class ConfigurationViewModel : ObservableObject
    {
        /// <summary>
        /// Represents "(None)" in the dropdown - signals no configuration is selected.
        /// </summary>
        public const string NoneValue = "(None)";

        [ObservableProperty]
        private ObservableCollection<string> availableConfigurations;

        [ObservableProperty]
        private string selectedConfiguration;

        public ConfigurationViewModel()
        {
            // Ensure Csv class is initialized so it subscribes to configuration changes
            // This is needed because the static constructor won't run until Csv is first accessed
            _ = Csv.IsVersionedDataAvailable;
            
            // Load available configurations
            var configs = CsvRegistry.AvailableConfigurations.ToList();
            
            // Add "(None)" at the beginning
            configs.Insert(0, NoneValue);
            
            AvailableConfigurations = new ObservableCollection<string>(configs);

            // Set the selected configuration from the persisted value
            string? activeConfig = ConfigurationManager.ActiveConfiguration;
            if (string.IsNullOrEmpty(activeConfig))
            {
                SelectedConfiguration = NoneValue;
            }
            else if (AvailableConfigurations.Contains(activeConfig))
            {
                SelectedConfiguration = activeConfig;
            }
            else
            {
                // Persisted config doesn't exist anymore
                SelectedConfiguration = NoneValue;
            }
        }

        partial void OnSelectedConfigurationChanged(string value)
        {
            // Don't persist "(None)" - only valid configurations
            if (value == NoneValue)
            {
                // Clear the configuration (but don't persist null)
                ConfigurationManager.ActiveConfiguration = null;
            }
            else
            {
                // Set and persist the configuration
                ConfigurationManager.ActiveConfiguration = value;
            }
        }

        /// <summary>
        /// Refreshes the available configurations from the file system.
        /// </summary>
        public void RefreshConfigurations()
        {
            CsvRegistry.Refresh();

            var configs = CsvRegistry.AvailableConfigurations.ToList();
            configs.Insert(0, NoneValue);

            AvailableConfigurations.Clear();
            foreach (var config in configs)
            {
                AvailableConfigurations.Add(config);
            }

            // Re-validate selected configuration
            string? activeConfig = ConfigurationManager.ActiveConfiguration;
            if (string.IsNullOrEmpty(activeConfig) || !AvailableConfigurations.Contains(activeConfig))
            {
                SelectedConfiguration = NoneValue;
            }
        }

        /// <summary>
        /// Gets whether a valid configuration is currently selected.
        /// </summary>
        public bool IsConfigurationSet => SelectedConfiguration != NoneValue;
    }
}
