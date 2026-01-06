using Autodesk.AutoCAD.ApplicationServices;

using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.Models.Nyttetimer;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.Services
{
    /// <summary>
    /// Singleton service for managing Nyttetimer configurations.
    /// Handles loading the default config from embedded CSV and user configs from FlexDataStore.
    /// </summary>
    internal partial class NyttetimerService : ObservableObject
    {
        private static NyttetimerService? _instance;
        public static NyttetimerService Instance => _instance ??= new NyttetimerService();

        private const string EmbeddedResourceName = "DimensioneringV2.Models.Nyttetimer.Anvendelseskoder.csv";

        /// <summary>
        /// The default configuration loaded from embedded CSV (read-only).
        /// </summary>
        public NyttetimerConfiguration DefaultConfiguration { get; }

        /// <summary>
        /// The currently selected configuration.
        /// </summary>
        [ObservableProperty]
        private NyttetimerConfiguration currentConfiguration;

        /// <summary>
        /// All available configurations (default + user-created).
        /// </summary>
        public ObservableCollection<NyttetimerConfiguration> AllConfigurations { get; } = new();

        /// <summary>
        /// The store containing user configurations and selection.
        /// </summary>
        [ObservableProperty]
        private NyttetimerConfigurationStore store;

        private NyttetimerService()
        {
            // Load default configuration from embedded CSV
            DefaultConfiguration = LoadDefaultConfiguration();
            
            // Load store from current document
            store = LoadStore(AcAp.DocumentManager.MdiActiveDocument);
            
            // Build all configurations
            RebuildAllConfigurations();
            
            // Set current configuration
            currentConfiguration = GetConfigurationByName(store.SelectedConfigurationName) ?? DefaultConfiguration;

            // Subscribe to document events
            AcAp.DocumentManager.DocumentActivated += DocumentManager_DocumentActivated;
            AcAp.DocumentManager.DocumentToBeDeactivated += DocumentManager_DocumentToBeDeactivated;
            AcAp.DocumentManager.DocumentToBeDestroyed += DocumentManager_DocumentToBeDestroyed;
        }

        private void DocumentManager_DocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            Store = LoadStore(e.Document);
            RebuildAllConfigurations();
            CurrentConfiguration = GetConfigurationByName(Store.SelectedConfigurationName) ?? DefaultConfiguration;
        }

        private void DocumentManager_DocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            SaveStore(e.Document);
        }

        private void DocumentManager_DocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;
            SaveStore(e.Document);
        }

        /// <summary>
        /// Saves current store to the active document.
        /// </summary>
        public void SaveToActiveDocument()
        {
            SaveStore(AcAp.DocumentManager.MdiActiveDocument);
        }

        private NyttetimerConfiguration LoadDefaultConfiguration()
        {
            var config = new NyttetimerConfiguration(NyttetimerConfiguration.DefaultConfigurationName, true);
            
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
            
            if (stream == null)
            {
                Utils.prtDbg($"Could not find embedded resource: {EmbeddedResourceName}");
                return config;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(';');
                if (parts.Length >= 2)
                {
                    if (int.TryParse(parts[0], out int nyttetimer))
                    {
                        var kode = parts[1];
                        var tekst = parts.Length >= 3 ? parts[2] : null;
                        config.Entries.Add(new NyttetimerEntry(kode, nyttetimer, tekst));
                    }
                }
            }

            return config;
        }

        private NyttetimerConfigurationStore LoadStore(Document? doc)
        {
            if (doc == null)
                return new NyttetimerConfigurationStore();

            return SettingsSerializer<NyttetimerConfigurationStore>.Load(doc);
        }

        private void SaveStore(Document? doc)
        {
            if (doc == null) return;

            // Update store with current state
            Store.SelectedConfigurationName = CurrentConfiguration.Name;
            Store.UserConfigurations.Clear();
            
            foreach (var config in AllConfigurations.Where(c => !c.IsDefault))
            {
                Store.UserConfigurations.Add(new NyttetimerConfigurationData(config));
            }

            SettingsSerializer<NyttetimerConfigurationStore>.Save(doc, Store);
        }

        private void RebuildAllConfigurations()
        {
            AllConfigurations.Clear();
            AllConfigurations.Add(DefaultConfiguration);

            foreach (var data in Store.UserConfigurations)
            {
                var config = data.ToConfiguration(DefaultConfiguration);
                AllConfigurations.Add(config);
            }
        }

        /// <summary>
        /// Gets a configuration by name, or null if not found.
        /// </summary>
        public NyttetimerConfiguration? GetConfigurationByName(string name)
        {
            return AllConfigurations.FirstOrDefault(c => 
                c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Selects a configuration by name.
        /// </summary>
        public void SelectConfiguration(string name)
        {
            var config = GetConfigurationByName(name);
            if (config != null)
            {
                CurrentConfiguration = config;
                Store.SelectedConfigurationName = name;
            }
        }

        /// <summary>
        /// Creates a new configuration based on a template.
        /// </summary>
        public NyttetimerConfiguration CreateNew(string baseName, NyttetimerConfiguration template)
        {
            var name = Store.GetUniqueName(baseName);
            var config = template.Clone(name);
            AllConfigurations.Add(config);
            return config;
        }

        /// <summary>
        /// Duplicates an existing configuration.
        /// </summary>
        public NyttetimerConfiguration Duplicate(NyttetimerConfiguration source)
        {
            var name = Store.GetUniqueName(source.Name);
            var config = source.Clone(name);
            AllConfigurations.Add(config);
            return config;
        }

        /// <summary>
        /// Renames a configuration.
        /// </summary>
        public bool Rename(NyttetimerConfiguration config, string newName)
        {
            if (config.IsDefault)
                return false;

            if (string.IsNullOrWhiteSpace(newName))
                return false;

            // Check for duplicates
            if (AllConfigurations.Any(c => c != config && 
                c.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                return false;

            config.Name = newName;
            
            if (CurrentConfiguration == config)
                Store.SelectedConfigurationName = newName;
            
            return true;
        }

        /// <summary>
        /// Deletes a configuration.
        /// </summary>
        public bool Delete(NyttetimerConfiguration config)
        {
            if (config.IsDefault)
                return false;

            AllConfigurations.Remove(config);

            // If we deleted the current config, switch to default
            if (CurrentConfiguration == config)
            {
                CurrentConfiguration = DefaultConfiguration;
                Store.SelectedConfigurationName = DefaultConfiguration.Name;
            }

            return true;
        }

        /// <summary>
        /// Exports a configuration to CSV file.
        /// </summary>
        public void ExportToCsv(NyttetimerConfiguration config, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            foreach (var entry in config.Entries)
            {
                writer.WriteLine($"{entry.Nyttetimer};{entry.AnvendelsesKode};{entry.AnvendelsesTekst ?? ""}");
            }
        }

        /// <summary>
        /// Imports a configuration from CSV file.
        /// </summary>
        public NyttetimerConfiguration ImportFromCsv(string filePath)
        {
            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var name = Store.GetUniqueName(baseName);
            var config = new NyttetimerConfiguration(name, false);

            using var reader = new StreamReader(filePath, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(';');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int nyttetimer))
                {
                    var kode = parts[1];
                    // We use text from default config, not from import
                    config.Entries.Add(new NyttetimerEntry(kode, nyttetimer));
                }
            }

            config.PopulateTextsFrom(DefaultConfiguration);
            AllConfigurations.Add(config);
            return config;
        }

        /// <summary>
        /// Imports configurations from another DWG file.
        /// </summary>
        public List<NyttetimerConfiguration> ImportFromDwg(string dwgFilePath)
        {
            var imported = new List<NyttetimerConfiguration>();

            try
            {
                // Open database in read-only mode
                using var db = new Autodesk.AutoCAD.DatabaseServices.Database(false, true);
                db.ReadDwgFile(dwgFilePath, Autodesk.AutoCAD.DatabaseServices.FileOpenMode.OpenForReadAndAllShare, true, null);

                // Read the store from that database
                var store = Dreambuild.AutoCAD.FlexDataStoreExtensions.FlexDataStore(db);
                var storeName = typeof(NyttetimerConfigurationStore).Name;
                
                if (store.Has(storeName))
                {
                    var otherStore = store.GetObject<NyttetimerConfigurationStore>(storeName);
                    
                    foreach (var data in otherStore.UserConfigurations)
                    {
                        var name = Store.GetUniqueName(data.Name);
                        var config = new NyttetimerConfiguration(name, false);
                        
                        foreach (var entry in data.Entries)
                        {
                            config.Entries.Add(new NyttetimerEntry(entry.AnvendelsesKode, entry.Nyttetimer));
                        }
                        
                        config.PopulateTextsFrom(DefaultConfiguration);
                        AllConfigurations.Add(config);
                        imported.Add(config);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.prtDbg($"Error importing from DWG: {ex.Message}");
            }

            return imported;
        }

        /// <summary>
        /// Gets the Nyttetimer value for a given AnvendelsesKode from the current configuration.
        /// Returns the default value from HydraulicSettings if not found or code is empty.
        /// </summary>
        public int GetNyttetimer(string? anvendelsesKode, int defaultValue)
        {            
            if (string.IsNullOrWhiteSpace(anvendelsesKode))
                return defaultValue;

            var value = CurrentConfiguration.GetNyttetimer(anvendelsesKode);
            return value ?? defaultValue;
        }
    }
}

