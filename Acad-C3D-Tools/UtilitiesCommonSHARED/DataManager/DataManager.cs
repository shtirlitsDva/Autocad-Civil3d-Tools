using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.UtilsCommon.DataManager
{
    public class DataManager
    {
        private string project;
        private string etape;
        public (string ProjectId, string EtapeId) StierKey => (project, etape);
        public DataManager(DataReferencesOptions dro)
        {
            project = dro.ProjectName;
            etape = dro.EtapeName;
        }
        public bool IsValid() => !string.IsNullOrEmpty(project) && !string.IsNullOrEmpty(etape);
        internal IEnumerable<string> GetFileNames(StierDataType type) => StierManager.GetFileNames(StierKey, type);
        private IEnumerable<Database> getDatabases(StierDataType type)
        {
            var fileNames = GetFileNames(type);
            if (fileNames.Count() == 0)
                throw new Exception($"{StierKey} does not have {type} defined!");

            List<Database> dbs = new();
            foreach (var fileName in fileNames)
            {
                var db = new Database(false, true);
                db.ReadDwgFile(fileName, FileOpenMode.OpenForReadAndAllShare, true, "");
                dbs.Add(db);
            }

            return dbs;
        }
        public Database Fremtid()
        {
            var key = StierDataType.Fremtid;
            var dbs = getDatabases(key);
            if (dbs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return dbs.First();
        }
        public Database Surface()
        {
            var key = StierDataType.Surface;
            var dbs = getDatabases(key);
            if (dbs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return dbs.First();
        }
        public Database Alignments()
        {
            var key = StierDataType.Alignments;
            var dbs = getDatabases(key);
            if (dbs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return dbs.First();
        }
        public List<Database> Ler()
        {
            var key = StierDataType.Ler;
            var dbs = getDatabases(key);
            if (dbs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return dbs.ToList();
        }
        public List<Database> Længdeprofiler()
        {
            var key = StierDataType.Længdeprofiler;
            var dbs = getDatabases(key);
            if (dbs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return dbs.ToList();
        }
        public string PathToFremtid()
        {
            var key = StierDataType.Fremtid;
            var fs = GetFileNames(key);
            if (fs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return fs.First();
        }
        public string PathToSurface()
        {
            var key = StierDataType.Surface;
            var fs = GetFileNames(key);
            if (fs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return fs.First();
        }
        public string PathToAlignments()
        {
            var key = StierDataType.Alignments;
            var fs = GetFileNames(key);
            if (fs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return fs.First();
        }
        public List<string> PathToLer()
        {
            var key = StierDataType.Ler;
            var fs = GetFileNames(key);
            if (fs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return fs.ToList();
        }
        public List<string> PathToLængdeprofiler()
        {
            var key = StierDataType.Længdeprofiler;
            var fs = GetFileNames(key);
            if (fs.Count() == 0) throw new Exception($"{StierKey} does not have {key} defined!");
            return fs.ToList();
        }        
    }
}