using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetTopologySuite.Geometries;

namespace IntersectUtilities.LongitudinalProfiles
{
    public class Ler3dManagerFolder : Ler3dManagerBase
    {
        private Dictionary<string, Database> storage = new Dictionary<string, Database>();
        private Dictionary<string, Polygon> areas = new Dictionary<string, Polygon>();
        private HashSet<Entity> entities = new HashSet<Entity>();
        public override void Load(string path)
        {
            var files = Directory.EnumerateFiles(path, "*_3DLER.dwg", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var db = new Database(false, true);
                db.ReadDwgFile(file, FileShare.Read, true, "");
                storage.Add(name, db);

                Transaction tx = db.TransactionManager.StartTransaction();
                MPolygon mpg = db.ListOfType<MPolygon>(tx).FirstOrDefault();

                areas.Add(name, NTS.NTSConversion.ConvertMPolygonToNTSPolygon(mpg));
            }
            if (!IsLoadValid()) throw new Exception("Ler3d load failed!: \n" + path);
        }
        protected override bool IsLoadValid() =>
            storage != null &&
            storage.Count > 0 &&
            !storage.Values.Any(x => x == null);
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var db in storage?.Values)
                {
                    while (db?.TransactionManager?.TopTransaction != null)
                    {
                        db?.TransactionManager?.TopTransaction?.Abort();
                        db?.TransactionManager?.TopTransaction?.Dispose();
                    }
                    if (db?.TransactionManager?.TopTransaction != null)
                        throw new Exception("Cannot dispose before transaction is closed!");
                    db?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
