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
        /// Writes CSV values to matched BBR blocks for transferable groups (1:1 and N:1).
        /// Skips properties where the current BBR value already equals the CSV value.
        /// SAFETY: Ignored groups/rows are ALWAYS skipped, even if passed in.
        /// Returns a dictionary of result key → number of properties actually written.
        /// For 1:1: key = group.KeyValue. For N:1: key = "KeyValue|EntityId" per BBR row.
        /// A value of 0 means all properties were already equal (block skipped).
        /// </summary>
        public static Dictionary<string, int> WriteUpdates(
            IReadOnlyList<MatchGroup> transferableGroups,
            IReadOnlyList<TransferMapping> transferMappings,
            string[] csvHeaders,
            string decimalSeparator)
        {
            var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (transferableGroups.Count == 0 || transferMappings.Count == 0)
                return results;

            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using DocumentLock docLock = docCol.MdiActiveDocument.LockDocument();
            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                foreach (var group in transferableGroups)
                {
                    if (group.Category == MatchCategory.OneToOne)
                    {
                        // DEFENSE-IN-DEPTH: Never write ignored groups.
                        if (group.IsIgnored) continue;
                        if (group.BbrRows.Count != 1 || group.CsvRows.Count != 1) continue;

                        int written = WriteSingleBbrRow(
                            tx, group.BbrRows[0], group.CsvRows[0],
                            transferMappings, csvHeaders, decimalSeparator);
                        results[group.KeyValue] = written;
                    }
                    else if (group.Category == MatchCategory.ManyToOne)
                    {
                        // N:1: write CSV data to each non-ignored BBR row
                        if (group.CsvRows.Count != 1) continue;
                        var csvRow = group.CsvRows[0];

                        foreach (var bbrRow in group.BbrRows)
                        {
                            if (bbrRow.IsIgnored) continue;

                            int written = WriteSingleBbrRow(
                                tx, bbrRow, csvRow,
                                transferMappings, csvHeaders, decimalSeparator);
                            results[$"{group.KeyValue}|{bbrRow.EntityId}"] = written;
                        }
                    }
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

        /// <summary>
        /// Writes CSV values to a single BBR block. Returns number of properties written.
        /// </summary>
        private static int WriteSingleBbrRow(
            Transaction tx,
            BbrRowData bbrRow,
            CsvRowData csvRow,
            IReadOnlyList<TransferMapping> transferMappings,
            string[] csvHeaders,
            string decimalSeparator)
        {
            var ent = tx.GetObject(bbrRow.EntityId, OpenMode.ForWrite) as BlockReference;
            if (ent == null) return 0;

            var bbr = new BBR(ent);
            int propsWritten = 0;

            foreach (var mapping in transferMappings)
            {
                string rawValue = string.Empty;
                int colIndex = Array.IndexOf(csvHeaders, mapping.CsvColumnName);
                if (colIndex >= 0 && colIndex < csvRow.RawFields.Length)
                    rawValue = csvRow.RawFields[colIndex];

                object? converted = GenericCsvReader.ConvertValue(
                    rawValue, mapping.DataType, decimalSeparator);

                if (converted == null) continue;

                // Skip-if-equal
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

            return propsWritten;
        }
    }
}
