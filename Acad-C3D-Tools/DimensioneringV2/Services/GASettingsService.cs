using Autodesk.AutoCAD.ApplicationServices;

using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.Genetic;

using System;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.Services
{
    /// <summary>
    /// Singleton service that holds the current GA configuration settings.
    /// Settings are persisted per-document using FlexDataStore.
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
            settings = SettingsSerializer<GASettings>.Load(
                AcAp.DocumentManager.MdiActiveDocument);

            // Subscribe to DocumentManager events
            AcAp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
            AcAp.DocumentManager.DocumentToBeDeactivated += DocumentManager_DocumentToBeDeactivated;
            AcAp.DocumentManager.DocumentToBeDestroyed += DocumentManager_DocumentToBeDestroyed;
        }

        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            var loaded = SettingsSerializer<GASettings>.Load(e.Document);
            // To avoid having OLD instances of the settings still bound to the UI
            // Do not create new instance of the settings, but copy the loaded settings
            Settings.CopyFrom(loaded);
        }

        private void DocumentManager_DocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            SettingsSerializer<GASettings>.Save(e.Document, Settings);
        }

        private void DocumentManager_DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            SettingsSerializer<GASettings>.Save(e.Document, Settings);
        }

        /// <summary>
        /// Saves current settings to the active document.
        /// </summary>
        public void SaveToActiveDocument()
        {
            Utils.prtDbg($"Saving GA settings to {AcAp.DocumentManager.MdiActiveDocument?.Name}");
            SettingsSerializer<GASettings>.Save(
                AcAp.DocumentManager.MdiActiveDocument,
                Settings);
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
