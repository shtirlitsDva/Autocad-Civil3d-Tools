using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.DataManager.FileResolvers;
using IntersectUtilities.UtilsCommon;

using System.Collections.Generic;
using System.Linq;
using System;

namespace IntersectUtilities.UtilsCommon.DataManager
{
    internal static class StierManager
    {
        private static readonly string[] ValueColumns = ["Ler", "Surface", "Alignments", "Fremtid", "Længdeprofiler"];
        
        static StierManager()
        {
            
        }

        public static IEnumerable<string> GetFileNames((string, string) key, StierDataType dataType)
        {
            var _cache = LoadStierData();
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
        public static IEnumerable<string> Projects() => LoadStierData().Keys.Select(x => x.ProjectId).Distinct().Order();
        public static IEnumerable<string> PhasesForProject(string projectId) => 
            LoadStierData().Keys.Where(x => x.ProjectId == projectId).Select(x => x.EtapeId).Distinct().Order();        
        public static IEnumerable<(string ProjectId, string EtapeId)> DetectProjectAndEtape(string fileName) =>
            LoadStierData().Values.Where(x => x.ContainsFile(fileName)).Select(x => x.Key).OrderBy(x => x.ProjectId).ThenBy(x => x.EtapeId);
        private static Dictionary<(string ProjectId, string EtapeId), StierRecord> LoadStierData()
        {
            var stier = Csv.Stier;
            var result = new Dictionary<(string, string), StierRecord>();

            var rs = new FileResolverSingle();
            var rler = new FileResolverLer();
            var rlgd = new FileResolverLængdeprofiler();

            foreach (var row in stier.Rows)
            {
                // Get values by column index
                var prjId = row.Length > (int)CsvData.Stier.Columns.PrjId ? row[(int)CsvData.Stier.Columns.PrjId] : string.Empty;
                var etape = row.Length > (int)CsvData.Stier.Columns.Etape ? row[(int)CsvData.Stier.Columns.Etape] : string.Empty;

                if (prjId.IsNoE() || etape.IsNoE()) continue;

                var key = (prjId, etape);

                // Get other column values by index
                string lerValue = row.Length > (int)CsvData.Stier.Columns.Ler ? row[(int)CsvData.Stier.Columns.Ler] : string.Empty;
                string surfaceValue = row.Length > (int)CsvData.Stier.Columns.Surface ? row[(int)CsvData.Stier.Columns.Surface] : string.Empty;
                string alignmentsValue = row.Length > (int)CsvData.Stier.Columns.Alignments ? row[(int)CsvData.Stier.Columns.Alignments] : string.Empty;
                string fremtidValue = row.Length > (int)CsvData.Stier.Columns.Fremtid ? row[(int)CsvData.Stier.Columns.Fremtid] : string.Empty;
                string laengdeprofilerValue = row.Length > (int)CsvData.Stier.Columns.Laengdeprofiler ? row[(int)CsvData.Stier.Columns.Laengdeprofiler] : string.Empty;

                List<string> Ler = rler.ResolveFiles(lerValue).ToList();
                string? Surface = rs.ResolveFiles(surfaceValue).FirstOrDefault();
                string? Alignments = rs.ResolveFiles(alignmentsValue).FirstOrDefault();
                string? Fremtid = rs.ResolveFiles(fremtidValue).FirstOrDefault();
                List<string> Længdeprofiler = rlgd.ResolveFiles(laengdeprofilerValue).ToList();

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