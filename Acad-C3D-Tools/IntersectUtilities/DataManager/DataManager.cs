using Autodesk.AutoCAD.DatabaseServices;

using Dreambuild.AutoCAD;

using Microsoft.Office.Interop.Excel;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.DataManagement
{
    public class DataManager : IDisposable
    {
        private Dictionary<string, Database> cache = new Dictionary<string, Database>();
        private string project;
        private string etape;
        public DataManager(DataReferencesOptions dro)
        {
            project = dro.ProjectName;
            etape = dro.EtapeName;
        }
        public bool IsValid() => !string.IsNullOrEmpty(project) && !string.IsNullOrEmpty(etape);
        /// <summary>
        /// Surface, Alignments, Fremtid
        /// </summary>
        public Database GetForRead(string name)
        {
            if (cache.ContainsKey(name)) return cache[name];
            else
            {
                var db = new Database(false, true);
                string path = UtilsCommon.Utils.GetPathToDataFiles(project, etape, name);
                db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, "");
                cache.Add(name, db);
                return db;
            }
        }
        public HashSet<Database> GetLængdepfofilerDatabases()
        {
            HashSet<Database> dbs = new HashSet<Database>();
            
            string path = UtilsCommon.Utils.GetPathToDataFiles(project, etape, "Længdeprofiler");

            Directory.EnumerateFiles(path, "Længdeprofiler*.dwg").ForEach(f =>
            {
                var db = new Database(false, true);
                db.ReadDwgFile(f, FileOpenMode.OpenForReadAndAllShare, true, "");
                dbs.Add(db);
            });

            return dbs;
        }
        public void Dispose()
        {
            foreach (var item in cache)
            {
                item.Value?.Dispose();
            }
        }
    }
}