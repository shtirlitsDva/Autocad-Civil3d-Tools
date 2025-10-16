using NTRExport.SoilModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public NtrWriter(INtrSoilAdapter soil) { _soil = soil; }        

        public string Build(NtrGraph g, IEnumerable<string> headerRecords, NtrConfiguration.ConfigurationData conf)
        {
            var sb = new StringBuilder();
            // Header: units in millimeters
            sb.AppendLine("C General settings");
            sb.AppendLine("GEN TMONT=10 EB=-Z UNITKT=MM CODE=EN13941");

            sb.AppendLine("C Loads definition");
            foreach (var last in conf.Last) sb.AppendLine(last.ToString());            

            // DN and IS sections from headerRecords
            var dnLines = headerRecords.Where(l => l.StartsWith("DN ", StringComparison.OrdinalIgnoreCase)).ToList();
            var isLines = headerRecords.Where(l => l.StartsWith("IS ", StringComparison.OrdinalIgnoreCase)).ToList();

            sb.AppendLine("C Definition of pipe dimensions");
            foreach (var dn in dnLines) sb.AppendLine(dn);

            sb.AppendLine("C Definition of insulation type");
            foreach (var isl in isLines) sb.AppendLine(isl);

            // Geometry
            sb.AppendLine("C Element definitions");
            foreach (var m in g.Members)
                foreach (var line in m.ToNtr(_soil)) sb.AppendLine(line);
            return sb.ToString();
        }
    }
}
