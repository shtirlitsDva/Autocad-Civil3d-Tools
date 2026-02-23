using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using DimensioneringV2.BBRData.Models;
using DimensioneringV2.BBRData.Services;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.BBRData.AutoCAD
{
    internal static class BbrBlockWriter
    {
        /// <summary>
        /// Writes CSV values to matched BBR blocks for non-ignored 1:1 match groups.
        /// Skips properties where the current BBR value already equals the CSV value.
        /// SAFETY: Ignored groups are ALWAYS skipped, even if passed in.
        /// Returns a dictionary of KeyValue → number of properties actually written.
        /// A value of 0 means all properties were already equal (block skipped).
        /// </summary>
        public static Dictionary<string, int> WriteUpdates(
            IReadOnlyList<MatchGroup> oneToOneGroups,
            IReadOnlyList<TransferMapping> transferMappings,
            string[] csvHeaders,
            string decimalSeparator)
        {
            var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (oneToOneGroups.Count == 0 || transferMappings.Count == 0)
                return results;

            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using DocumentLock docLock = docCol.MdiActiveDocument.LockDocument();
            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                foreach (var group in oneToOneGroups)
                {
                    // DEFENSE-IN-DEPTH: Never write ignored groups, regardless of caller.
                    if (group.IsIgnored) continue;
                    if (group.Category != MatchCategory.OneToOne) continue;
                    if (group.BbrRows.Count != 1 || group.CsvRows.Count != 1) continue;

                    var bbrRow = group.BbrRows[0];
                    var csvRow = group.CsvRows[0];

                    var ent = tx.GetObject(bbrRow.EntityId, OpenMode.ForWrite) as BlockReference;
                    if (ent == null) continue;

                    var bbr = new BBR(ent);
                    int propsWritten = 0;

                    foreach (var mapping in transferMappings)
                    {
                        // Get raw CSV value for this column
                        string rawValue = string.Empty;
                        int colIndex = Array.IndexOf(csvHeaders, mapping.CsvColumnName);
                        if (colIndex >= 0 && colIndex < csvRow.RawFields.Length)
                            rawValue = csvRow.RawFields[colIndex];

                        // Convert to target type
                        object? converted = GenericCsvReader.ConvertValue(
                            rawValue, mapping.DataType, decimalSeparator);

                        if (converted == null) continue;

                        // Skip-if-equal: compare current BBR value with CSV value before writing
                        try
                        {
                            object? currentBbrVal = mapping.BbrProperty.GetValue(bbr);
                            if (currentBbrVal != null && currentBbrVal.Equals(converted))
                                continue;
                        }
                        catch
                        {
                            // If we can't read the current value, proceed with the write
                        }

                        try
                        {
                            mapping.BbrProperty.SetValue(bbr, converted);
                            propsWritten++;
                        }
                        catch (Exception ex)
                        {
                            prdDbg($"Failed to set {mapping.BbrProperty.Name} on block {bbrRow.EntityId}: {ex.Message}");
                        }
                    }

                    results[group.KeyValue] = propsWritten;
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return new Dictionary<string, int>();
            }

            return results;
        }
    }
}
