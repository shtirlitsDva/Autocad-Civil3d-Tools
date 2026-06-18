using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

        /// <summary>
        /// Human-readable status of the selected configuration (active / none / incomplete).
        /// </summary>
        [ObservableProperty]
        private string statusMessage = string.Empty;

        /// <summary>
        /// True when the selected configuration is incomplete (missing a required versioned file).
        /// The user is NOT blocked from selecting it; instead the palette shows this error and any
        /// command that requests the missing data will throw with guidance.
        /// </summary>
        [ObservableProperty]
        private bool isConfigurationError;

        /// <summary>
        /// True when no configuration is selected ("(None)").
        /// </summary>
        [ObservableProperty]
        private bool isConfigurationMissing;

        public ConfigurationViewModel()
        {
            // Safe default so the control always has something to bind to, even if loading fails.
            availableConfigurations = new ObservableCollection<string> { NoneValue };

            try
            {
                // Ensure Csv class is initialized so it subscribes to configuration changes
                // This is needed because the static constructor won't run until Csv is first accessed
                _ = Csv.IsVersionedDataAvailable;

                // Load available configurations (discovery is hardened and never throws)
                var configs = CsvRegistry.AvailableConfigurations.ToList();
                configs.Insert(0, NoneValue);
                AvailableConfigurations = new ObservableCollection<string>(configs);

                // Restore the persisted selection (read it BEFORE setting SelectedConfiguration,
                // because setting the property writes back to ConfigurationManager).
                string? activeConfig = ConfigurationManager.ActiveConfiguration;
                if (!string.IsNullOrEmpty(activeConfig) && ContainsConfiguration(activeConfig))
                    SelectedConfiguration = activeConfig;
                else
                    SelectedConfiguration = NoneValue;
            }
            catch (System.Exception)
            {
                // Never let palette construction crash; degrade to "(None)".
                SelectedConfiguration = NoneValue;
            }

            UpdateStatus();
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
                // Set and persist the configuration. Incomplete configurations are allowed here
                // on purpose - the status below tells the user what (if anything) is wrong.
                ConfigurationManager.ActiveConfiguration = value;
            }

            UpdateStatus();
        }

        /// <summary>
        /// Re-discovers configurations from the file system and re-validates the selection.
        /// Lets the user pick up newly-added files (e.g. a missing versioned file or a new
        /// unversioned fallback) without restarting AutoCAD.
        /// </summary>
        [RelayCommand]
        private void Refresh()
        {
            RefreshConfigurations();
        }

        /// <summary>
        /// Refreshes the available configurations from the file system.
        /// </summary>
        public void RefreshConfigurations()
        {
            try
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
                if (string.IsNullOrEmpty(activeConfig) || !ContainsConfiguration(activeConfig))
                {
                    SelectedConfiguration = NoneValue;
                }
            }
            catch (System.Exception)
            {
                // Never let a refresh crash the palette; UpdateStatus reflects the current state.
            }

            UpdateStatus();
        }

        /// <summary>
        /// Recomputes the status message for the currently active configuration. Completeness is
        /// re-checked against the file system on every call, so a fix is reflected immediately.
        /// </summary>
        private void UpdateStatus()
        {
            try
            {
                string? token = ConfigurationManager.ActiveConfiguration;

                if (string.IsNullOrEmpty(token))
                {
                    IsConfigurationError = false;
                    IsConfigurationMissing = true;
                    StatusMessage = "No configuration selected — choose one above (e.g. DKv1, DKv2, DEv1).";
                    return;
                }

                if (!CsvRegistry.IsValidConfigurationName(token))
                {
                    IsConfigurationError = true;
                    IsConfigurationMissing = false;
                    StatusMessage =
                        $"'{token}' has an INVALID name — configuration names must not contain '.'. " +
                        "Rename the file(s) to a dot-free name (e.g. 'DEv1'), then click Refresh. " +
                        "Other configurations are unaffected.";
                    return;
                }

                var missing = CsvRegistry.GetMissingRequiredFiles(token);
                if (missing.Count == 0)
                {
                    IsConfigurationError = false;
                    IsConfigurationMissing = false;
                    StatusMessage = $"'{token}' is active and complete.";
                }
                else
                {
                    IsConfigurationError = true;
                    IsConfigurationMissing = false;
                    StatusMessage =
                        $"'{token}' is INCOMPLETE — missing required file(s): {string.Join(", ", missing)}. " +
                        "Commands that need this data will fail until you add the file(s) " +
                        "(or an unversioned fallback file) and click Refresh.";
                }
            }
            catch (System.Exception ex)
            {
                // Never let a status refresh crash the palette.
                IsConfigurationError = true;
                IsConfigurationMissing = false;
                StatusMessage = $"Could not evaluate configuration status: {ex.Message}";
            }
        }

        private bool ContainsConfiguration(string config) =>
            AvailableConfigurations.Any(c => string.Equals(c, config, System.StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets whether a valid configuration is currently selected.
        /// </summary>
        public bool IsConfigurationSet => SelectedConfiguration != NoneValue;
    }
}
