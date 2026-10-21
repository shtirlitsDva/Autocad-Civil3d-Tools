using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    /// <summary>
    /// Dynamic CSV loader for pipe schedule data from the Schedule directory.
    /// Unlike other CsvDataSource implementations, this takes the file path at construction time
    /// to support loading different pipe type CSVs (DN.csv, ALUPEX.csv, etc.).
    /// </summary>
    public sealed class PipeScheduleCsv : CsvDataSource
    {
        /// <summary>
        /// Column indices for type-safe access.
        /// </summary>
        public enum Columns
        {
            DN = 0,
            PipeType = 1,
            PipeSeries = 2,
            pOd = 3,
            pThk = 4,
            kOd = 5,
            kThk = 6,
            pDst = 7,
            tWdth = 8,
            minElasticRadii = 9,
            VertFactor = 10,
            color = 11,
            DefaultL = 12,
            OffsetUnder7_5 = 13
        }

        private static readonly string[] _columnNames =
        {
            "DN", "PipeType", "PipeSeries", "pOd", "pThk", "kOd", "kThk",
            "pDst", "tWdth", "minElasticRadii", "VertFactor", "color", "DefaultL", "OffsetUnder7_5"
        };

        private readonly string _filePath;

        /// <summary>
        /// Creates a new PipeScheduleCsv loader for the specified file.
        /// </summary>
        /// <param name="filePath">Full path to the CSV file (e.g., "X:\...\Schedule\DN.csv").</param>
        public PipeScheduleCsv(string filePath)
        {
            _filePath = filePath;
        }

        protected override string FilePath => _filePath;
        protected override string[] ColumnNames => _columnNames;

        /// <summary>
        /// Static cache for loaded CSV files to avoid reloading.
        /// </summary>
        private static readonly Dictionary<string, PipeScheduleCsv> _cache = new();
        private static readonly object _cacheLock = new();

        /// <summary>
        /// Gets a cached instance for the specified file path.
        /// </summary>
        public static PipeScheduleCsv GetOrLoad(string filePath)
        {
            lock (_cacheLock)
            {
                if (!_cache.TryGetValue(filePath, out var instance))
                {
                    instance = new PipeScheduleCsv(filePath);
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
