using IntersectUtilities.UtilsCommon.DataManager.CsvData;

namespace ExportShapeFilesEasyGis
{
    public static class Utils
    {
        /// <summary>
        /// Gets the FJV Dynamic Components data source.
        /// Lazy-loaded and cached by the DataManager infrastructure.
        /// </summary>
        public static FjvDynamicComponents GetFjvComponents() => Csv.FjvDynamicComponents;
    }
}
