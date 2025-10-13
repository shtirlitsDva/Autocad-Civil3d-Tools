using NTRExport.Excel;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.NtrConfiguration
{
    public class ConfigurationData
    {
        private static readonly string ExcelPath = @"X:\AC - NTR\NTR_CONFIG.xlsx";
        public StringBuilder _01_GEN { get; }
        public StringBuilder _02_AUFT { get; }
        public StringBuilder _03_TEXT { get; }
        public StringBuilder _04_LAST { get; }
        public StringBuilder _05_DN { get; }
        public StringBuilder _06_ISO { get; }
        public DataTable Pipelines { get; }
        //public DataTable Elements { get; }
        //public DataTable Supports { get; }
        public DataTable Profiles { get; }
        //public DataTable Flexjoints { get; }

        public ConfigurationData()
        {
            DataSet dataSet = ExcelReader.ReadExcelToDataSet(ExcelPath, false);
            dataSet = ExcelReader.ConvertToDataSetOfStrings(dataSet);

            DataTableCollection dataTableCollection = dataSet.Tables;

            _01_GEN = ReadNtrConfigurationData(dataTableCollection, "GEN", "C General settings");
            _02_AUFT = ReadNtrConfigurationData(dataTableCollection, "AUFT", "C Project description");
            _03_TEXT = ReadNtrConfigurationData(dataTableCollection, "TEXT", "C User text");
            _04_LAST = ReadNtrConfigurationData(dataTableCollection, "LAST", "C Loads definition");
            _05_DN = ReadNtrConfigurationData(dataTableCollection, "DN", "C Definition of pipe dimensions");
            _06_ISO = ReadNtrConfigurationData(dataTableCollection, "IS", "C Definition of insulation type");

            DataSet dataSetWithHeaders = ExcelReader.ReadExcelToDataSet(ExcelPath, true);
            dataSetWithHeaders = ExcelReader.ConvertToDataSetOfStrings(dataSetWithHeaders);

            Pipelines = ReadDataTable(dataSetWithHeaders.Tables, "PIPELINES");
            //Elements = ReadDataTable(dataSetWithHeaders.Tables, "ELEMENTS");
            //Supports = ReadDataTable(dataSetWithHeaders.Tables, "SUPPORTS");
            Profiles = ReadDataTable(dataSetWithHeaders.Tables, "PROFILES");
            //Flexjoints = ReadDataTable(dataSetWithHeaders.Tables, "FLEXJOINTS");

            //http://stackoverflow.com/questions/10855/linq-query-on-a-datatable?rq=1
        }

        /// <summary>
        /// Selects a DataTable by name and creates a StringBuilder output to NTR format based on the data in table.
        /// </summary>
        /// <param name="dataTableCollection">A collection of datatables.</param>
        /// <param name="tableName">The name of the DataTable to process.</param>
        /// <returns>StringBuilder containing the output NTR data.</returns>
        private static StringBuilder ReadNtrConfigurationData(DataTableCollection dataTableCollection, string tableName, string description)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(description);

            var table = ReadDataTable(dataTableCollection, tableName);
            if (table == null)
            {
                sb.AppendLine("C " + tableName + " does not exist!");
                return sb;
            }

            int numberOfRows = table.Rows.Count;
            if (numberOfRows % 2 != 0)
            {
                sb.AppendLine("C " + tableName + " is malformed, contains odd number of rows, must be even");
                return sb;
            }

            for (int i = 0; i < numberOfRows / 2; i++)
            {
                DataRow headerRow = table.Rows[i * 2];
                DataRow dataRow = table.Rows[i * 2 + 1];
                if (headerRow == null || dataRow == null)
                    throw new NullReferenceException(
                        tableName + " does not have two rows, check EXCEL configuration sheet!");

                sb.Append(tableName);

                for (int j = 0; j < headerRow.ItemArray.Length; j++)
                {
                    sb.Append(' ');
                    sb.Append(headerRow.Field<string>(j));
                    sb.Append('=');
                    sb.Append(dataRow.Field<string>(j));
                }

                sb.AppendLine();
            }

            return sb;
        }

        public static DataTable ReadDataTable(DataTableCollection dataTableCollection, string tableName)
        {
            return (from DataTable dtbl in dataTableCollection where dtbl.TableName == tableName select dtbl).FirstOrDefault();
        }
    }
}
