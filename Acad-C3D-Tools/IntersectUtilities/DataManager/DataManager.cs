using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.DataManager
{
    public class DataManager
    {
        private Dictionary<string, Database> cache = new Dictionary<string, Database>();
        private string project;
        private string etape;
        public DataManager(DataReferencesOptions dro)
        {
            project = dro.ProjectName;
            etape = dro.EtapeName;
        }
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
    }
}
