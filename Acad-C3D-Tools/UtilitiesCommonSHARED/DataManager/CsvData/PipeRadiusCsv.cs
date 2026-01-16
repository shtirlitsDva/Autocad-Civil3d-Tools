using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// Dynamic CSV loader for pipe radius data from the Radier directory.
    /// Unlike other CsvDataSource implementations, this takes the file path at construction time
    /// to support loading different company CSVs (Logstor.csv, Isoplus.csv, etc.).
    /// </summary>
    public sealed class PipeRadiusCsv : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            DN = 0,
            PipeType = 1,
            PipeLength = 2,
            BRpmin = 3,
            ERpmin = 4
        }

        private static readonly string[] _columnNames =
        {
            "DN", "PipeType", "PipeLength", "BRpmin", "ERpmin"
        };

        private readonly string _filePath;

        /// <summary>
        /// Creates a new PipeRadiusCsv loader for the specified file.
        /// </summary>
        /// <param name="filePath">Full path to the CSV file (e.g., "X:\...\Radier\Isoplus.csv").</param>
        public PipeRadiusCsv(string filePath)
        {
            _filePath = filePath;
        }

        protected override string FilePath => _filePath;
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Static cache for loaded CSV files to avoid reloading.
        /// </summary>
        private static readonly Dictionary<string, PipeRadiusCsv> _cache = new();
        private static readonly object _cacheLock = new();

        /// <summary>
        /// Gets a cached instance for the specified file path.
        /// </summary>
        public static PipeRadiusCsv GetOrLoad(string filePath)
        {
            lock (_cacheLock)
            {
                if (!_cache.TryGetValue(filePath, out var instance))
                {
                    instance = new PipeRadiusCsv(filePath);
                    _cache[filePath] = instance;
                }
                return instance;
            }
        }

        /// <summary>
        /// Clears the cache, forcing reload on next access.
        /// </summary>
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
            }
        }
    }
}
