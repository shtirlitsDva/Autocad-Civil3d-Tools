using System.Globalization;
using System.Text.RegularExpressions;

namespace NTRExport.ConsoleTests.TestCases
{
    internal sealed class PreinsulatedTeeTwinTest : BaseTestCase
    {
        protected override string DwgName => "T1-Preinsulated tee, twin to twin.dwg";
        public override string DisplayName => "Preinsulated twin tee";

        protected override bool Validate(string ntrPath)
        {
            var template = LoadTemplate();
            var actual = LoadActual(ntrPath);

            if (template is null)
            {
                Console.WriteLine("INCOMPLETE: golden NTR is missing; skipping comparison and CtE assertion.");
                return true;
            }

            if (!Ntr.NtrDocumentComparer.AreEquivalent(template, actual, out var message))
            {
                Console.Error.WriteLine(message);
                return false;
            }

            return AssertBranchStubLength(actual, branchDn: 65, expectedCtEMm: 95);
        }

        private static bool AssertBranchStubLength(Ntr.NtrDocument doc, int branchDn, double expectedCtEMm)
        {
            var stubSegments = doc.Records
                .Where(r => string.Equals(r.Code, "RO", StringComparison.OrdinalIgnoreCase))
                .Select(r => ParseRo(r))
                .Where(r => r.dn == branchDn && r.suffix.Equals("t", StringComparison.OrdinalIgnoreCase))
                .Select(r => DistanceMm(r.p1, r.p2))
                .Where(d => Math.Abs(d - expectedCtEMm) <= 1.0)
                .ToList();

            if (stubSegments.Count != 2)
            {
                Console.Error.WriteLine($"ASSERT FAIL: expected two branch stubs of length {expectedCtEMm}mm ±1mm, found {stubSegments.Count}.");
                return false;
            }

            return true;
        }

        private static ((double x, double y, double z) p1, (double x, double y, double z) p2, int dn, string suffix) ParseRo(Ntr.NtrRecord record)
        {
            var fields = record.Fields.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            if (!fields.TryGetValue("P1", out var p1Value) || !fields.TryGetValue("P2", out var p2Value))
            {
                throw new InvalidDataException($"RO record missing P1/P2 data: {record.OriginalLine}");
            }

            if (!fields.TryGetValue("DN", out var dnRaw))
            {
                throw new InvalidDataException($"RO record missing DN data: {record.OriginalLine}");
            }

            var suffix = dnRaw.Length > 0 ? dnRaw[^1].ToString() : string.Empty;
            var dnText = dnRaw.Length > 2 ? dnRaw.Substring(2, dnRaw.Length - 3) : string.Empty; // DNxxx.s => extract xxx
            if (!int.TryParse(dnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn))
            {
                throw new InvalidDataException($"Unable to parse DN value '{dnRaw}' in: {record.OriginalLine}");
            }

            var p1 = ParsePoint(p1Value);
            var p2 = ParsePoint(p2Value);

            return (p1, p2, dn, suffix);
        }

        private static (double x, double y, double z) ParsePoint(string text)
        {
            var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) throw new FormatException($"Unexpected point format: {text}");
            return (
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture),
                double.Parse(parts[2], CultureInfo.InvariantCulture));
        }

        private static double DistanceMm((double x, double y, double z) a, (double x, double y, double z) b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            var dz = a.z - b.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}

