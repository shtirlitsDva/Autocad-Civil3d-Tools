using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.MPE.Shared;

namespace IntersectUtilities.MPE.MatchBBR
{
    internal sealed class MatchResult
    {
        public MatchResult(IReadOnlyList<ComparisonRow> rows)
        {
            Rows = rows;
        }

        public static readonly MatchResult Empty = new MatchResult(Array.Empty<ComparisonRow>());

        public IReadOnlyList<ComparisonRow> Rows { get; }

        public int CountOf(MatchStatus status) => Rows.Count(x => x.Status == status);

        public string Summary() =>
            $"{Rows.Count} rækker · {CountOf(MatchStatus.Match)} match · "
            + $"{CountOf(MatchStatus.Afvigelse)} afvigelse · "
            + $"{CountOf(MatchStatus.KunIExcel)} kun i Excel · "
            + $"{CountOf(MatchStatus.KunITegning)} kun i tegning · "
            + $"{CountOf(MatchStatus.Dublet)} dublet";
    }

    // Full outer join of the selected Excel rows against the gathered BBR blocks on the key rule,
    // followed by evaluation of the remaining rules on each matched pair.
    //
    // Modelled on DSMERGEDATA in DataScience.cs: a row is consumed at most once, and every row
    // that fails to match carries a recorded reason. The reason is what makes a surprising result
    // arguable — without it "kun i Excel" gives no clue whether the address was blank, ambiguous,
    // or genuinely absent.
    internal static class MatchBbrMatcher
    {
        // hints maps an address key to the reason a block with that address was left out of
        // bbrRecords — outside the boundary, or skipped for Type = "Ingen". Without it every such
        // row reads "the address is not in the drawing", which is not what happened.
        public static MatchResult Run(
            IReadOnlyList<ExcelRowRecord> excelRows,
            IReadOnlyList<BbrRecord> bbrRecords,
            IReadOnlyList<CompareRule> rules,
            Func<CompareRule, string> labelFor,
            IReadOnlyDictionary<string, string>? hints = null)
        {
            CompareRule? keyRule = rules.FirstOrDefault(r => r.IsKey && r.IsConfigured);
            if (keyRule is null)
            {
                return MatchResult.Empty;
            }

            List<CompareRule> valueRules = rules
                .Where(r => !r.IsKey && r.IsConfigured)
                .ToList();

            // Every key each side can be found under. Both word orders are generated on both
            // sides, so the direction of the comparison cannot change the outcome.
            Dictionary<string, List<BbrRecord>> bbrByKey =
                new Dictionary<string, List<BbrRecord>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<Handle, string> bbrPrimaryKey = new Dictionary<Handle, string>();

            foreach (BbrRecord record in bbrRecords)
            {
                IReadOnlyList<string> keys =
                    MatchBbrAddressKey.BuildKeys(record.Text(keyRule.BbrPropertyName));
                bbrPrimaryKey[record.Handle] = keys.Count > 0 ? keys[0] : string.Empty;

                foreach (string key in keys)
                {
                    if (!bbrByKey.TryGetValue(key, out List<BbrRecord>? bucket))
                    {
                        bucket = new List<BbrRecord>();
                        bbrByKey[key] = bucket;
                    }

                    if (!bucket.Any(x => x.Handle == record.Handle))
                    {
                        bucket.Add(record);
                    }
                }
            }

            HashSet<Handle> consumed = new HashSet<Handle>();
            List<ComparisonRow> rows = new List<ComparisonRow>();

            foreach (ExcelRowRecord excelRow in excelRows)
            {
                string excelAddress = keyRule.ReadExcelText(excelRow.Row);
                IReadOnlyList<string> keys = MatchBbrAddressKey.BuildKeys(excelAddress);

                if (keys.Count == 0)
                {
                    rows.Add(new ComparisonRow(
                        MatchStatus.KunIExcel,
                        string.Empty,
                        excelRow,
                        null,
                        EvaluateRules(valueRules, excelRow, null, labelFor),
                        "Ingen adressenøgle i rækken"));
                    continue;
                }

                string primaryKey = keys[0];

                List<BbrRecord> candidates = keys
                    .SelectMany(key => bbrByKey.TryGetValue(key, out List<BbrRecord>? bucket)
                        ? bucket
                        : Enumerable.Empty<BbrRecord>())
                    .GroupBy(x => x.Handle)
                    .Select(g => g.First())
                    .ToList();

                if (candidates.Count == 0)
                {
                    rows.Add(new ComparisonRow(
                        MatchStatus.KunIExcel,
                        primaryKey,
                        excelRow,
                        null,
                        EvaluateRules(valueRules, excelRow, null, labelFor),
                        ExplainMissing(keys, hints)));
                    continue;
                }

                List<BbrRecord> available = candidates.Where(x => !consumed.Contains(x.Handle)).ToList();

                if (available.Count == 0)
                {
                    // Another Excel row already claimed the block on this address — two Excel rows
                    // sharing one address is itself the finding, so it is not reported as missing.
                    rows.Add(new ComparisonRow(
                        MatchStatus.Dublet,
                        primaryKey,
                        excelRow,
                        candidates[0],
                        EvaluateRules(valueRules, excelRow, candidates[0], labelFor),
                        "Adressen er allerede matchet af en anden Excel-række"));
                    continue;
                }

                if (available.Count > 1)
                {
                    // Several blocks on one address. The old code silently kept one of them.
                    // All of them are consumed here so they do not reappear as "kun i tegning",
                    // and the row says how many there were.
                    foreach (BbrRecord duplicate in available)
                    {
                        consumed.Add(duplicate.Handle);
                    }

                    rows.Add(new ComparisonRow(
                        MatchStatus.Dublet,
                        primaryKey,
                        excelRow,
                        available[0],
                        EvaluateRules(valueRules, excelRow, available[0], labelFor),
                        $"{available.Count} BBR-blokke deler adressen"));
                    continue;
                }

                BbrRecord matched = available[0];
                consumed.Add(matched.Handle);

                IReadOnlyList<RuleCellResult> cells = EvaluateRules(valueRules, excelRow, matched, labelFor);
                bool hasMismatch = cells.Any(c => c.IsMismatch);

                rows.Add(new ComparisonRow(
                    hasMismatch ? MatchStatus.Afvigelse : MatchStatus.Match,
                    primaryKey,
                    excelRow,
                    matched,
                    cells,
                    hasMismatch ? "Adresse matcher, værdi afviger" : string.Empty));
            }

            // Whatever the Excel side never claimed.
            foreach (BbrRecord record in bbrRecords)
            {
                if (consumed.Contains(record.Handle))
                {
                    continue;
                }

                string key = bbrPrimaryKey.TryGetValue(record.Handle, out string? value)
                    ? value
                    : string.Empty;

                rows.Add(new ComparisonRow(
                    MatchStatus.KunITegning,
                    key,
                    null,
                    record,
                    EvaluateRules(valueRules, null, record, labelFor),
                    string.IsNullOrWhiteSpace(key)
                        ? "Blokken har ingen adresse"
                        : "Adressen findes ikke i de valgte Excel-rækker"));
            }

            return new MatchResult(rows);
        }

