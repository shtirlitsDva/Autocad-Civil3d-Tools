using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities.MPE.MatchBBR
{
    internal sealed class WriteBackResult
    {
        public WriteBackResult(int written, int skipped, IReadOnlyList<string> problems)
        {
            Written = written;
            Skipped = skipped;
            Problems = problems;
        }

        public int Written { get; }

        public int Skipped { get; }

        public IReadOnlyList<string> Problems { get; }
    }

    // Pushes Excel values onto the matched BBR blocks, for the properties named on the right-hand
    // side of the rule table and nothing else.
    //
    // The caller owns the document lock and the transaction, so a bulk write lands as a single
    // AutoCAD undo step and a failure part-way through rolls the whole batch back rather than
    // leaving the drawing half-updated.
    internal static class MatchBbrWriteBack
    {
        // Writes every configured non-key rule on every row that has both sides.
        public static WriteBackResult WriteAll(
            Database database,
            IEnumerable<ComparisonRow> rows,
            IReadOnlyList<CompareRule> rules)
        {
            List<CompareRule> writable = rules.Where(r => !r.IsKey && r.IsConfigured).ToList();
            if (writable.Count == 0)
            {
                return new WriteBackResult(0, 0, new[] { "Ingen regler at skrive (nøgleregler skrives ikke)." });
            }

            return Write(database, rows.SelectMany(row => writable.Select(rule => (row, rule))));
        }

        // Writes one value on one row — the per-cell arrow button in the comparison grid.
        public static WriteBackResult WriteOne(Database database, ComparisonRow row, CompareRule rule)
        {
            return Write(database, new[] { (row, rule) });
        }

        private static WriteBackResult Write(
            Database database,
            IEnumerable<(ComparisonRow Row, CompareRule Rule)> writes)
        {
            PSetDefs.BBR definition = new PSetDefs.BBR();
            Dictionary<string, PSetDefs.Property> propertyByName = definition
                .ListOfProperties()
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            PropertySetManager psm = new PropertySetManager(database, PSetDefs.DefinedSets.BBR);
            Transaction tx = database.TransactionManager.TopTransaction
                ?? throw new InvalidOperationException(
                    "MatchBbrWriteBack.Write must run inside an open transaction.");

            int written = 0;
            int skipped = 0;
            List<string> problems = new List<string>();

            foreach ((ComparisonRow row, CompareRule rule) in writes)
            {
                // Only an unambiguously matched pair has both a value to write and somewhere
                // definite to write it. Checked here rather than only at the call sites so the
                // bulk button and the per-cell arrow are governed by the same rule.
                if (!row.IsWritable)
                {
                    if (row.Status == MatchStatus.Dublet)
                    {
                        problems.Add(
                            $"{Describe(row)}: sprunget over — flere blokke deler adressen, "
                            + "så det er uklart hvilken der skulle skrives til.");
                    }

                    skipped++;
                    continue;
                }

                // IsWritable already guarantees this, but it is a property on another type so the
                // compiler's null-flow analysis cannot see through it. Bind it once rather than
                // sprinkling null-forgiving operators over the write below.
                BbrRecord bbrRecord = row.BbrRecord!;

                // The key rule is what joined the two sides; overwriting it would break the very
                // relationship the row is built on, so it is never written.
                if (rule.IsKey || !rule.IsConfigured)
                {
                    skipped++;
                    continue;
                }

                if (!propertyByName.TryGetValue(rule.BbrPropertyName, out PSetDefs.Property? property))
                {
                    problems.Add($"Ukendt BBR-egenskab '{rule.BbrPropertyName}'.");
                    skipped++;
                    continue;
                }

                if (!TryConvert(rule, row, property.DataType, out object? value, out string problem))
                {
                    problems.Add($"{Describe(row)}: {problem}");
                    skipped++;
                    continue;
                }

                try
                {
                    if (tx.GetObject(bbrRecord.ObjectId, OpenMode.ForWrite) is not Entity entity)
                    {
                        skipped++;
                        continue;
                    }

                    psm.WritePropertyObject(entity, property, value!);
                    written++;
                }
                catch (System.Exception ex)
                {
                    problems.Add($"{Describe(row)}: {ex.Message}");
                    skipped++;
                }
            }

            return new WriteBackResult(written, skipped, problems);
        }

        private static bool TryConvert(
            CompareRule rule,
            ComparisonRow row,
            PsDataType dataType,
            out object? value,
            out string problem)
        {
            value = null;
            problem = string.Empty;

            string text = rule.ReadExcelText(row.ExcelRow!.Row);

            switch (dataType)
            {
                case PsDataType.Real:
                {
                    if (!rule.TryReadExcelNumber(row.ExcelRow.Row, out double number)
                        && !TryParseDouble(text, out number))
                    {
                        problem = $"'{text}' kan ikke læses som tal for {rule.BbrPropertyName}.";
                        return false;
                    }

                    value = number;
                    return true;
                }

                case PsDataType.Integer:
                {
                    if (!rule.TryReadExcelNumber(row.ExcelRow.Row, out double number)
                        && !TryParseDouble(text, out number))
                    {
                        problem = $"'{text}' kan ikke læses som heltal for {rule.BbrPropertyName}.";
                        return false;
                    }

                    value = (int)Math.Round(number, MidpointRounding.AwayFromZero);
                    return true;
                }

                case PsDataType.TrueFalse:
                {
                    if (!bool.TryParse(text.Trim(), out bool flag))
                    {
                        problem = $"'{text}' kan ikke læses som sand/falsk for {rule.BbrPropertyName}.";
                        return false;
                    }

                    value = flag;
                    return true;
                }

                default:
                {
                    value = text;
                    return true;
                }
            }
        }

        private static bool TryParseDouble(string text, out double value)
        {
            value = 0.0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(
                    trimmed.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static string Describe(ComparisonRow row)
        {
            if (row.BbrRecord is not null && !string.IsNullOrWhiteSpace(row.BbrRecord.Adresse))
            {
                return row.BbrRecord.Adresse;
            }

            return string.IsNullOrWhiteSpace(row.Key) ? "(ukendt række)" : row.Key;
        }
    }
}
