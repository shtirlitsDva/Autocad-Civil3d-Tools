using Autodesk.AutoCAD.DatabaseServices;

using NTRExport.NtrConfiguration;
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

        public string Build(NtrGraph g, IEnumerable<string> headerRecords)
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
    }
}
