using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.Genetic;

using System;

namespace DimensioneringV2.Services
{
    /// <summary>
    /// Singleton service that holds the current GA configuration settings.
    /// Unlike HydraulicSettingsService, GA settings are not persisted per-document
    /// as they are algorithm parameters rather than project-specific data.
    /// </summary>
    internal partial class GASettingsService : ObservableObject
    {
        private static GASettingsService? _instance;
        
        /// <summary>
        /// Gets the singleton instance of the GASettingsService.
        /// </summary>
        public static GASettingsService Instance => _instance ??= new GASettingsService();

        [ObservableProperty]
        private GASettings settings;

        private GASettingsService()
        {
            settings = new GASettings();
        }

        /// <summary>
        /// Resets all settings to their default values.
        /// </summary>
        public void ResetToDefaults()
        {
            Settings = new GASettings();
        }
    }
}
