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
            // emit your soil model definition lines as needed
            yield return $"UMG NAME={p.Name} CUSHION={p.CushionThk}";
        }
        public string? RefToken(SoilProfile p) => $"UMG={p.Name}";
    }

    internal class NtrWriter
    {
        private readonly INtrSoilAdapter _soil;
        public NtrWriter(INtrSoilAdapter soil) { _soil = soil; }

        public string Build(NtrGraph g)
        {
            var sb = new StringBuilder();
            // 1) unique soil defs
            foreach (var s in g.Members.OfType<NtrPipe>().Select(x => x.Soil).Distinct())
                foreach (var line in _soil.Define(s)) sb.AppendLine(line);
            // 2) geometry
            foreach (var m in g.Members)
                foreach (var line in m.ToNtr(_soil)) sb.AppendLine(line);
            return sb.ToString();
        }
    }
}
