using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace DimensioneringV2.Models.Nyttetimer
{
    /// <summary>
    /// Container for all Nyttetimer configurations in a document.
    /// This is what gets persisted to FlexDataStore.
    /// </summary>
    public partial class NyttetimerConfigurationStore : ObservableObject
    {
        /// <summary>
        /// The name of the currently selected configuration.
        /// </summary>
        [ObservableProperty]
        private string selectedConfigurationName = NyttetimerConfiguration.DefaultConfigurationName;

        /// <summary>
        /// User-created configurations (excluding the default).
        /// Only Nyttetimer values are persisted, not AnvendelsesTekst.
        /// </summary>
        public ObservableCollection<NyttetimerConfigurationData> UserConfigurations { get; set; } = new();

        public NyttetimerConfigurationStore() { }

        /// <summary>
        /// Gets a unique name for a new configuration, adding numeric suffix if needed.
        /// </summary>
        public string GetUniqueName(string baseName)
        {
            var existingNames = UserConfigurations.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // Also reserve the default name
            existingNames.Add(NyttetimerConfiguration.DefaultConfigurationName);

            if (!existingNames.Contains(baseName))
                return baseName;

            int suffix = 1;
            string candidate;
            do
            {
                candidate = $"{baseName} ({suffix})";
                suffix++;
            } while (existingNames.Contains(candidate));

            return candidate;
        }
    }

    /// <summary>
    /// Serializable data for a user configuration.
    /// Does not include AnvendelsesTekst - that's populated from default config on load.
    /// </summary>
    public class NyttetimerConfigurationData
    {
        public string Name { get; set; } = string.Empty;
        public List<NyttetimerEntryData> Entries { get; set; } = new();

        public NyttetimerConfigurationData() { }

        public NyttetimerConfigurationData(NyttetimerConfiguration config)
        {
            Name = config.Name;
            Entries = config.Entries.Select(e => new NyttetimerEntryData
            {
                AnvendelsesKode = e.AnvendelsesKode,
                Nyttetimer = e.Nyttetimer
                // AnvendelsesTekst is NOT saved
            }).ToList();
        }

        /// <summary>
        /// Converts to full configuration, populating texts from reference.
        /// </summary>
        public NyttetimerConfiguration ToConfiguration(NyttetimerConfiguration reference)
        {
            var config = new NyttetimerConfiguration(Name, false);
            foreach (var entry in Entries)
            {
                config.Entries.Add(new NyttetimerEntry(entry.AnvendelsesKode, entry.Nyttetimer));
            }
            config.PopulateTextsFrom(reference);
            return config;
        }
    }

    /// <summary>
    /// Serializable data for a single entry (without text).
    /// </summary>
    public class NyttetimerEntryData
    {
        public string AnvendelsesKode { get; set; } = string.Empty;
        public int Nyttetimer { get; set; }
    }
}

