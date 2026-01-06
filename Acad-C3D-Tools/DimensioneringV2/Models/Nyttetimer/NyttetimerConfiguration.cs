using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace DimensioneringV2.Models.Nyttetimer
{
    /// <summary>
    /// A named configuration containing Nyttetimer values for different AnvendelsesKoder.
    /// </summary>
    public partial class NyttetimerConfiguration : ObservableObject
    {
        public const string DefaultConfigurationName = "Standard";

        /// <summary>
        /// The name of this configuration.
        /// </summary>
        [ObservableProperty]
        private string name = string.Empty;

        /// <summary>
        /// Whether this is the default (read-only) configuration.
        /// </summary>
        [ObservableProperty]
        private bool isDefault;

        /// <summary>
        /// The entries in this configuration.
        /// </summary>
        public ObservableCollection<NyttetimerEntry> Entries { get; set; } = new();

        public NyttetimerConfiguration() { }

        public NyttetimerConfiguration(string name, bool isDefault = false)
        {
            Name = name;
            IsDefault = isDefault;
        }

        /// <summary>
        /// Gets the Nyttetimer value for a given AnvendelsesKode.
        /// Returns null if the code is not found.
        /// </summary>
        public int? GetNyttetimer(string? anvendelsesKode)
        {
            if (string.IsNullOrWhiteSpace(anvendelsesKode))
                return null;

            var entry = Entries.FirstOrDefault(e => 
                e.AnvendelsesKode.Equals(anvendelsesKode, StringComparison.OrdinalIgnoreCase));
            
            return entry?.Nyttetimer;
        }

        /// <summary>
        /// Creates a deep copy of this configuration with a new name.
        /// </summary>
        public NyttetimerConfiguration Clone(string newName)
        {
            var clone = new NyttetimerConfiguration(newName, false);
            foreach (var entry in Entries)
            {
                clone.Entries.Add(entry.Clone());
            }
            return clone;
        }

        /// <summary>
        /// Populates the AnvendelsesTekst from a reference configuration (typically default).
        /// This is called after loading user configs that don't store the text column.
        /// </summary>
        public void PopulateTextsFrom(NyttetimerConfiguration reference)
        {
            var lookup = reference.Entries.ToDictionary(e => e.AnvendelsesKode, e => e.AnvendelsesTekst);
            
            foreach (var entry in Entries)
            {
                if (lookup.TryGetValue(entry.AnvendelsesKode, out var text))
                {
                    entry.AnvendelsesTekst = text;
                }
                // If not found, text remains null - handled gracefully
            }
        }
    }
}

