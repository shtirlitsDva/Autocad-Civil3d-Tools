using Autodesk.AutoCAD.Windows;

using System;

namespace IntersectUtilities.CmdUI.UI
{
    /// <summary>
    /// Palette set for CSV Configuration UI.
    /// </summary>
    internal class ConfigPaletteSet : PaletteSet
    {
        private static ConfigPaletteSet? _instance;
        
        /// <summary>
        /// Gets or creates the singleton instance of the palette set.
        /// </summary>
        public static ConfigPaletteSet Instance => _instance ??= new ConfigPaletteSet();

        /// <summary>
        /// Tracks whether the palette was visible before being hidden.
        /// </summary>
        public bool WasVisible { get; set; } = false;

        private ConfigPaletteSet() 
            : base("CSV Config", "IUCFG", new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"))
        {
            Style =
                PaletteSetStyles.ShowAutoHideButton |
                PaletteSetStyles.ShowCloseButton |
                PaletteSetStyles.ShowPropertiesMenu;
            
            MinimumSize = new System.Drawing.Size(300, 250);
            Location = new System.Drawing.Point(100, 100);
            Size = new System.Drawing.Size(420, 400);

            AddVisual("CONFIG", new ConfigurationControl());
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
}
