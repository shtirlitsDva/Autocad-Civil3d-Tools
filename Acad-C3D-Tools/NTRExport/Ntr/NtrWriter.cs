using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NTRExport.NtrConfiguration;
using NTRExport.Routing;
using NTRExport.SoilModel;

using System.Text;

namespace NTRExport.Ntr
{
    internal class Rohr2SoilAdapter : INtrSoilAdapter
    {
        public IEnumerable<string> Define(SoilProfile p)
        {
            // Using SOIL_* parameters per element; no global soil definitions needed.
            yield break;
        }
        public string? RefToken(SoilProfile p) => null;
    }

    internal class NtrWriter
    {
        private readonly INtrSoilAdapter _soil;
        private readonly ConfigurationData _conf;
        public NtrWriter(INtrSoilAdapter soil, ConfigurationData conf) { _soil = soil; _conf = conf; }

        public string Build(RoutedGraph g, IEnumerable<string> headerRecords)
        {
            var sb = new StringBuilder();
            // Header: units in millimeters
            sb.AppendLine("C General settings");
            sb.AppendLine("GEN TMONT=10 EB=-Z UNITKT=MM CODE=EN13941");

            sb.AppendLine("C Loads definition");
            foreach (var last in _conf.Last) sb.AppendLine(last.ToString());

            // DN and IS sections from headerRecords
            var dnLines = headerRecords.Where(l => l.StartsWith("DN ", StringComparison.OrdinalIgnoreCase)).ToList();
            var isLines = headerRecords.Where(l => l.StartsWith("IS ", StringComparison.OrdinalIgnoreCase)).ToList();

            // Sort DN records by NAME: twin/bonded (t vs s), series (s1, s2, s3, empty), then DN (ascending)
            dnLines.Sort((a, b) =>
            {
                var nameA = ExtractName(a);
                var nameB = ExtractName(b);
                return CompareDnName(nameA, nameB);
            });

            // Sort IS records by NAME: twin/bonded (t vs s), then DN (ascending)
            // Note: IS records don't have series suffix in NAME
            isLines.Sort((a, b) =>
            {
                var nameA = ExtractName(a);
                var nameB = ExtractName(b);
                return CompareIsName(nameA, nameB);
            });

            sb.AppendLine("C Definition of pipe dimensions");
            foreach (var dn in dnLines) sb.AppendLine(dn);

            sb.AppendLine("C Definition of insulation type");
            foreach (var isl in isLines) sb.AppendLine(isl);

            // Geometry
            sb.AppendLine("C Element definitions");

            var totalByHandle = new Dictionary<Handle, int>();
            foreach (var member in g.Members)
            {
                if (totalByHandle.TryGetValue(member.Source, out var count))
                {
                    totalByHandle[member.Source] = count + 1;
                }
                else
                {
                    totalByHandle[member.Source] = 1;
                }
            }

            var nextOrdinal = new Dictionary<Handle, int>();

            foreach (var member in g.Members)
            {
                var handle = member.Source;
                var refValue = handle.ToString();
                if (totalByHandle.TryGetValue(member.Source, out var total) && total > 1)
                {
                    var next = nextOrdinal.TryGetValue(member.Source, out var ordinal) ? ordinal + 1 : 1;
                    nextOrdinal[member.Source] = next;
                    refValue = $"{handle}-{next}";
                }

                foreach (var line in member.ToNtr(_soil, _conf))
                {
                    sb.AppendLine($"{line} REF={refValue}");
                }
            }
            return sb.ToString();
        }

        private static string ExtractName(string record)
        {
            // Extract NAME=... from record like "DN NAME=DN125.ts1 DA=..."
            var match = Regex.Match(record, @"NAME=([^\s]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static int CompareDnName(string nameA, string nameB)
        {
            // Format: DN{dn}.{s/t}{s1/s2/s3/empty}
            // Example: DN125.ts1, DN125.s, DN150.ts2
            
            var matchA = Regex.Match(nameA, @"^DN(\d+)\.([st])(s[123]|)$", RegexOptions.IgnoreCase);
            var matchB = Regex.Match(nameB, @"^DN(\d+)\.([st])(s[123]|)$", RegexOptions.IgnoreCase);
            
            if (!matchA.Success || !matchB.Success) return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            
            // 1. Compare twin/bonded: t comes before s
            var twinA = matchA.Groups[2].Value.ToLowerInvariant();
            var twinB = matchB.Groups[2].Value.ToLowerInvariant();
            var twinCmp = string.Compare(twinA, twinB, StringComparison.OrdinalIgnoreCase);
            if (twinCmp != 0) return twinCmp;
            
            // 2. Compare series: empty < s1 < s2 < s3
            var seriesA = matchA.Groups[3].Value.ToLowerInvariant();
            var seriesB = matchB.Groups[3].Value.ToLowerInvariant();
            var seriesOrder = new Dictionary<string, int> { { "", 0 }, { "s1", 1 }, { "s2", 2 }, { "s3", 3 } };
            var seriesCmp = (seriesOrder.TryGetValue(seriesA, out var sa) ? sa : 999)
                .CompareTo(seriesOrder.TryGetValue(seriesB, out var sb) ? sb : 999);
            if (seriesCmp != 0) return seriesCmp;
            
            // 3. Compare DN (ascending)
            if (int.TryParse(matchA.Groups[1].Value, out var dnA) &&
                int.TryParse(matchB.Groups[1].Value, out var dnB))
            {
                return dnA.CompareTo(dnB);
            }
            
            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareIsName(string nameA, string nameB)
        {
            // Format: FJV{dn}.{s/t}
            // Example: FJV125.t, FJV125.s, FJV150.t
            
            var matchA = Regex.Match(nameA, @"^FJV(\d+)\.([st])$", RegexOptions.IgnoreCase);
            var matchB = Regex.Match(nameB, @"^FJV(\d+)\.([st])$", RegexOptions.IgnoreCase);
            
            if (!matchA.Success || !matchB.Success) return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
            
            // 1. Compare twin/bonded: t comes before s
            var twinA = matchA.Groups[2].Value.ToLowerInvariant();
            var twinB = matchB.Groups[2].Value.ToLowerInvariant();
            var twinCmp = string.Compare(twinA, twinB, StringComparison.OrdinalIgnoreCase);
            if (twinCmp != 0) return twinCmp;
            
            // 2. Compare DN (ascending)
            if (int.TryParse(matchA.Groups[1].Value, out var dnA) &&
                int.TryParse(matchB.Groups[1].Value, out var dnB))
            {
                return dnA.CompareTo(dnB);
            }
            
            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
        }
    }
}
