using CacheStatsType = DimensioneringV2.ResultCache.CacheStatistics;

using System.Windows;

namespace DimensioneringV2.UI.CacheStatistics
{
    /// <summary>
    /// Static context for cache statistics.
    /// Used to share statistics between the cache and the UI.
    /// Manages a singleton window that is reused across calculations.
    /// </summary>
    public static class CacheStatisticsContext
    {
        /// <summary>
        /// The shared statistics tracker.
        /// </summary>
        public static CacheStatsType Statistics { get; } = new CacheStatsType();

        /// <summary>
        /// The view model for the statistics window.
        /// </summary>
        public static CacheStatisticsViewModel? VM { get; private set; }

        /// <summary>
        /// The statistics window instance (reused).
        /// </summary>
        private static CacheStatisticsWindow? _window;

        /// <summary>
        /// Enable debug mode for cache operations.
        /// Set to true BEFORE running calculations to capture all entries.
        /// Debug entries are dumped automatically when calculation completes.
        /// </summary>
        public static bool EnableDebugMode { get; set; } = false;

        /// <summary>
        /// Path where debug CSV will be saved. If null, uses desktop.
        /// </summary>
        public static string? DebugOutputPath { get; set; } = null;

        /// <summary>
        /// Gets or creates the statistics window, reusing existing one if open.
        /// Call this at the start of calculations.
        /// </summary>
        /// <returns>The view model for the window.</returns>
        public static CacheStatisticsViewModel EnsureWindowVisible()
        {
            // Check if window exists and is still open
            if (_window != null && _window.IsLoaded)
            {
                // Window exists, just reset and bring to front
                VM?.Reset();
                _window.Activate();
            }
            else
            {
                // Create new window and view model
                VM = new CacheStatisticsViewModel(Statistics);
                _window = new CacheStatisticsWindow(VM);
                
                // Handle window closing - don't dispose, just hide
                _window.Closing += (s, e) =>
                {
                    // Allow closing, but clear reference so next time creates new
                    _window = null;
                    VM = null;
                };
                
                _window.Show();
            }

            return VM!;
        }

        /// <summary>
        /// Closes the statistics window if open.
        /// </summary>
        public static void CloseWindow()
        {
            if (_window != null && _window.IsLoaded)
            {
                _window.Close();
            }
            _window = null;
            VM = null;
        }
    }
}
