using System;
using System.Collections.Generic;
using System.Linq;

using DimensioneringV2.BBRData.Models;

namespace DimensioneringV2.BBRData.Services
{
    internal static class MatchingEngine
    {
        private const string KeySeparator = "|";

        /// <summary>
        /// Computes matches between BBR rows and CSV rows using separate key lists.
        /// BBR key[i] is paired with CSV key[i] by order.
        /// </summary>
        public static MatchResult ComputeMatches(
            IReadOnlyList<BbrRowData> bbrRows,
            IReadOnlyList<CsvRowData> csvRows,
            IReadOnlyList<BbrMatchKey> bbrKeys,
            IReadOnlyList<CsvMatchKey> csvKeys)
        {
            var result = new MatchResult();

            int keyCount = Math.Min(bbrKeys.Count, csvKeys.Count);
            if (keyCount == 0 || bbrRows.Count == 0 || csvRows.Count == 0)
                return result;

            // Compute composite key for each BBR row
            foreach (var row in bbrRows)
                row.ComputedKey = ComputeBbrCompositeKey(row, bbrKeys, keyCount);

            // Compute composite key for each CSV row
            foreach (var row in csvRows)
                row.ComputedKey = ComputeCsvCompositeKey(row, csvKeys, keyCount);

            // Group by key
            var bbrGroups = bbrRows
                .GroupBy(r => r.ComputedKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var csvGroups = csvRows
                .GroupBy(r => r.ComputedKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            // Collect all unique keys
            var allKeys = new HashSet<string>(bbrGroups.Keys, StringComparer.OrdinalIgnoreCase);
            allKeys.UnionWith(csvGroups.Keys);

            foreach (var key in allKeys)
            {
                bbrGroups.TryGetValue(key, out var bbrList);
                csvGroups.TryGetValue(key, out var csvList);

                int bbrCount = bbrList?.Count ?? 0;
                int csvCount = csvList?.Count ?? 0;

                var category = Classify(bbrCount, csvCount);

                result.Groups.Add(new MatchGroup
                {
                    Category = category,
                    KeyValue = key,
                    BbrRows = bbrList ?? new List<BbrRowData>(),
                    CsvRows = csvList ?? new List<CsvRowData>()
                });
            }

            // Sort groups: OneToOne first, then OneToMany/ManyToOne, then Unmatched, then ManyToMany
            result.Groups.Sort((a, b) => GetCategorySortOrder(a.Category)
                .CompareTo(GetCategorySortOrder(b.Category)));

            return result;
        }

        /// <summary>
        /// Validates the complete setup for readiness to update.
        /// Returns (isValid, diagnosticMessage).
        /// </summary>
        public static (bool IsValid, string Message) ValidateSetup(
            IReadOnlyList<BbrMatchKey> bbrKeys,
            IReadOnlyList<CsvMatchKey> csvKeys,
            IReadOnlyList<TransferMapping> transferMappings,
            MatchResult? matchResult)
        {
            // 1. At least one key on each side
            if (bbrKeys.Count == 0)
                return (false, "Define at least one BBR key.");
            if (csvKeys.Count == 0)
                return (false, "Define at least one CSV key.");

            // 2. Key counts must match
            if (bbrKeys.Count != csvKeys.Count)
                return (false, $"Key count mismatch: {bbrKeys.Count} BBR key(s) vs {csvKeys.Count} CSV key(s).");

            // 3. All keys must have parts defined
            for (int i = 0; i < bbrKeys.Count; i++)
            {
                if (!bbrKeys[i].HasParts)
                    return (false, $"BBR key {i + 1}: no property selected.");
            }
            for (int i = 0; i < csvKeys.Count; i++)
            {
                if (!csvKeys[i].HasParts)
                    return (false, $"CSV key {i + 1}: no column selected.");
            }

            // 4. Need at least one transfer mapping
            // (type matching is guaranteed by construction — type auto-derived from BBR property)
            if (transferMappings.Count == 0)
                return (false, "Select at least one transfer mapping.");

            // Matching checks (only if match result available)
            if (matchResult == null)
                return (false, "No match data available. Load CSV and define keys.");

            // 8. All non-1:1 groups must be ignored.
            // Ignored groups do NOT participate in data transfer but stop blocking.
            if (!matchResult.AllNonOneToOneIgnored)
            {
                var unresolved = matchResult.UnresolvedGroups.ToList();
                int count = unresolved.Count;
                var firstCategory = unresolved[0].Category switch
                {
                    MatchCategory.ManyToMany => "N:N",
                    MatchCategory.OneToMany => "1:N",
                    MatchCategory.ManyToOne => "N:1",
                    MatchCategory.BbrUnmatched => "unmatched BBR",
                    MatchCategory.CsvUnmatched => "unmatched CSV",
                    _ => "non-1:1"
                };
                return (false, $"{count} non-1:1 group(s) not ignored (first: {firstCategory}). Ignore them to proceed.");
            }

            var transferable = matchResult.TransferableGroups;
            int totalBbrBlocks = transferable.Sum(g =>
                g.Category == MatchCategory.ManyToOne ? g.NonIgnoredBbrRows.Count : g.BbrRows.Count);
            return (true, $"Ready — {totalBbrBlocks} block(s) will be updated ({transferable.Count} group(s)).");
        }

        private static string ComputeBbrCompositeKey(
            BbrRowData row, IReadOnlyList<BbrMatchKey> bbrKeys, int keyCount)
        {
            var keyParts = new List<string>(keyCount);
            for (int i = 0; i < keyCount; i++)
                keyParts.Add(bbrKeys[i].ComputeKeyValue(row));
            return string.Join(KeySeparator, keyParts);
        }

        private static string ComputeCsvCompositeKey(
            CsvRowData row, IReadOnlyList<CsvMatchKey> csvKeys, int keyCount)
        {
            var keyParts = new List<string>(keyCount);
            for (int i = 0; i < keyCount; i++)
                keyParts.Add(csvKeys[i].ComputeKeyValue(row));
            return string.Join(KeySeparator, keyParts);
        }

        private static MatchCategory Classify(int bbrCount, int csvCount)
        {
            if (bbrCount == 0) return MatchCategory.CsvUnmatched;
            if (csvCount == 0) return MatchCategory.BbrUnmatched;
            if (bbrCount == 1 && csvCount == 1) return MatchCategory.OneToOne;
            if (bbrCount > 1 && csvCount > 1) return MatchCategory.ManyToMany;
            if (bbrCount == 1 && csvCount > 1) return MatchCategory.OneToMany;
            return MatchCategory.ManyToOne; // bbrCount > 1 && csvCount == 1
        }

        private static int GetCategorySortOrder(MatchCategory cat) => cat switch
        {
            MatchCategory.OneToOne => 0,
            MatchCategory.OneToMany => 1,
            MatchCategory.ManyToOne => 1,
            MatchCategory.BbrUnmatched => 2,
            MatchCategory.CsvUnmatched => 2,
            MatchCategory.ManyToMany => 3,
            _ => 4
        };
    }
}
