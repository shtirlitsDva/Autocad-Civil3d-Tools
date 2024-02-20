using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using NetTopologySuite.Geometries;
using IntersectUtilities.UtilsCommon;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.LongitudinalProfiles
{
    public interface ILer3dManager
    {
        void Load(string path);
        void Dispose(bool disposing);
        HashSet<Entity> GetIntersectingEntities(Alignment al);
        string GetHandle(Entity ent);
        bool IsPointWithinPolygon(Entity ent, Point3d p3d);
        Entity GetEntityByHandle(string handle);
    }
    public abstract class Ler3dManagerBase : ILer3dManager, IDisposable
    {
        private bool _disposed = false;
        public abstract void Load(string path);
        protected abstract bool IsLoadValid();
        public virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        public abstract HashSet<Entity> GetIntersectingEntities(Alignment al);
        public abstract string GetHandle(Entity ent);
        public abstract bool IsPointWithinPolygon(Entity ent, Point3d p3d);
        public abstract Entity GetEntityByHandle(string handle);
    }
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
                        pl.Layer, CsvData.Kryds, "Type", 0);
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
    public class Ler3dManagerFolder : Ler3dManagerBase
    {
        private Dictionary<string, Database> storage = new Dictionary<string, Database>();
        private Dictionary<string, Transaction> trans = new Dictionary<string, Transaction>();
        private Dictionary<string, Polygon> areas = new Dictionary<string, Polygon>();
        public override void Load(string path)
        {
            var files = Directory.EnumerateFiles(path, "*_3DLER.dwg", SearchOption.TopDirectoryOnly);

            if (files.Count() == 0)
                throw new Exception($"No files with search mask \"*_3DLER.dwg\" found in {path}!");

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var db = new Database(false, true);
                db.ReadDwgFile(file, FileOpenMode.OpenForReadAndReadShare, true, "");
                storage.Add(name, db);

                Transaction tx = db.TransactionManager.StartTransaction();
                MPolygon mpg = db.ListOfType<MPolygon>(tx).FirstOrDefault();

                if (mpg == null)
                    throw new Exception($"No MPolygon found in {file}!");

                trans.Add(name, tx);
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
                foreach (Transaction item in trans?.Values)
                {
                    if (item != null)
                    {
                        item.Abort();
                        item.Dispose();
                    }
                }

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
        public override HashSet<Entity> GetIntersectingEntities(Alignment al)
        {
            HashSet<Entity> result = new HashSet<Entity>();

            var pline = al.GetPolyline().Go<Polyline>(
                al.Database.TransactionManager.TopTransaction);
            var line = NTS.NTSConversion.ConvertPlineToNTSLineString(pline);
            pline.UpgradeOpen();
            pline.Erase(true);
            Plane plane = new Plane();

            foreach (var entry in areas)
            {
                if (entry.Value.Intersects(line))
                {
                    var db = storage[entry.Key];
                    //var tx = db.TransactionManager.TopTransaction;
                    var tx = trans[entry.Key];
                    if (tx == null) db.TransactionManager.StartTransaction();
                    var plines = db.ListOfType<Polyline3d>(tx);
                    foreach (var pl in plines)
                    {
                        string type = UtilsDataTables.ReadStringParameterFromDataTable(
                            pl.Layer, CsvData.Kryds, "Type", 0);
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
                }
            }

            return result;
        }
        public override string GetHandle(Entity ent)
        {
            Database db = ent.Database;
            string filename = Path.GetFileName(db.Filename);
            foreach (var item in storage)
            {
                if (item.Value.Filename == db.Filename)
                    return filename + ":" + ent.Handle.ToString();
            }
            throw new Exception($"Entitys' {ent.Handle}\nDB {db.Filename}" +
                $"\nnot found in GetHandle!");
        }
        public override bool IsPointWithinPolygon(Entity ent, Point3d p3d)
        {
            Database db = ent.Database;
            foreach (var item in storage)
            {
                if (item.Value.Filename == db.Filename)
                {
                    string area = item.Key;
                    var polygon = areas[area];
                    return polygon.Contains(
                        new NetTopologySuite.Geometries.Point(p3d.X, p3d.Y));
                }
            }
            throw new Exception($"Entitys' {ent.Handle}\nDB {db.Filename}" +
                $"\nnot found in IsPointWithinPolygon!");
        }
        private Regex rgx = new Regex(@"^(?<PROJECT>[^:]+):(?<ETAPE>[^:]+):(?<AREA>[^:]+?)(.dwg):(?<HANDLE>\w+)");
        public override Entity GetEntityByHandle(string handle)
        {
            if (rgx.IsMatch(handle))
            {
                var match = rgx.Match(handle);
                string area = match.Groups["AREA"].Value;
                string hndl = match.Groups["HANDLE"].Value;
                if (!storage.ContainsKey(area)) throw new Exception($"Area {area} not found in Ler3dManager!");
                var db = storage[area];
                return db.Go<Entity>(hndl);
            }
            else return null;
        }
    }
    public static class Ler3dManagerFactory
    {
        public static ILer3dManager LoadLer3d(string path)
        {
            if (File.Exists(path))
            {
                UtilsCommon.Utils.prdDbg("Loading Ler 3d from single file!");
                if (Path.GetExtension(path).ToLower() == ".dwg")
                {
                    var obj = new Ler3dManagerFile();
                    obj.Load(path);
                    return obj;
                }

                else throw new Exception("Ler3d has wrong extension: " + path);
            }
            else if (Directory.Exists(path))
            {
                UtilsCommon.Utils.prdDbg("Loading Ler 3d from a collection of files!");
                var obj = new Ler3dManagerFolder();
                obj.Load(path);
                return obj;
            }
            else
            {
                throw new Exception("Ler3d info not found: " + path);
            }
        }
    }
}