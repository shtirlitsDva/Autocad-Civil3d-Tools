using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Microsoft.VisualBasic.FileIO;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.UtilsCommon.DataManager.CsvData
{
    public static partial class CsvRegistry
    {
        /// <summary>
        /// The base path where CSV files are located.
        /// </summary>
        public const string ConfPath = @"X:\AutoCAD DRI - 01 Civil 3D\Conf";

        /// <summary>
        /// Gets the file path for a non-versioned CSV file.
        /// </summary>
        /// <param name="fileName">The CSV file name (e.g., "Distances.csv")</param>
        /// <returns>The full path to the file.</returns>
        public static string GetFilePath(string fileName)
        {
            return Path.Combine(ConfPath, fileName);
        }

    }
}