        // A block with this address does exist — it was just left out of the compared set. Saying
        // which of the two reasons applies is the difference between a usable finding and a
        // wrong-looking one.
        private static string ExplainMissing(
            IReadOnlyList<string> keys,
            IReadOnlyDictionary<string, string>? hints)
        {
            if (hints is not null)
            {
                foreach (string key in keys)
                {
                    if (hints.TryGetValue(key, out string? hint))
                    {
                        return hint;
                    }
                }
            }

            return "Adressen findes ikke i tegningen";
        }

        private static IReadOnlyList<RuleCellResult> EvaluateRules(
            IReadOnlyList<CompareRule> rules,
            ExcelRowRecord? excelRow,
            BbrRecord? bbrRecord,
            Func<CompareRule, string> labelFor)
        {
            if (rules.Count == 0)
            {
                return Array.Empty<RuleCellResult>();
            }

            List<RuleCellResult> results = new List<RuleCellResult>(rules.Count);
            foreach (CompareRule rule in rules)
            {
                results.Add(Evaluate(rule, excelRow, bbrRecord, labelFor(rule)));
            }

            return results;
        }

        private static RuleCellResult Evaluate(
            CompareRule rule,
            ExcelRowRecord? excelRow,
            BbrRecord? bbrRecord,
            string label)
        {
            string excelText = excelRow is null ? string.Empty : rule.ReadExcelText(excelRow.Row);
            BbrPropertyValue bbrValue = bbrRecord?.Value(rule.BbrPropertyName) ?? BbrPropertyValue.Missing;
            string bbrText = bbrValue.Text;

            // Only a pair can be compared. A one-sided row still shows its value, greyed.
            if (excelRow is null || bbrRecord is null)
            {
                return new RuleCellResult(rule.Id, label, excelText, bbrText, null, false, false);
            }

            switch (rule.Kind)
            {
                case CompareRuleKind.Numeric:
                {
                    bool hasExcel = rule.TryReadExcelNumber(excelRow.Row, out double excelNumber);
                    double? bbrNumber = bbrValue.Number ?? TryParse(bbrText);

                    if (!hasExcel || !bbrNumber.HasValue)
                    {
                        return new RuleCellResult(rule.Id, label, excelText, bbrText, null, false, false);
                    }

                    double delta = bbrNumber.Value - excelNumber;
                    double allowed = rule.ToleranceIsPercent
                        ? Math.Abs(excelNumber) * rule.Tolerance / 100.0
                        : rule.Tolerance;

                    // A percentage of zero is zero, which would flag every blank-vs-blank pair.
                    // Fall back to the absolute tolerance when the reference value is zero.
                    if (rule.ToleranceIsPercent && Math.Abs(excelNumber) < 1e-9)
                    {
                        allowed = rule.Tolerance;
                    }

                    bool mismatch = Math.Abs(delta) > allowed + 1e-9;
                    return new RuleCellResult(
                        rule.Id,
                        label,
                        excelText,
                        bbrText,
                        delta,
                        mismatch,
                        true,
                        excelNumber,
                        bbrNumber.Value);
                }

                case CompareRuleKind.TextExact:
                {
                    bool mismatch = !string.Equals(
                        excelText.Trim(),
                        bbrText.Trim(),
                        StringComparison.OrdinalIgnoreCase);
                    return new RuleCellResult(rule.Id, label, excelText, bbrText, null, mismatch, true);
                }

                default:
                {
                    bool mismatch = !string.Equals(
                        MatchBbrAddressKey.Normalize(excelText),
                        MatchBbrAddressKey.Normalize(bbrText),
                        StringComparison.Ordinal);
                    return new RuleCellResult(rule.Id, label, excelText, bbrText, null, mismatch, true);
                }
            }
        }

        private static double? TryParse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            string trimmed = text.Trim();
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            return null;
        }
    }
}
