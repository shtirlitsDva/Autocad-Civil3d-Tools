using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using NorsynHydraulicCalc;

namespace NorsynHydraulicCalcTestApp
{
    public static class DataUtils
    {
        public static List<RowData> ReadExcelFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("The file does not exist!", filePath);

            List<RowData> rowDataList = new List<RowData>();

            // Open the Excel file as a file stream
            using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                IWorkbook workbook;

                // Check if the file is .xlsx or .xls and initialize the correct workbook
                if (Path.GetExtension(filePath).Equals(".xlsx"))
                {
                    workbook = new XSSFWorkbook(file); // For .xlsx
                }
                else
                {
                    workbook = new HSSFWorkbook(file); // For .xls
                }

                // Get the first sheet from the workbook
                ISheet sheet = workbook.GetSheetAt(0);

                // Read the header row (first row, index 0)
                IRow headerRow = sheet.GetRow(0);
                Dictionary<string, int> columnMapping = new Dictionary<string, int>();

                // Map column names to their respective index
                for (int col = 0; col < headerRow.LastCellNum; col++)
                {
                    string columnName = headerRow.GetCell(col).ToString().Trim();
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        columnMapping[columnName] = col;
                    }
                }

                // Get all public properties of the RowData class
                PropertyInfo[] properties = typeof(RowData).GetProperties();

                // Loop through the remaining rows
                for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    IRow currentRow = sheet.GetRow(rowIndex);
                    if (currentRow != null)
                    {
                        RowData rowData = new RowData();

                        foreach (var property in properties)
                        {
                            // Check if the Excel has a column matching the property name
                            if (columnMapping.TryGetValue(property.Name, out int columnIndex))
                            {
                                ICell cell = currentRow.GetCell(columnIndex);

                                if (cell != null)
                                {
                                    // Convert the cell value based on the property type
                                    if (property.PropertyType == typeof(int))
                                    {
                                        property.SetValue(rowData, (int)cell.NumericCellValue);
                                    }
                                    else if (property.PropertyType == typeof(double))
                                    {
                                        property.SetValue(rowData, cell.NumericCellValue);
                                    }
                                    else if (property.PropertyType == typeof(string))
                                    {
                                        property.SetValue(rowData, cell.ToString());
                                    }
                                    else if (property.PropertyType.IsEnum)
                                    {
                                        string enumValue = cell.ToString().Replace(" ", "");
                                        property.SetValue(rowData, Enum.Parse(property.PropertyType, enumValue));
                                    }
                                }
                            }
                        }

                        // Add the populated object to the list
                        rowDataList.Add(rowData);
                    }
                }
            }

            return rowDataList;
        }
        public static void WriteToExcelFile(string filePath, List<RowData> rowDataList)
        {
            // Create a new workbook and a sheet
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Sheet1");

            // Create the header row
            IRow headerRow = sheet.CreateRow(0);

            // Define headers based on RowData properties
            string[] headers = 
            {
                "Address", "NumberOfUnits", "NumberOfBuildings", "Segment",
                "HeatingDemand", "Length", "ReynoldsFrem", "ReynoldsRetur",
                "FlowFrem", "FlowRetur", "GradientFrem", "GradientRetur",
                "Dimension", "FrictionLossFrem", "FrictionLossRetur"
            };

            // Write the header row
            for (int i = 0; i < headers.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(headers[i]);
            }

            // Write the data rows
            for (int rowIndex = 0; rowIndex < rowDataList.Count; rowIndex++)
            {
                RowData data = rowDataList[rowIndex];
                IRow row = sheet.CreateRow(rowIndex + 1);

                // Write each property into its respective column
                row.CreateCell(0).SetCellValue(data.Address ?? string.Empty);
                row.CreateCell(1).SetCellValue(data.NumberOfUnits);
                row.CreateCell(2).SetCellValue(data.NumberOfBuildings);
                row.CreateCell(3).SetCellValue(data.Segment.ToString());
                row.CreateCell(4).SetCellValue(data.HeatingDemand);
                row.CreateCell(5).SetCellValue(data.Length);
                row.CreateCell(6).SetCellValue(data.ReynoldsFrem);
                row.CreateCell(7).SetCellValue(data.ReynoldsRetur);
                row.CreateCell(8).SetCellValue(data.FlowFrem);
                row.CreateCell(9).SetCellValue(data.FlowRetur);
                row.CreateCell(10).SetCellValue(data.GradientFrem);
                row.CreateCell(11).SetCellValue(data.GradientRetur);
                row.CreateCell(12).SetCellValue(data.Dimension ?? string.Empty);
                row.CreateCell(13).SetCellValue(data.FrictionLossFrem);
                row.CreateCell(14).SetCellValue(data.FrictionLossRetur);
            }

            // Save the workbook to the specified file path
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fileStream);
            }
        }
        public static void WriteToExcelFileAuto(string filePath, List<RowData> rowDataList)
        {
            // Create a new workbook and a sheet
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Sheet1");

            // Use reflection to get all public properties of the RowData class
            var properties = typeof(RowData).GetProperties();

            // Create the header row
            IRow headerRow = sheet.CreateRow(0);

            // Write the header row using property names
            for (int i = 0; i < properties.Length; i++)
            {
                headerRow.CreateCell(i).SetCellValue(properties[i].Name);
            }

            // Write the data rows
            for (int rowIndex = 0; rowIndex < rowDataList.Count; rowIndex++)
            {
                RowData data = rowDataList[rowIndex];
                IRow row = sheet.CreateRow(rowIndex + 1);

                // Write each property value into its respective column
                for (int colIndex = 0; colIndex < properties.Length; colIndex++)
                {
                    var propertyValue = properties[colIndex].GetValue(data);

                    if (propertyValue != null)
                    {
                        if (propertyValue is string stringValue)
                        {
                            row.CreateCell(colIndex).SetCellValue(stringValue);
                        }
                        else if (propertyValue is int intValue)
                        {
                            row.CreateCell(colIndex).SetCellValue(intValue);
                        }
                        else if (propertyValue is double doubleValue)
                        {
                            row.CreateCell(colIndex).SetCellValue(doubleValue);
                        }
                        else if (propertyValue is float floatValue)
                        {
                            row.CreateCell(colIndex).SetCellValue((double)floatValue);
                        }
                        else if (propertyValue is bool boolValue)
                        {
                            row.CreateCell(colIndex).SetCellValue(boolValue ? "True" : "False");
                        }
                        else if (propertyValue is Enum enumValue)
                        {
                            row.CreateCell(colIndex).SetCellValue(enumValue.ToString());
                        }
                        else
                        {
                            row.CreateCell(colIndex).SetCellValue(propertyValue.ToString());
                        }
                    }
                    else
                    {
                        row.CreateCell(colIndex).SetCellValue(string.Empty);
                    }
                }
            }

            // Save the workbook to the specified file path
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fileStream);
            }
        }
    }
}