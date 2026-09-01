using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Project;
using OdTable = Autodesk.Gis.Map.ObjectData.Table;
using OdTables = Autodesk.Gis.Map.ObjectData.Tables;
using PsDataType = Autodesk.Aec.PropertyData.DataType;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.MatchBBR
{
    // Legacy fallback for drawings whose BBR data lives in a Map3D Object Data table rather than
    // an AEC property set. This is the only Object Data reader in the repo, carried over from the
    // original MATCHBBR so those drawings keep working.
    //
    // The long-term fix is to convert them once — ODDataConverter.oddatacreatepropertysetsdefs()
    // in IntersectUtilities/ODDataConverter.cs turns every OD table into a matching
    // PropertySetDefinition — after which this whole path can be deleted.
    //
    // TryOpen returns null when the drawing has no "BBR" OD table, which is the normal case, so
    // an ordinary drawing pays nothing for this existing.
    internal sealed class MatchBbrObjectData
    {
        private readonly OdTable _table;

        private MatchBbrObjectData(OdTable table)
        {
            _table = table;
        }

        public static MatchBbrObjectData? TryOpen()
        {
            try
            {
                ProjectModel project = HostMapApplicationServices.Application.ActiveProject;
                OdTables tables = project.ODTables;
                OdTable table = tables[MatchBbrConstants.PropertySetName];
                return table is null ? null : new MatchBbrObjectData(table);
            }
            catch (System.Exception)
            {
                // No Map project, or no table by that name. Both mean "this drawing does not use
                // Object Data for BBR", which is not a fault worth logging on every gather.
                return null;
            }
        }

        // True when this block carries at least one row in the BBR table.
        public bool HasRecord(BlockReference block)
        {
            try
            {
                using Records records = _table.GetObjectTableRecords(
                    0,
                    block.ObjectId,
                    Autodesk.Gis.Map.Constants.OpenMode.OpenForRead,
                    false);
                return records.Count > 0;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        // Reads the wanted properties off one block, shaped the same way the property-set path
        // shapes them so the rest of the tool cannot tell the two sources apart.
        public Dictionary<string, BbrPropertyValue> ReadValues(
            BlockReference block,
            IEnumerable<string> wanted,
            IReadOnlyDictionary<string, PSetDefs.Property> propertyByName)
        {
            Dictionary<string, BbrPropertyValue> values =
                new Dictionary<string, BbrPropertyValue>(StringComparer.OrdinalIgnoreCase);

            foreach (string name in wanted)
            {
                if (!TryReadRaw(block, name, out string raw))
                {
                    continue;
                }

                PsDataType dataType =
                    propertyByName.TryGetValue(name, out PSetDefs.Property? property)
                        ? property.DataType
                        : PsDataType.Text;

                values[name] = Shape(raw, dataType);
            }

            return values;
        }

        private bool TryReadRaw(BlockReference block, string propertyName, out string value)
        {
            value = string.Empty;

            try
            {
                using Records records = _table.GetObjectTableRecords(
                    0,
                    block.ObjectId,
                    Autodesk.Gis.Map.Constants.OpenMode.OpenForRead,
                    false);

                foreach (Record record in records)
                {
                    for (int i = 0; i < _table.FieldDefinitions.Count; i++)
                    {
                        FieldDefinition fieldDefinition = _table.FieldDefinitions[i];
                        if (!string.Equals(
                                fieldDefinition.Name,
                                propertyName,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // MapValue exposes the payload through typed accessors; StrValue is the
                        // one the original implementation used, with ToString as the fallback.
                        dynamic cell = record[i];
                        value = cell?.StrValue?.ToString() ?? cell?.ToString() ?? string.Empty;
                        return true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg($"MatchBBR: Object Data-læsning fejlede for {block.Handle}/{propertyName}.");
                prdDbg(ex);
            }

            return false;
        }

        // Object Data hands back strings for everything, so numeric properties are parsed here to
        // give the comparison the same Number it gets from a property set.
        private static BbrPropertyValue Shape(string raw, PsDataType dataType)
        {
            string trimmed = (raw ?? string.Empty).Trim();

            if (dataType is PsDataType.Real or PsDataType.Integer)
            {
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    || double.TryParse(
                        trimmed.Replace(',', '.'),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out number))
                {
                    string text = dataType == PsDataType.Real
                        ? number.ToString("0.##", CultureInfo.CurrentCulture)
                        : ((int)Math.Round(number, MidpointRounding.AwayFromZero))
                            .ToString(CultureInfo.CurrentCulture);

                    return new BbrPropertyValue(text, number, dataType);
                }
            }

            return new BbrPropertyValue(trimmed, null, dataType);
        }
    }
}
