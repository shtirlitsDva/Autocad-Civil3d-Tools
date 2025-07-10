using IntersectUtilities.UtilsCommon.DataManager.FileResolvers;
using IntersectUtilities.UtilsCommon;

using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;

namespace IntersectUtilities.UtilsCommon.DataManager
{
    internal static class StierManager
    {
        private static readonly string[] ValueColumns = ["Ler", "Surface", "Alignments", "Fremtid", "Længdeprofiler"];

        private static readonly Dictionary<(string ProjectId, string EtapeId), StierRecord> _cache;

        static StierManager()
        {
            _cache = LoadStierData();
        }

        public static IEnumerable<string> GetFileNames((string, string) key, StierDataType dataType)
        {
            if (_cache == null) throw new System.Exception("Something wrong with Stier! _cache in StierManager is null!");
            if (!_cache.TryGetValue(key, out var sr)) throw new System.Exception(
                $"{key} is not set! This project, etape does not exist!");

            return dataType switch
            {
                StierDataType.Ler => sr.Ler,
                StierDataType.Surface when sr.Surface is { } s => [s],
                StierDataType.Alignments when sr.Alignments is { } s => [s],
                StierDataType.Fremtid when sr.Fremtid is { } s => [s],
                StierDataType.Længdeprofiler => sr.Længdeprofiler,
                _ => []
            };
        }        
        public static IEnumerable<string> Projects() => _cache.Keys.Select(x => x.ProjectId).Distinct().Order();
        public static IEnumerable<string> PhasesForProject(string projectId) => 
            _cache.Keys.Where(x => x.ProjectId == projectId).Select(x => x.EtapeId).Distinct().Order();        
        public static IEnumerable<(string ProjectId, string EtapeId)> DetectProjectAndEtape(string fileName) =>
            _cache.Values.Where(x => x.ContainsFile(fileName)).Select(x => x.Key).OrderBy(x => x.ProjectId).ThenBy(x => x.EtapeId);
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
                            break;
                    }
                }

                //if all entries are empty then the record is not accessible
                if (Fremtid == null &&
                    Alignments == null &&
                    Surface == null &&
                    Ler.Count == 0 &&
                    Længdeprofiler.Count == 0) continue;

                var sr = new StierRecord(key, Fremtid, Alignments, Længdeprofiler, Ler, Surface);
                result[key] = sr;
            }

            return result;
        }
    }
}