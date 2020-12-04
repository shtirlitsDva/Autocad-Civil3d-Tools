using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using System.Data;

namespace IntersectUtilities
{
    internal static class CsvReader
    {
        internal static DataTable ReadCsvToDataTable(string path, string dataTableName)
        {
            using (TextFieldParser csvParser = new TextFieldParser(path))
            {
                csvParser.CommentTokens = new string[] { "#" };
                csvParser.SetDelimiters(new string[] { ";" });
                csvParser.HasFieldsEnclosedInQuotes = false;

                string[] colNames = new string[0];
                string[] fields = new string[0];

                DataTable dt = new DataTable(dataTableName);

                int counter = 0;
                while (!csvParser.EndOfData)
                {
                    if (counter == 0)
                    {
                        colNames = csvParser.ReadFields();
                        counter++;

                        for (int i = 0; i < colNames.Length; i++)
                        {

                            DataColumn dc = new DataColumn(colNames[i]);
                            dc.DataType = typeof(string);
                            dt.Columns.Add(dc);
                        }
                    }
                    else
                    {// Read current line fields, pointer moves to the next line.
                        fields = csvParser.ReadFields();
                        DataRow dr = dt.NewRow();

                        for (int i = 0; i < colNames.Length; i++)
                        {
                            dr[i] = fields[i];
                        }
                        dt.Rows.Add(dr);
                        counter++;
                    }
                }
                return dt;
            }
        }
    }
}
