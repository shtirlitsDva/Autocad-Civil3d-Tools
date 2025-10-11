using ExcelDataReader;

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Excel
{
    internal class ExcelReader
    {
        public static DataSet ReadExcelToDataSet(string fileName, bool dataHasHeaders = true)
        {
            DataSet dataSet;
            using (var stream = File.Open(fileName, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                {
                    ConfigureDataTable = (tableReader) => new ExcelDataTableConfiguration()
                    {
                        // Gets or sets a value indicating the prefix of generated column names.
                        EmptyColumnNamePrefix = "Column",

                        // Gets or sets a value indicating whether to use a row from the 
                        // data as column names.
                        UseHeaderRow = dataHasHeaders,

                        // Gets or sets a callback to determine which row is the header row. 
                        // Only called when UseHeaderRow = true.
                        ReadHeaderRow = (rowReader) =>
                        {
                            // F.ex skip the first row and use the 2nd row as column headers:
                            // rowReader.Read();
                        },

                        // Gets or sets a callback to determine whether to include the 
                        // current row in the DataTable.
                        //FilterRow = (rowReader) => {
                        //    return true;
                        //},

                        // Gets or sets a callback to determine whether to include the specific
                        // column in the DataTable. Called once per column after reading the 
                        // headers.
                        //FilterColumn = (rowReader, columnIndex) => {
                        //    return true;
                        //}
                    }
                });
            }
            return dataSet;
        }

        public static DataSet ConvertToDataSetOfStrings(DataSet sourceDataSet)
        {
            var result = new DataSet();
            result.Tables.AddRange(
                sourceDataSet.Tables.Cast<DataTable>().Select(srcDataTable =>
                {
                    var destDataTable = new DataTable(srcDataTable.TableName, srcDataTable.Namespace);
                    // Copy each source column as System.String...
                    destDataTable.Columns.AddRange(
                        srcDataTable.Columns.Cast<DataColumn>()
                            .Select(col => new DataColumn(col.ColumnName, typeof(String)))
                            .ToArray()
                            );
                    // Implicitly convert all source cells to System.String using DataTable.ImportRow()
                    srcDataTable.Rows.OfType<DataRow>()
                    .ToList()
                    .ForEach(row => destDataTable.ImportRow(row));
                    return destDataTable;
                })
                .ToArray()
                );
            return result;
        }
    }
}
