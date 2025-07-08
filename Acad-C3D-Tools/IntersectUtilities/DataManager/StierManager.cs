using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.DataManager
{
    internal static class StierManager
    {
        private static readonly string[] ValueColumns = ["Ler", "Surface", "Alignments", "Fremtid", "Længdeprofiler"];

        private static readonly Lazy<Dictionary<(string ProjectId, string EtapeId), HashSet<StierRecord>>> _cache
            = new(() => LoadAndResolve(), isThreadSafe: true);
    }
}
