using System;

namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// Static facade for accessing CSV data sources.
    /// Provides easy access to all CSV data with automatic configuration-based routing for versioned files.
    /// </summary>
    /// <remarks>
    /// Usage examples:
    /// <code>
    /// // Get distance for a type (non-versioned file)
    /// string? distance = Csv.Distances.Distance("FJV");
    /// 
    /// // Get layer from Krydsninger (versioned file - uses active configuration)
    /// string? layer = Csv.Krydsninger.Layer("04kV_kabel");
    /// 
    /// // Use alternate key column
    /// string? layer2 = Csv.Krydsninger.Layer("0-EL_04kV", Krydsninger.Columns.Layer);
    /// </code>
    /// </remarks>
    public static class Csv
    {
        private static readonly object _lock = new();

        // Non-versioned data sources (lazy-initialized)
        private static Distances? _distances;
        private static Dybde? _dybde;
        private static FjvDynamicComponents? _fjvDynamicComponents;
        private static Stier? _stier;

        // Versioned data sources (recreated when configuration changes)
        private static Krydsninger? _krydsninger;
        private static LagLer? _lagLer;
        private static string? _lastConfigForVersioned;

        // Additional data sources (lazy-initialized)
        private static InstOgBr? _instOgBr;
        private static AnvKoder? _anvKoder;
        private static EnhKoder? _enhKoder;

        static Csv()
        {
            // Subscribe to configuration changes to invalidate versioned data sources
            ConfigurationManager.ConfigurationChanged += OnConfigurationChanged;
        }

        private static void OnConfigurationChanged(object? sender, EventArgs e)
        {
            lock (_lock)
            {
                // Invalidate versioned data sources when configuration changes
                _krydsninger = null;
                _lagLer = null;
                _lastConfigForVersioned = null;
            }
        }

        #region Non-versioned data sources

        /// <summary>
        /// Gets the Distances data source.
        /// </summary>
        public static Distances Distances
        {
            get
            {
                if (_distances == null)
                {
                    lock (_lock)
                    {
                        _distances ??= new Distances();
                    }
                }
                return _distances;
            }
        }

        /// <summary>
        /// Gets the Dybde data source.
        /// </summary>
        public static Dybde Dybde
        {
            get
            {
                if (_dybde == null)
                {
                    lock (_lock)
                    {
                        _dybde ??= new Dybde();
                    }
                }
                return _dybde;
            }
        }

        /// <summary>
        /// Gets the FJV Dynamiske Komponenter data source.
        /// </summary>
        public static FjvDynamicComponents FjvDynamicComponents
        {
            get
            {
                if (_fjvDynamicComponents == null)
                {
                    lock (_lock)
                    {
                        _fjvDynamicComponents ??= new FjvDynamicComponents();
                    }
                }
                return _fjvDynamicComponents;
            }
        }

        /// <summary>
        /// Gets the Stier (project paths) data source.
        /// </summary>
        public static Stier Stier
        {
            get
            {
                if (_stier == null)
                {
                    lock (_lock)
                    {
                        _stier ??= new Stier();
                    }
                }
                return _stier;
            }
        }

        #endregion

        #region Versioned data sources

        /// <summary>
        /// Gets the Krydsninger data source for the active configuration.
        /// Throws InvalidOperationException if no configuration is set.
        /// </summary>
        public static Krydsninger Krydsninger
        {
            get
            {
                EnsureVersionedDataSources();
                return _krydsninger!;
            }
        }

        /// <summary>
        /// Gets the LagLer data source for the active configuration.
        /// Throws InvalidOperationException if no configuration is set.
        /// </summary>
        public static LagLer LagLer
        {
            get
            {
                EnsureVersionedDataSources();
                return _lagLer!;
            }
        }

        private static void EnsureVersionedDataSources()
        {
            // First ensure configuration is set
            ConfigurationManager.EnsureConfigurationSet();

            string? currentConfig = ConfigurationManager.ActiveConfiguration;

            lock (_lock)
            {
                // Check if we need to recreate versioned data sources
                if (_lastConfigForVersioned != currentConfig)
                {
                    _krydsninger = new Krydsninger();
                    _lagLer = new LagLer();
                    _lastConfigForVersioned = currentConfig;
                }
            }
        }

        #endregion

        #region Additional data sources (from register)

        /// <summary>
        /// Gets the InstOgBr (Installation og brændsel) data source.
        /// </summary>
        public static InstOgBr InstOgBr
        {
            get
            {
                if (_instOgBr == null)
                {
                    lock (_lock)
                    {
                        _instOgBr ??= new InstOgBr();
                    }
                }
                return _instOgBr;
            }
        }

        /// <summary>
        /// Gets the AnvKoder (BygAnvendelse) data source.
        /// </summary>
        public static AnvKoder AnvKoder
        {
            get
            {
                if (_anvKoder == null)
                {
                    lock (_lock)
                    {
                        _anvKoder ??= new AnvKoder();
                    }
                }
                return _anvKoder;
            }
        }

        /// <summary>
        /// Gets the EnhKoder (EnhAnvendelse) data source.
        /// </summary>
        public static EnhKoder EnhKoder
        {
            get
            {
                if (_enhKoder == null)
                {
                    lock (_lock)
                    {
                        _enhKoder ??= new EnhKoder();
                    }
                }
                return _enhKoder;
            }
        }

        #endregion

        #region Utility methods

        /// <summary>
        /// Invalidates all cached data sources, forcing a reload on next access.
        /// </summary>
        public static void InvalidateAll()
        {
            lock (_lock)
            {
                _distances?.Invalidate();
                _dybde?.Invalidate();
                _fjvDynamicComponents?.Invalidate();
                _stier?.Invalidate();
                _krydsninger?.Invalidate();
                _lagLer?.Invalidate();
                _instOgBr?.Invalidate();
                _anvKoder?.Invalidate();
                _enhKoder?.Invalidate();
            }
        }

        /// <summary>
        /// Checks if versioned data sources are available (configuration is set).
        /// </summary>
        public static bool IsVersionedDataAvailable => ConfigurationManager.IsConfigurationSet;

        #endregion
    }
}
