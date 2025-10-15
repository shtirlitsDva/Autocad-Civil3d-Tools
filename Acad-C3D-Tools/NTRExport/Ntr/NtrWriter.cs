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

        public string Build(NtrGraph g)
        {
            var sb = new StringBuilder();
            // Header: units in millimeters
            sb.AppendLine("GEN TMONT=20 EB=-Z UNITKT=MM");

            // Optional: project/text lines can be added here if needed

            // 1) unique soil defs (none currently)
            foreach (var s in g.Members.OfType<NtrPipe>().Select(x => x.Soil).Distinct())
                foreach (var line in _soil.Define(s)) sb.AppendLine(line);

            // 2) geometry with per-element SOIL_* tokens already appended
            foreach (var m in g.Members)
                foreach (var line in m.ToNtr(_soil)) sb.AppendLine(line);
            return sb.ToString();
        }
    }
}
