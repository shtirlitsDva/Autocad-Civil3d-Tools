using CommunityToolkit.Mvvm.ComponentModel;

namespace DimensioneringV2.Models.Nyttetimer
{
    /// <summary>
    /// Represents a single entry mapping an AnvendelsesKode to its Nyttetimer value.
    /// </summary>
    public partial class NyttetimerEntry : ObservableObject
    {
        /// <summary>
        /// The building usage code (BBR code).
        /// </summary>
        [ObservableProperty]
        private string anvendelsesKode = string.Empty;

        /// <summary>
        /// The usage hours value for this building type.
        /// </summary>
        [ObservableProperty]
        private int nyttetimer;

        /// <summary>
        /// Optional description text for this building type.
        /// Read from default config, not persisted in user configs.
        /// </summary>
        [ObservableProperty]
        private string? anvendelsesTekst;

        public NyttetimerEntry() { }

        public NyttetimerEntry(string anvendelsesKode, int nyttetimer, string? anvendelsesTekst = null)
        {
            AnvendelsesKode = anvendelsesKode;
            Nyttetimer = nyttetimer;
            AnvendelsesTekst = anvendelsesTekst;
        }

        /// <summary>
        /// Creates a copy of this entry.
        /// </summary>
        public NyttetimerEntry Clone() => new(AnvendelsesKode, Nyttetimer, AnvendelsesTekst);
    }
}

