using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.MPE.Shared;
using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities.MPE.MatchBBR
{
    // How a comparison row came out. The old MATCHBBR/COMPAREBBR pair could only express
    // "in both" and "in one" — once a value (varmeforbrug) is compared as well, a matched
    // address whose value disagrees is its own case, and it is usually the interesting one.
    internal enum MatchStatus
    {
        Match,
        Afvigelse,
        KunIExcel,
        KunITegning,
        Dublet,
    }

    internal enum CompareRuleKind
    {
        TextNormalized,
        TextExact,
        Numeric,
    }

    // One declared comparison: an Excel source, a BBR property, and how to compare them.
    // Exactly one rule in a set carries IsKey and is used to join the two sides; the rest are
    // evaluated on the pairs the key produced.
    internal sealed class CompareRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public bool IsKey { get; set; }

        // Any number of worksheet columns, joined with a space in the order listed. One column is
        // an "Adresse"-style workbook, two are the usual Vejnavn + Husnummer, and more exist:
        // Vejnavn + Husnummer + Etage + Dør is a real addressing scheme.
        //
        // Column letters, not headers — headers are not unique and may be blank.
        public List<string> ExcelColumns { get; set; } = new List<string>();

        public string BbrPropertyName { get; set; } = string.Empty;

        public CompareRuleKind Kind { get; set; } = CompareRuleKind.TextNormalized;

        public double Tolerance { get; set; } = 5.0;

        public bool ToleranceIsPercent { get; set; } = true;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(BbrPropertyName)
            && ExcelColumns.Count > 0
            && ExcelColumns.All(c => !string.IsNullOrWhiteSpace(c));

        public string ReadExcelText(XlsxRow row)
        {
            if (ExcelColumns.Count == 1)
            {
                return row[ExcelColumns[0]].Trim();
            }

            // Blank parts are dropped rather than leaving double spaces, so a missing Etage does
            // not change the key of every address in the sheet.
            return string.Join(
                " ",
                ExcelColumns
                    .Select(column => row[column].Trim())
                    .Where(value => value.Length > 0));
        }

        // Only meaningful for Numeric rules; joining several columns has no numeric reading.
        public bool TryReadExcelNumber(XlsxRow row, out double value)
        {
            if (ExcelColumns.Count == 1)
            {
                return row.TryGetDouble(ExcelColumns[0], out value);
            }

            value = 0.0;
            return false;
        }

        public CompareRule Clone() =>
            new CompareRule
            {
                Id = Id,
                IsKey = IsKey,
                ExcelColumns = new List<string>(ExcelColumns),
                BbrPropertyName = BbrPropertyName,
                Kind = Kind,
                Tolerance = Tolerance,
                ToleranceIsPercent = ToleranceIsPercent,
            };
    }

    // One BBR property value read off a block, kept in both shapes so a rule can compare it
    // either as text or as a number without re-reading the drawing.
    internal sealed class BbrPropertyValue
    {
        public BbrPropertyValue(string text, double? number, PsDataType dataType)
        {
            Text = text;
            Number = number;
            DataType = dataType;
        }

        public string Text { get; }

        public double? Number { get; }

        public PsDataType DataType { get; }

        public static readonly BbrPropertyValue Missing =
            new BbrPropertyValue(string.Empty, null, PsDataType.Text);
    }

    // A snapshot of one BBR block. Deliberately detached from the Transaction it was read in:
    // the palette outlives every transaction, so the grid must not hold open DBObjects. The
    // ObjectId and Handle are enough to re-open the block for zooming or write-back.
    internal sealed class BbrRecord
    {
        public BbrRecord(
            ObjectId objectId,
            Handle handle,
            Point3d position,
            IReadOnlyDictionary<string, BbrPropertyValue> values)
        {
            ObjectId = objectId;
            Handle = handle;
            Position = position;
            Values = values;
        }

        public ObjectId ObjectId { get; }

        public Handle Handle { get; }

        public Point3d Position { get; }

        public IReadOnlyDictionary<string, BbrPropertyValue> Values { get; }

        public BbrPropertyValue Value(string propertyName) =>
            Values.TryGetValue(propertyName, out BbrPropertyValue? value) ? value : BbrPropertyValue.Missing;

        public string Text(string propertyName) => Value(propertyName).Text;

        public string Adresse => Text(MatchBbrConstants.AdresseProperty);

        public string Distrikt => Text(MatchBbrConstants.DistriktProperty);
    }

    // An Excel row plus the tick that decides whether it takes part in the comparison.
    internal sealed class ExcelRowRecord
    {
        public ExcelRowRecord(XlsxRow row)
        {
            Row = row;
        }

        public XlsxRow Row { get; }

        public bool IsIncluded { get; set; } = true;
    }

    // Per-rule outcome on one comparison row.
    internal sealed class RuleCellResult
    {
        public RuleCellResult(
            string ruleId,
            string label,
            string excelText,
            string bbrText,
            double? delta,
            bool isMismatch,
            bool isComparable,
            double? excelNumber = null,
            double? bbrNumber = null)
        {
            RuleId = ruleId;
            Label = label;
            ExcelText = excelText;
            BbrText = bbrText;
            Delta = delta;
            IsMismatch = isMismatch;
            IsComparable = isComparable;
            ExcelNumber = excelNumber;
            BbrNumber = bbrNumber;
        }

        // The unrounded values behind ExcelText/BbrText, so the export writes full precision even
        // though the grid shows two decimals.
        public double? ExcelNumber { get; }

        public double? BbrNumber { get; }

        public string RuleId { get; }

        public string Label { get; }

        public string ExcelText { get; }

        public string BbrText { get; }

        public double? Delta { get; }

        public bool IsMismatch { get; }

        // False when one side had no value at all — shown as blank rather than as a mismatch,
        // because "no data" and "different data" are different problems.
        public bool IsComparable { get; }

        // Two decimals, matching the values it is the difference of.
        public string DeltaText => Delta.HasValue
            ? Delta.Value.ToString("0.##", CultureInfo.CurrentCulture)
            : string.Empty;
    }

    // One row of the comparison grid: an Excel row, a BBR block, or both.
    internal sealed class ComparisonRow
    {
        public ComparisonRow(
            MatchStatus status,
            string key,
            ExcelRowRecord? excelRow,
            BbrRecord? bbrRecord,
            IReadOnlyList<RuleCellResult> cells,
            string reason)
        {
            Status = status;
            Key = key;
            ExcelRow = excelRow;
            BbrRecord = bbrRecord;
            Cells = cells;
            Reason = reason;
        }

        public MatchStatus Status { get; }

        // The normalized address key this row was joined on. Empty when the row had none.
        public string Key { get; }

        public ExcelRowRecord? ExcelRow { get; }

        public BbrRecord? BbrRecord { get; }

        public IReadOnlyList<RuleCellResult> Cells { get; }

        // Why an unmatched row failed to match, carried into the grid and the export. Without
        // it a wrong-looking comparison is very hard to argue with.
        public string Reason { get; }

        public bool HasBlock => BbrRecord is not null;

        // Writable means both sides are present AND the pairing is unambiguous.
        //
        // Dublet is excluded deliberately: on such a row BbrRecord is whichever block the matcher
        // happened to take first out of several sharing the address, so writing to it is a coin
        // flip the user cannot see, and it silently edits survey data on an arbitrary building.
        public bool IsWritable =>
            ExcelRow is not null
            && BbrRecord is not null
            && (Status == MatchStatus.Match || Status == MatchStatus.Afvigelse);

        public RuleCellResult? Cell(string ruleId) =>
            Cells.FirstOrDefault(x => string.Equals(x.RuleId, ruleId, StringComparison.Ordinal));
    }

    internal static class MatchBbrConstants
    {
        public const string CommandName = "MATCHBBR";

        // The property set's name, which a legacy drawing reuses as its Map3D Object Data table
        // name — see MatchBbrObjectData.
        public const string PropertySetName = "BBR";

        // BBR property set property names. Spelled exactly as PSetDefs.BBR declares them —
        // note the capital F in VarmeForbrug, which is easy to get wrong.
        public const string AdresseProperty = "Adresse";
        public const string VejnavnProperty = "Vejnavn";
        public const string HusnummerProperty = "Husnummer";
        public const string TypeProperty = "Type";
        public const string DistriktProperty = "DistriktetsNavn";
        public const string EstimeretVarmeForbrugProperty = "EstimeretVarmeForbrug";

        // Blocks whose BBR Type is this are excluded, as in the original commands.
        public const string SkippedTypeValue = "Ingen";

        // Radius of the transient preview circles drawn around matched blocks. Nothing is written
        // to the drawing, so there are no marker layers or XData tags any more — see
        // MatchBbrPreview.
        public const double MarkerRadius = 5.0;
    }
}
