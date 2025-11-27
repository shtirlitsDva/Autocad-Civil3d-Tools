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
    public class GenericPropertySetImporter
    {
        private readonly Database _database;
        private readonly string _csvFilePath;
        private readonly string _propertySetName;
        private readonly DictionaryPropertySetDefinitions _dictionaryPropertySetDefinitions;
        private PropertySetDefinition _propertySetDefinition;
        private readonly List<Dictionary<string, object>> _rows;
        private int _currentRowIndex;
        private readonly Dictionary<string, PsDataType> _columnDataTypes;
        private readonly string _xColumnName;
        private readonly string _yColumnName;

        public bool HasMoreRows => _currentRowIndex < _rows.Count;

        public GenericPropertySetImporter(
            Database database,
            string csvFilePath,
            string xColumnName,
            string yColumnName)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));
            if (string.IsNullOrWhiteSpace(csvFilePath))
                throw new ArgumentException("CSV file path cannot be null or empty.", nameof(csvFilePath));
            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException($"CSV file not found: {csvFilePath}");

            _database = database;
            _csvFilePath = csvFilePath;
            _xColumnName = xColumnName ?? throw new ArgumentNullException(nameof(xColumnName));
            _yColumnName = yColumnName ?? throw new ArgumentNullException(nameof(yColumnName));

            // Extract property set name from file name (without path and extension)
            _propertySetName = Path.GetFileNameWithoutExtension(csvFilePath);

            _dictionaryPropertySetDefinitions = new DictionaryPropertySetDefinitions(_database);
            _rows = new List<Dictionary<string, object>>();
            _columnDataTypes = new Dictionary<string, PsDataType>();
            _currentRowIndex = 0;

            LoadCsvFile();
            CreatePropertySetDefinition();
        }

        private void LoadCsvFile()
        {
            string[] columnNames = null;
            string[] dataTypes = null;

            using (TextFieldParser csvParser = new TextFieldParser(_csvFilePath))
            {
                csvParser.CommentTokens = new string[] { "#" };
                csvParser.SetDelimiters(new string[] { ";" });
                csvParser.HasFieldsEnclosedInQuotes = false;

                // Read first header row (column names)
                if (!csvParser.EndOfData)
                {
                    columnNames = csvParser.ReadFields();
                }
                else
                {
                    throw new InvalidDataException("CSV file is empty or missing header row.");
                }

                // Read second header row (data types)
                if (!csvParser.EndOfData)
                {
                    dataTypes = csvParser.ReadFields();
                }
                else
                {
                    throw new InvalidDataException("CSV file is missing data type header row.");
                }

                if (columnNames.Length != dataTypes.Length)
                {
                    throw new InvalidDataException(
                        $"Column count mismatch: {columnNames.Length} column names but {dataTypes.Length} data types.");
                }

                // Map CSV data types to PropertySet data types
                for (int i = 0; i < columnNames.Length; i++)
                {
                    string columnName = columnNames[i];
                    string csvDataType = dataTypes[i].ToLowerInvariant().Trim();
                    PsDataType psDataType = MapCsvTypeToPropertySetType(csvDataType);
                    _columnDataTypes[columnName] = psDataType;
                }

                // Verify coordinate columns exist
                if (!_columnDataTypes.ContainsKey(_xColumnName))
                {
                    throw new ArgumentException(
                        $"X coordinate column '{_xColumnName}' not found in CSV file.");
                }
                if (!_columnDataTypes.ContainsKey(_yColumnName))
                {
                    throw new ArgumentException(
                        $"Y coordinate column '{_yColumnName}' not found in CSV file.");
                }

                // Read data rows
                while (!csvParser.EndOfData)
                {
                    string[] fields = csvParser.ReadFields();
                    if (fields.Length != columnNames.Length)
                    {
                        prdDbg($"Warning: Row {_rows.Count + 1} has {fields.Length} fields, expected {columnNames.Length}. Skipping.");
                        continue;
                    }

                    Dictionary<string, object> row = new Dictionary<string, object>();
                    for (int i = 0; i < columnNames.Length; i++)
                    {
                        string columnName = columnNames[i];
                        string fieldValue = fields[i];
                        object parsedValue = ParseFieldValue(fieldValue, _columnDataTypes[columnName]);
                        row[columnName] = parsedValue;
                    }
                    _rows.Add(row);
                }
            }
        }

        private PsDataType MapCsvTypeToPropertySetType(string csvType)
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
                    // Dates are stored as Text in PropertySets
                    return PsDataType.Text;
                default:
                    // Default to Text for unknown types
                    prdDbg($"Warning: Unknown CSV data type '{csvType}', defaulting to Text.");
                    return PsDataType.Text;
            }
        }

        private object ParseFieldValue(string fieldValue, PsDataType targetType)
        {
            if (string.IsNullOrWhiteSpace(fieldValue))
            {
                return GetDefaultValue(targetType);
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
                        return 0.0;
                    case PsDataType.Integer:
                        if (int.TryParse(fieldValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                            return intValue;
                        return 0;
                    case PsDataType.TrueFalse:
                        if (bool.TryParse(fieldValue, out bool boolValue))
                            return boolValue;
                        // Try parsing "1"/"0" or "true"/"false" case-insensitively
                        if (fieldValue.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                            fieldValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (fieldValue.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                            fieldValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                            return false;
                        return false;
                    default:
                        return fieldValue;
                }
            }
            catch (System.Exception ex)
            {
                prdDbg($"Error parsing field value '{fieldValue}' as {targetType}: {ex.Message}");
                return GetDefaultValue(targetType);
            }
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
                if (_dictionaryPropertySetDefinitions.Has(_propertySetName, checkTx))
                {
                    _propertySetDefinition = _dictionaryPropertySetDefinitions
                        .GetAt(_propertySetName)
                        .Go<PropertySetDefinition>(checkTx);
                    if (ownsTransaction)
                    {
                        checkTx.Commit();
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
            foreach (var kvp in _columnDataTypes)
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
                defTx.AddNewlyCreatedDBObject(_propertySetDefinition, true);
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

        public (Point3d Coordinates, Action<Entity> AttachPropertySet) GetNextRow()
        {
            if (!HasMoreRows)
            {
                throw new InvalidOperationException("No more rows available.");
            }

            Dictionary<string, object> currentRow = _rows[_currentRowIndex];
            _currentRowIndex++;

            // Extract coordinates
            object xObj = currentRow[_xColumnName];
            object yObj = currentRow[_yColumnName];

            double x = Convert.ToDouble(xObj, CultureInfo.InvariantCulture);
            double y = Convert.ToDouble(yObj, CultureInfo.InvariantCulture);
            Point3d coordinates = new Point3d(x, y, 0.0);

            // Create action to attach and populate PropertySet
            Action<Entity> attachAction = (Entity entity) =>
            {
                if (_database.TransactionManager.TopTransaction == null)
                {
                    throw new InvalidOperationException(
                        "AttachPropertySet action must be executed within a transaction.");
                }

                // Attach PropertySet to entity
                entity.CheckOrOpenForWrite();
                PropertyDataServices.AddPropertySet(entity, _propertySetDefinition.Id);

                // Get the PropertySet
                PropertySet propertySet = PropertyDataServices
                    .GetPropertySet(entity, _propertySetDefinition.Id)
                    .Go<PropertySet>(_database.TransactionManager.TopTransaction);

                // Populate PropertySet with row data
                propertySet.CheckOrOpenForWrite();
                foreach (var kvp in currentRow)
                {
                    string propertyName = kvp.Key;
                    object value = kvp.Value;

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

            return (coordinates, attachAction);
        }
    }
}