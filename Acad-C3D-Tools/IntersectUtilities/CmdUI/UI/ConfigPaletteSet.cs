using Autodesk.AutoCAD.Windows;

using System;

namespace IntersectUtilities.CmdUI.UI
{
    /// <summary>
    /// Central palette set hub that hosts multiple tabs from different parts of the repo.
    /// Renamed from ConfigPaletteSet to NsCmdPaletteSet to reflect its multi-tab nature.
    /// Tab 0: ConfigurationControl (CSV config)
    /// Tab 1: BatchProcessingControl (BPUIv2)
    /// </summary>
    internal class NsCmdPaletteSet : PaletteSet
    {
        private static NsCmdPaletteSet? _instance;

        /// <summary>
        /// Gets or creates the singleton instance of the palette set.
        /// </summary>
        public static NsCmdPaletteSet Instance => _instance ??= new NsCmdPaletteSet();

        /// <summary>
        /// Tracks whether the palette was visible before being hidden.
        /// </summary>
        public bool WasVisible { get; set; } = false;

        private NsCmdPaletteSet()
            : base("NS Command Center", "NSCMD", new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"))
        {
            Style =
                PaletteSetStyles.ShowAutoHideButton |
                PaletteSetStyles.ShowCloseButton |
                PaletteSetStyles.ShowPropertiesMenu;

            MinimumSize = new System.Drawing.Size(350, 300);
            Location = new System.Drawing.Point(100, 100);
            Size = new System.Drawing.Size(450, 600);

            // Tab 0: CSV Configuration
            AddVisual("CONFIG", new ConfigurationControl());

            // Tab 1: Batch Processing v2
            AddVisual("BATCH", new BatchProcessing.BPUIv2.UI.BatchProcessingControl());

            Activate(0);
        }

        /// <summary>
        /// Shows the palette set.
        /// </summary>
        public static void Show()
        {
            Instance.Visible = true;
            Instance.WasVisible = true;
        }

        /// <summary>
        /// Shows the palette set and activates a specific tab by index.
        /// </summary>
        public static void Show(int tabIndex)
        {
            Instance.Visible = true;
            Instance.WasVisible = true;
            if (tabIndex >= 0 && tabIndex < Instance.Count)
                Instance.Activate(tabIndex);
        }

        /// <summary>
        /// Resets the singleton instance (useful for cleanup).
        /// </summary>
        public static void Reset()
        {
            if (_instance != null)
            {
                _instance.Visible = false;
                _instance.Dispose();
                _instance = null;
            }
        }
    }

    // Keep the old name as an alias for backward compatibility in case
    // any existing code references ConfigPaletteSet directly.
    internal class ConfigPaletteSet
    {
        public static void Show() => NsCmdPaletteSet.Show();
        public static void Reset() => NsCmdPaletteSet.Reset();
    }
}
