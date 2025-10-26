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
            var ok = Regex.IsMatch(File.ReadAllText(ntrPath), @"^TEE ", RegexOptions.Multiline)
                     & Regex.IsMatch(File.ReadAllText(ntrPath), @"^RO .+REF=.*-1", RegexOptions.Multiline)
                     & Regex.IsMatch(File.ReadAllText(ntrPath), @"^RO .+REF=.*-2", RegexOptions.Multiline)
                     & Regex.IsMatch(File.ReadAllText(ntrPath), @"^BOG ", RegexOptions.Multiline)
                     & AssertBranchStubLength(ntrPath, 65, expectedCtEMm: 95);
            return ok;
        }

        private static bool AssertBranchStubLength(string ntrPath, int branchDn, double expectedCtEMm)
        {
            var text = File.ReadAllText(ntrPath);
            var matches = Regex.Matches(text, @"^RO .*P1='(?<p1>[^']+)' .*P2='(?<p2>[^']+)' .*DN=DN(?<dn>\d+)\.(?<suffix>[st])", RegexOptions.Multiline);

            var stubSegments = matches
                .Cast<Match>()
                .Where(m => m.Groups["dn"].Value == branchDn.ToString(CultureInfo.InvariantCulture)
                            && m.Groups["suffix"].Value == "t")
                .Select(m =>
                {
                    var p1 = ParsePoint(m.Groups["p1"].Value);
                    var p2 = ParsePoint(m.Groups["p2"].Value);
                    return DistanceMm(p1, p2);
                })
                .Where(d => Math.Abs(d - expectedCtEMm) <= 1.0)
                .ToList();

            if (stubSegments.Count != 2)
            {
                Console.Error.WriteLine($"ASSERT FAIL: expected two branch stubs of length {expectedCtEMm}mm ±1mm, found {stubSegments.Count}.");
                return false;
            }

            return true;
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

