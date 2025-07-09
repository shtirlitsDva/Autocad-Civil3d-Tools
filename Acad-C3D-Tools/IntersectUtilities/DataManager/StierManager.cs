using IntersectUtilities.DataManager.FileResolvers;
using IntersectUtilities.UtilsCommon;

using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace IntersectUtilities.DataManager
{
    internal static class StierManager
    {
        private static readonly string[] ValueColumns = ["Ler", "Surface", "Alignments", "Fremtid", "Længdeprofiler"];

        private static readonly Dictionary<(string ProjectId, string EtapeId), StierRecord> _cache;

        static StierManager()
        {
            _cache = LoadStierData();
        }

        private static Dictionary<(string ProjectId, string EtapeId), StierRecord> LoadStierData()
        {
            var dt = CsvData.Stier;
            var result = new Dictionary<(string, string), StierRecord>();

            var rs = new FileResolverSingle();
            var rler = new FileResolverLer();
            var rlgd = new FileResolverLængdeprofiler();

            foreach (DataRow row in dt.Rows)
            {
                var prjId = row["PrjId"]?.ToString() ?? string.Empty;
                var etape = row["Etape"]?.ToString() ?? string.Empty;

                if (prjId.IsNoE() || etape.IsNoE()) continue;

                var key = (prjId, etape);

                string? Fremtid = null, Alignments = null, Surface = null;
                List<string> Ler = new(), Længdeprofiler = new();                

                foreach (var column in ValueColumns)
                {
                    string value = row[column]?.ToString() ?? string.Empty;

                    switch (column)
                    {
                        case "Ler":
                            Ler = rler.ResolveFiles(value).ToList();
                            break;
                        case "Surface":
                            Surface = rs.ResolveFiles(value).FirstOrDefault();
                            break;
                        case "Alignments":
                            Alignments = rs.ResolveFiles(value).FirstOrDefault();
                            break;
                        case "Fremtid":
                            Fremtid = rs.ResolveFiles(value).FirstOrDefault();
                            break;
                        case "Længdeprofiler":
                            Længdeprofiler = rlgd.ResolveFiles(value).ToList();
                            break;
                        default:
                            throw new System.Exception($"Undefined value column {column}");
                    }
                }

                var sr = new StierRecord(key, Fremtid, Alignments, Længdeprofiler, Ler, Surface);
                result[key] = sr;
            }

            return result;
        }
    }
}
