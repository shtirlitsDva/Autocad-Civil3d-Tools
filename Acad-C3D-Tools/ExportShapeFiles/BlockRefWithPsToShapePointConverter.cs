using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities;

using NetTopologySuite.Features;

using System;
using System.Collections.Generic;

using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace ExportShapeFiles
{
    internal static class BlockRefWithPsToShapePointConverter
    {
        public static Feature Convert(
            Entity entity,
            List<PSetDefs.DefinedSets>? propertySets,
            List<string>? nonDefinedPropertySets = null)
        {
            if (!(entity is BlockReference br))
                throw new ArgumentException($"Entity {entity.Handle} is not a block reference!");

            if ((propertySets == null || propertySets.Count == 0) &&
                (nonDefinedPropertySets == null || nonDefinedPropertySets.Count == 0))
                throw new ArgumentException("At least one property set list must be provided!");

            // Ensure we're in a transaction
            Transaction tx = entity.Database.TransactionManager.TopTransaction;
            if (tx == null)
                throw new System.Exception("BlockRefWithPsToShapePointConverter.Convert must be called within a transaction!");

            // Get all property values from defined property sets
            Dictionary<string, (object value, PsDataType dataType)> definedProps =
                PropertySetManager.GetAllPropertyValues(entity, propertySets ?? new List<PSetDefs.DefinedSets>());

            // Get all property values from non-defined property sets
            Dictionary<string, (object value, PsDataType dataType)> nonDefinedProps =
                PropertySetManager.GetAllPropertyValues(entity, nonDefinedPropertySets ?? new List<string>(), tx);

            // Merge both dictionaries (non-defined will overwrite defined if same property name)
            var allProps = new Dictionary<string, (object value, PsDataType dataType)>(definedProps);
            foreach (var kvp in nonDefinedProps)
            {
                allProps[kvp.Key] = kvp.Value;
            }

            // Sanitize property names to max 10 characters for shape file compatibility
            // and convert values to appropriate types
            Dictionary<string, object> sanitizedProps = SanitizePropertyNamesAndConvertValues(allProps);
            
            // Create AttributesTable from dictionary (required - can't set values after creation)
            var props = new AttributesTable(sanitizedProps);

            var geom = new NetTopologySuite.Geometries.Point(
                new NetTopologySuite.Geometries.Coordinate(br.Position.X, br.Position.Y));

            return new Feature(geom, props);
        }

        /// <summary>
        /// Sanitizes property names to be at most 10 characters long for shape file compatibility.
        /// Truncates names to 10 chars and replaces last char with numbers (2, 3...) for duplicates.
        /// Converts values to appropriate types based on their data type.
        /// </summary>
        private static Dictionary<string, object> SanitizePropertyNamesAndConvertValues(
            Dictionary<string, (object value, PsDataType dataType)> properties)
        {
            const int maxLength = 10;
            var result = new Dictionary<string, object>();
            var nameCounts = new Dictionary<string, int>();

            foreach (var kvp in properties)
            {
                string originalName = kvp.Key;
                object value = kvp.Value.value;
                PsDataType dataType = kvp.Value.dataType;
                string baseName;

                // Truncate to max length to get base name
                if (originalName.Length > maxLength)
                {
                    baseName = originalName.Substring(0, maxLength);
                }
                else
                {
                    baseName = originalName;
                }

                string sanitizedName;

                // Handle duplicates by replacing last char with number
                if (nameCounts.ContainsKey(baseName))
                {
                    nameCounts[baseName]++;
                    int count = nameCounts[baseName];

                    // Replace last character(s) with the count number
                    // For single digit (1-9), replace last char
                    // For multiple digits, replace last N chars where N is digit count
                    int digitCount = count.ToString().Length;

                    if (baseName.Length >= digitCount)
                    {
                        // Replace last N characters with the count
                        sanitizedName = baseName.Substring(0, baseName.Length - digitCount) + count.ToString();
                    }
                    else
                    {
                        // If base name is shorter than digit count, just use the count
                        sanitizedName = count.ToString().PadLeft(maxLength, '0');
                    }
                }
                else
                {
                    // First occurrence - use base name as-is and mark as seen
                    nameCounts[baseName] = 1;
                    sanitizedName = baseName;
                }

                // Ensure final name doesn't exceed max length
                if (sanitizedName.Length > maxLength)
                {
                    sanitizedName = sanitizedName.Substring(0, maxLength);
                }

                // Convert value to appropriate type based on dataType
                object convertedValue = ConvertValueToType(value, dataType);
                result[sanitizedName] = convertedValue;
            }

            return result;
        }

        /// <summary>
        /// Converts property value to appropriate type based on data type.
        /// Handles special markers like "&lt;null&gt;" and "&lt;empty string&gt;" from TryReadNonDefinedPropertySetObject.
        /// </summary>
        private static object ConvertValueToType(object value, PsDataType dataType)
        {
            // Handle null values
            if (value == null)
            {
                return GetDefaultValueForType(dataType);
            }

            // Handle special string markers from TryReadNonDefinedPropertySetObject
            if (value is string strValue)
            {
                if (strValue == "<null>")
                {
                    return GetDefaultValueForType(dataType);
                }
                if (strValue == "<empty string>")
                {
                    return "";
                }
            }

            // Convert based on data type
            switch (dataType)
            {
                case PsDataType.Text:
                    return value?.ToString() ?? "";
                case PsDataType.Integer:
                    if (value == null) return 0;
                    try
                    {
                        return System.Convert.ToInt32(value);
                    }
                    catch
                    {
                        return 0;
                    }
                case PsDataType.Real:
                    if (value == null) return 0.0;
                    try
                    {
                        return System.Convert.ToDouble(value);
                    }
                    catch
                    {
                        return 0.0;
                    }
                case PsDataType.TrueFalse:
                    if (value == null) return false;
                    try
                    {
                        return System.Convert.ToBoolean(value);
                    }
                    catch
                    {
                        return false;
                    }
                case PsDataType.List:
                    return value?.ToString() ?? "";
                default:
                    return value?.ToString() ?? "";
            }
        }

        private static object GetDefaultValueForType(PsDataType dataType)
        {
            switch (dataType)
            {
                case PsDataType.Text:
                    return "";
                case PsDataType.Integer:
                    return 0;
                case PsDataType.Real:
                    return 0.0;
                case PsDataType.TrueFalse:
                    return false;
                case PsDataType.List:
                    return "";
                default:
                    return "";
            }
        }
    }
}