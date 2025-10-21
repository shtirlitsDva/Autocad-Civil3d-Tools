using NTRExport.Excel;
using NTRExport.Ntr;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Globalization;
using IntersectUtilities;

namespace NTRExport.NtrConfiguration
{
    internal class ConfigurationData
    {
        private static readonly string ExcelPath = @"X:\AC - NTR\NTR_CONFIG.xlsx";
        internal List<NtrLast> Last { get; }
        internal NtrLast SupplyLast { get; private set; }
        internal NtrLast ReturnLast { get; private set; }
        internal DataTable Pipelines { get; }        
        internal DataTable Profiles { get; }
        

        internal ConfigurationData()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            DataSet dataSet = ExcelReader.ReadExcelToDataSet(ExcelPath, false);
            dataSet = ExcelReader.ConvertToDataSetOfStrings(dataSet);

            DataTableCollection dataTableCollection = dataSet.Tables;

            //Read LAST records here
            DataTable lastTable = ReadDataTable(dataTableCollection, "LAST");
            Last = lastTable != null ? MapPairedHeaderValueRowsTo<NtrLast>(lastTable) : new List<NtrLast>();
            Last = Last.Where(x => x.Name.StartsWith("FJV")).ToList();

            var chosenSupplyLast =
                TGridFormCaller.Call(
                    Last.Where(x => x.Name.Contains("FREM")),
                    x => x.Name,
                    "Select LAST for *SUPPLY*: ");
            if (chosenSupplyLast == null) throw new Exception("Cancelled!");
            SupplyLast = chosenSupplyLast;
            
            var chosenReturnLast =
                TGridFormCaller.Call(
                    Last.Where(x => x.Name.Contains("RETUR")),
                    x => x.Name,
                    "Select LAST for *RETURN*: ");
            if (chosenReturnLast == null) throw new Exception("Cancelled!");
            ReturnLast = chosenReturnLast;

            // Assign other known sheets if present
            Pipelines = ReadDataTable(dataTableCollection, "PIPELINES");
            Profiles = ReadDataTable(dataTableCollection, "PROFILES");
        }

        internal static DataTable ReadDataTable(DataTableCollection dataTableCollection, string tableName)
        {
            return (from DataTable dtbl in dataTableCollection where dtbl.TableName == tableName select dtbl).FirstOrDefault();
        }

        /// <summary>
        /// Convert a worksheet arranged as pairs of rows (header row then value row)
        /// into a list of instances of T. Constructor parameter names are matched
        /// to headers case-insensitively; if no matching ctor is found, writable
        /// properties are set as a fallback.
        /// </summary>
        private static List<T> MapPairedHeaderValueRowsTo<T>(DataTable table) where T : class
        {
            var results = new List<T>();
            if (table == null || table.Rows.Count == 0)
            {
                return results;
            }

            for (int r = 0; r + 1 < table.Rows.Count; r += 2)
            {
                DataRow headerRow = table.Rows[r];
                DataRow valueRow = table.Rows[r + 1];

                var headerToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    string header = headerRow[c]?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        continue;
                    }
                    string value = valueRow[c]?.ToString()?.Trim() ?? string.Empty;
                    headerToValue[header] = value;
                }

                ConstructorInfo ctor = typeof(T)
                    .GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    .OrderByDescending(ci => ci.GetParameters().Length)
                    .FirstOrDefault(ci => ci.GetParameters().All(p => headerToValue.ContainsKey(p.Name)));

                if (ctor != null)
                {
                    object[] args = ctor.GetParameters()
                        .Select(p => ConvertStringToType(headerToValue[p.Name], p.ParameterType))
                        .ToArray();
                    results.Add((T)ctor.Invoke(args));
                    continue;
                }

                var instance = Activator.CreateInstance<T>();
                foreach (PropertyInfo prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite)
                    {
                        continue;
                    }
                    if (!headerToValue.TryGetValue(prop.Name, out string strValue))
                    {
                        continue;
                    }
                    object converted = ConvertStringToType(strValue, prop.PropertyType);
                    prop.SetValue(instance, converted);
                }
                results.Add(instance);
            }

            return results;
        }

        private static object ConvertStringToType(string input, Type targetType)
        {
            Type effective = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (effective == typeof(string))
            {
                return input;
            }
            if (string.IsNullOrWhiteSpace(input))
            {
                return effective.IsValueType ? Activator.CreateInstance(effective) : null;
            }
            try
            {
                return Convert.ChangeType(input, effective, CultureInfo.InvariantCulture);
            }
            catch
            {
                return effective.IsValueType ? Activator.CreateInstance(effective) : null;
            }
        }
    }
}
