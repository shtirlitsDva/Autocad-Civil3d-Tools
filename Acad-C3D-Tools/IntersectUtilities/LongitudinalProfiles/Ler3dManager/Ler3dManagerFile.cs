using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.LongitudinalProfiles
{
    public class Ler3dManagerFile : Ler3dManagerBase
    {
        private Database _db;
        private Transaction _tx;
        public override void Load(string path)
        {
            if (!File.Exists(path)) throw new Exception("Ler3d file does not exist!: " + path);

            var db = new Database(false, true);
            db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, false, null);
            _db = db;
            _tx = _db.TransactionManager.StartTransaction();

            if (!IsLoadValid()) throw new Exception("Ler3d load failed!: \n" + path);
        }
        protected override bool IsLoadValid() => _db != null;
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tx.Abort();
                _tx.Dispose();

                while (_db?.TransactionManager?.TopTransaction != null)
                {
                    _db?.TransactionManager?.TopTransaction?.Abort();
                    _db?.TransactionManager?.TopTransaction?.Dispose();
                }
                _db?.Dispose();
            }
            base.Dispose(disposing);
        }
        public override HashSet<Entity> GetIntersectingEntities(Alignment al)
        {
            HashSet<Entity> result = new HashSet<Entity>();

            using (Transaction tx = _db.TransactionManager.StartTransaction())
            {
                var plines = _db.ListOfType<Polyline3d>(tx);
                Plane plane = new Plane();

                foreach (var pl in plines)
                {
                    string type = UtilsDataTables.ReadStringParameterFromDataTable(
                        pl.Layer, CsvData.Get("krydsninger"), "Type", 0);
                    if (type == "IGNORE") continue;

                    using (Point3dCollection p3dcol = new Point3dCollection())
                    {
                        al.IntersectWith(
                            pl,
                            Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                            plane, p3dcol, new IntPtr(0), new IntPtr(0));

                        if (p3dcol.Count > 0) result.Add(pl);
                    }
                }

                tx.Abort();
            }

            return result;
        }
        public override string GetHandle(Entity ent) => ent.Handle.ToString();
        public override bool IsPointWithinPolygon(Entity ent, Point3d p3d) => true;
        public override Entity GetEntityByHandle(string handle)
        { 
            if (handle.Contains(":"))
            {
                var split = handle.Split(':');
                return _db.Go<Entity>(split[2]);
            }

            return _db.Go<Entity>(handle); 
        }
    }
}
