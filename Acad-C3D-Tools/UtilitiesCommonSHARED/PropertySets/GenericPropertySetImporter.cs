using Autodesk.Aec.DatabaseServices;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using MoreLinq;

using Microsoft.VisualBasic.FileIO;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities
{
    public class CsvTypedDataTable
    {
        private readonly List<Dictionary<string, object?>> _rows = new();
        private readonly List<string> _columnNames = new();
        private readonly Dictionary<string, PsDataType> _columnDataTypes =
            new(StringComparer.OrdinalIgnoreCase);

        public string FilePath { get; }
        public string TableName { get; }
        public IReadOnlyList<string> ColumnNames => _columnNames;
        public IReadOnlyDictionary<string, PsDataType> ColumnDataTypes => _columnDataTypes;
        public IReadOnlyList<Dictionary<string, object?>> Rows => _rows;
        public int RowCount => _rows.Count;

        public CsvTypedDataTable(string csvFilePath)
        {
            if (string.IsNullOrWhiteSpace(csvFilePath))
                throw new ArgumentException("CSV file path cannot be null or empty.", nameof(csvFilePath));
            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException($"CSV file not found: {csvFilePath}");

            FilePath = csvFilePath;
            TableName = Path.GetFileNameWithoutExtension(csvFilePath);

            LoadCsvFile();
        }

        public bool HasColumn(string columnName) => _columnDataTypes.ContainsKey(columnName);

        private void LoadCsvFile()
        {
            using TextFieldParser csvParser = new TextFieldParser(FilePath);
            csvParser.CommentTokens = new string[] { "#" };
            csvParser.SetDelimiters(new string[] { ";" });
            csvParser.HasFieldsEnclosedInQuotes = false;

            if (csvParser.EndOfData)
                throw new InvalidDataException("CSV file is empty or missing header row.");

            string[]? columnNames = csvParser.ReadFields();
            if (columnNames == null || columnNames.Length == 0)
                throw new InvalidDataException("CSV file is missing column header row.");
            _columnNames.AddRange(columnNames);

            if (csvParser.EndOfData)
                throw new InvalidDataException("CSV file is missing data type header row.");

            string[]? dataTypes = csvParser.ReadFields();
            if (dataTypes == null || dataTypes.Length != _columnNames.Count)
                throw new InvalidDataException(
                    $"Column count mismatch: {_columnNames.Count} column names but {dataTypes?.Length ?? 0} data types.");

            // Validate that the second row contains valid data type declarations
            var invalidTypes = dataTypes
                .Select((dt, idx) => (Type: dt, Column: _columnNames[idx]))
                .Where(x => !IsValidCsvType(x.Type))
                .ToList();

            if (invalidTypes.Count > 0)
            {
                string invalidExamples = string.Join(", ",
                    invalidTypes.Take(3).Select(x => $"'{x.Type}' (column '{x.Column}')"));
                if (invalidTypes.Count > 3)
                    invalidExamples += $", ... ({invalidTypes.Count - 3} more)";

                throw new InvalidDataException(
                    $"CSV file is missing the data types row (row 2).\n" +
                    $"The second row appears to contain data values instead of type declarations.\n" +
                    $"Invalid type values found: {invalidExamples}\n" +
                    $"Valid types are: string, double, int, integer, bool, boolean, date.\n" +
                    $"Example CSV format:\n" +
                    $"  Row 1: ColumnA;ColumnB;ColumnC\n" +
                    $"  Row 2: string;double;int\n" +
                    $"  Row 3+: actual;data;values");
            }

            for (int i = 0; i < _columnNames.Count; i++)
            {
                string columnName = _columnNames[i];
                PsDataType psDataType = MapCsvTypeToPropertySetType(dataTypes[i]);
                _columnDataTypes[columnName] = psDataType;
            }

            while (!csvParser.EndOfData)
            {
                string[]? fields = csvParser.ReadFields();
                if (fields == null || fields.Length == 0)
                    continue;

                Dictionary<string, object?> row = new(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _columnNames.Count; i++)
                {
                    string columnName = _columnNames[i];
                    string fieldValue = i < fields.Length ? fields[i] : string.Empty;
                    object? parsedValue = ParseFieldValue(fieldValue, _columnDataTypes[columnName]);
                    row[columnName] = parsedValue;
                }
                _rows.Add(row);
            }
        }

        private static readonly HashSet<string> ValidCsvTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "string", "double", "int", "integer", "bool", "boolean", "date"
        };

        private static bool IsValidCsvType(string csvType)
        {
            return ValidCsvTypes.Contains(csvType.Trim());
        }

        private static PsDataType MapCsvTypeToPropertySetType(string csvType)
        {
            switch (csvType.ToLowerInvariant().Trim())
            {
                case "string":
                    return PsDataType.Text;
                case "double":
                    return PsDataType.Real;
                case "int":
                case "integer":
                    return PsDataType.Integer;
                case "bool":
                case "boolean":
                    return PsDataType.TrueFalse;
                case "date":
                    return PsDataType.Text;
                default:
                    prdDbg($"Warning: Unknown CSV data type '{csvType}', defaulting to Text.");
                    return PsDataType.Text;
            }
        }

        private static object? ParseFieldValue(string fieldValue, PsDataType targetType)
        {
            if (string.IsNullOrWhiteSpace(fieldValue))
            {
                return null;
            }

            try
            {
                switch (targetType)
                {
                    case PsDataType.Text:
                        return fieldValue;
                    case PsDataType.Real:
                        if (double.TryParse(fieldValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblValue))
                            return dblValue;
                        return null;
                    case PsDataType.Integer:
                        if (int.TryParse(fieldValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                            return intValue;
                        return null;
                    case PsDataType.TrueFalse:
                        if (bool.TryParse(fieldValue, out bool boolValue))
                            return boolValue;
                        if (fieldValue.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                            fieldValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (fieldValue.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                            fieldValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                            return false;
                        return null;
                    default:
                        return fieldValue;
                }
            }
            catch (System.Exception ex)
            {
                prdDbg($"Error parsing field value '{fieldValue}' as {targetType}: {ex.Message}");
                return null;
            }
        }
    }

    public class GenericPropertySetImporter
    {
        private readonly Database _database;
        private readonly CsvTypedDataTable _dataTable;
        private readonly string _propertySetName;
        private readonly DictionaryPropertySetDefinitions _dictionaryPropertySetDefinitions;
        private PropertySetDefinition _propertySetDefinition = null!;
        private int _currentRowIndex;
        private readonly string _xColumnName;
        private readonly string _yColumnName;

        public bool HasMoreRows => _currentRowIndex < _dataTable.RowCount;

        public GenericPropertySetImporter(
            Database database,
            string csvFilePath,
            string xColumnName,
            string yColumnName)
            : this(database, new CsvTypedDataTable(csvFilePath), xColumnName, yColumnName)
        { }

        public GenericPropertySetImporter(
            Database database,
            CsvTypedDataTable dataTable,
            string xColumnName,
            string yColumnName)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _dataTable = dataTable ?? throw new ArgumentNullException(nameof(dataTable));
            _xColumnName = xColumnName ?? throw new ArgumentNullException(nameof(xColumnName));
            _yColumnName = yColumnName ?? throw new ArgumentNullException(nameof(yColumnName));

            if (!_dataTable.HasColumn(_xColumnName))
                throw new ArgumentException($"X coordinate column '{_xColumnName}' not found in CSV file.");
            if (!_dataTable.HasColumn(_yColumnName))
                throw new ArgumentException($"Y coordinate column '{_yColumnName}' not found in CSV file.");

            _propertySetName = _dataTable.TableName;
            _dictionaryPropertySetDefinitions = new DictionaryPropertySetDefinitions(_database);
            _currentRowIndex = 0;

            CreatePropertySetDefinition();
        }

        private void CreatePropertySetDefinition()
        {
            // Check if PropertySetDefinition already exists
            // We need a transaction to check, so create one if needed
            Transaction checkTx = _database.TransactionManager.TopTransaction;
            bool ownsTransaction = checkTx == null;

            if (ownsTransaction)
            {
                checkTx = _database.TransactionManager.StartTransaction();
            }

            try
            {
                if (_dictionaryPropertySetDefinitions.Has(_propertySetName, checkTx!))
                {
                    _propertySetDefinition = _dictionaryPropertySetDefinitions
                        .GetAt(_propertySetName)
                        .Go<PropertySetDefinition>(checkTx!)!;
                    if (ownsTransaction)
                    {
                        checkTx!.Commit();
                    }
                    return;
                }
            }
            finally
            {
                if (ownsTransaction && checkTx != null)
                {
                    checkTx.Dispose();
                }
            }

            // Create new PropertySetDefinition
            _propertySetDefinition = new PropertySetDefinition();
            _propertySetDefinition.SetToStandard(_database);
            _propertySetDefinition.SubSetDatabaseDefaults(_database);
            _propertySetDefinition.Description = _propertySetName;

            // Set AppliesTo to all entity types (empty StringCollection means all)
            _propertySetDefinition.SetAppliesToFilter(new StringCollection(), false);

            // Add PropertyDefinitions for each column
            foreach (var kvp in _dataTable.ColumnDataTypes)
            {
                string propertyName = kvp.Key;
                PsDataType dataType = kvp.Value;
                object defaultValue = GetDefaultValue(dataType);

                PropertyDefinition propDef = new PropertyDefinition();
                propDef.SetToStandard(_database);
                propDef.SubSetDatabaseDefaults(_database);
                propDef.Name = propertyName;
                propDef.Description = $"Property from CSV column '{propertyName}'";
                propDef.DataType = dataType;
                propDef.DefaultData = defaultValue;

                _propertySetDefinition.Definitions.Add(propDef);
            }

            // Add PropertySetDefinition to database
            // Create our own transaction if we're not already in one
            Transaction defTx = _database.TransactionManager.TopTransaction;
            bool ownsDefTransaction = defTx == null;

            if (ownsDefTransaction)
            {
                defTx = _database.TransactionManager.StartTransaction();
            }

            try
            {
                _dictionaryPropertySetDefinitions.AddNewRecord(_propertySetName, _propertySetDefinition);
                defTx!.AddNewlyCreatedDBObject(_propertySetDefinition, true);
                if (ownsDefTransaction)
                {
                    defTx.Commit();
                }
            }
            finally
            {
                if (ownsDefTransaction && defTx != null)
                {
                    defTx.Dispose();
                }
            }
        }

        public (double? X, double? Y, Action<Entity> AttachPropertySet) GetNextRow()
        {
            if (!HasMoreRows)
            {
                throw new InvalidOperationException("No more rows available.");
            }

            Dictionary<string, object?> currentRow = _dataTable.Rows[_currentRowIndex]!;
            _currentRowIndex++;

            // Extract coordinates
            object? xObj = currentRow[_xColumnName];
            object? yObj = currentRow[_yColumnName];

            double? x = null;
            double? y = null;

            if (xObj != null)
            {
                try
                {
                    x = Convert.ToDouble(xObj, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // If conversion fails, x remains null
                }
            }

            if (yObj != null)
            {
                try
                {
                    y = Convert.ToDouble(yObj, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // If conversion fails, y remains null
                }
            }

            // Create action to attach and populate PropertySet
            Action<Entity> attachAction = (Entity entity) =>
            {
                if (_database.TransactionManager.TopTransaction == null)
                {
                    throw new InvalidOperationException(
                        "AttachPropertySet action must be executed within a transaction.");
                }

                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                // Attach PropertySet to entity
                entity.CheckOrOpenForWrite();
                PropertyDataServices.AddPropertySet(entity, _propertySetDefinition.Id);

                // Get the PropertySet
                PropertySet propertySet = PropertyDataServices
                    .GetPropertySet(entity, _propertySetDefinition.Id)
                    .Go<PropertySet>(_database.TransactionManager.TopTransaction)!;

                // Populate PropertySet with row data
                propertySet.CheckOrOpenForWrite();
                foreach (var kvp in currentRow)
                {
                    string propertyName = kvp.Key;
                    object value = kvp.Value ?? GetDefaultValue(_dataTable.ColumnDataTypes[propertyName]);

                    try
                    {
                        int propertyId = propertySet.PropertyNameToId(propertyName);
                        propertySet.SetAt(propertyId, value);
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg($"Error setting property '{propertyName}' to value '{value}': {ex.Message}");
                    }
                }
                propertySet.DowngradeOpen();
            };

            return (x, y, attachAction);
        }

        private object GetDefaultValue(PsDataType dataType)
        {
            switch (dataType)
            {
                case PsDataType.Text:
                    return "";
                case PsDataType.Real:
                    return 0.0;
                case PsDataType.Integer:
                    return 0;
                case PsDataType.TrueFalse:
                    return false;
                default:
                    return "";
            }
        }
    }
}