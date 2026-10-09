using System;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    public static partial class Csv
    {
        private static readonly object _lock = new();

        // Non-versioned data sources (lazy-initialized)
        private static FjvDynamicComponents? _fjvDynamicComponents;

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

    }
}
