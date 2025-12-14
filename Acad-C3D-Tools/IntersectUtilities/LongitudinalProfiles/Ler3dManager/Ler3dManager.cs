using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using static IntersectUtilities.UtilsCommon.Utils;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.LongitudinalProfiles
{
    public interface ILer3dManager
    {
        void Dispose(bool disposing);
        HashSet<Entity> GetIntersectingEntities(Alignment al);
        string GetHandle(Entity ent);
        bool IsPointWithinPolygon(Entity ent, Point3d p3d);
        Entity GetEntityByHandle(string handle);
        HashSet<Database> GetDatabases();
        /// <summary>
        /// Gets the database by filename (without extension).
        /// For Ler3dManagerFile, returns the single database regardless of filename.
        /// For Ler3dManagerFolder, returns the database matching the filename.
        /// </summary>
        Database? GetDatabaseByFileName(string fileNameWithoutExtension);
        /// <summary>
        /// Gets the database from the full ID string (e.g. CogoPoint.PointName).
        /// For Ler3dManagerFile, returns the single database.
        /// For Ler3dManagerFolder, parses the string to determine which database to return.
        /// </summary>
        Database? GetDatabaseByIdString(string idString);
    }
    public abstract class Ler3dManagerBase : ILer3dManager, IDisposable
    {
        protected bool _disposed = false;
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
        public abstract HashSet<Database> GetDatabases();
        public abstract Database? GetDatabaseByFileName(string fileNameWithoutExtension);
        public abstract Database? GetDatabaseByIdString(string idString);
    }
    public class Ler3dManagerFile : Ler3dManagerBase
    {
        private Database _db;
        private Transaction _tx;
        public Ler3dManagerFile(Database db)
        {
            _db = db;
            _tx = _db.TransactionManager.StartTransaction();
            if (!IsLoadValid()) throw new Exception("Ler3d load failed!: \n" + db.Filename);
        }
        protected override bool IsLoadValid() => _db != null;
        public override void Dispose(bool disposing)
        {
            if (_disposed) return;
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
            Polyline alPline = al.GetPolyline().Go<Polyline>(
                al.Database.TransactionManager.TopTransaction);

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

                    List<Point3d> p3dcol = new List<Point3d>();
                    al.IntersectWithValidation(pl, p3dcol);
                    if (p3dcol.Count > 0) result.Add(pl);
                }

                tx.Abort();
            }

            alPline.UpgradeOpen();
            alPline.Erase(true);

            return result;
        }
        public override string GetHandle(Entity ent) => ent.Handle.ToString();
        public override bool IsPointWithinPolygon(Entity ent, Point3d p3d) => true;
        public override Entity GetEntityByHandle(string handle)
        {
            if (handle.Contains(":"))
            {
                var split = handle.Split(':');
                string hndl = split[2];
                if (hndl.Contains("_")) hndl = hndl.Split('_')[0];
                return _db.Go<Entity>(hndl);
            }

            return _db.Go<Entity>(handle);
        }
        public override HashSet<Database> GetDatabases() => new HashSet<Database> { _db };
        public override Database? GetDatabaseByFileName(string fileNameWithoutExtension) => _db;
        public override Database? GetDatabaseByIdString(string idString) => _db;
    }
    public class Ler3dManagerFolder : Ler3dManagerBase
    {
        private Dictionary<string, Database> storage = new Dictionary<string, Database>();
        private Dictionary<string, Transaction> trans = new Dictionary<string, Transaction>();
        private Dictionary<string, Polygon> areas = new Dictionary<string, Polygon>();
        public Ler3dManagerFolder(IEnumerable<Database> databases)
        {
            if (databases.Count() < 2)
                throw new Exception($"Less files than expected! At least 2 files expected.");

            foreach (Database db in databases)
            {
                var name = Path.GetFileNameWithoutExtension(db.Filename);
                storage.Add(name, db);

                Transaction tx = db.TransactionManager.StartTransaction();
                MPolygon? mpg = db.ListOfType<MPolygon>(tx).FirstOrDefault();

                if (mpg == null)
                {
                    prdDbg("No MPolygon found in " + db.Filename + "! Civil will crash in:");
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(1000);
                    for (int i = 5; i == -1; i--)
                    {
                        prdDbg($"T minus {i}");
                        System.Windows.Forms.Application.DoEvents();
                        System.Threading.Thread.Sleep(1000);
                    }
                    prdDbg("CRASHING NOW!");
                    System.Windows.Forms.Application.DoEvents();
                    System.Threading.Thread.Sleep(1000);

                    this.Dispose(true);
                    throw new Exception($"No MPolygon found in {db.Filename}!");
                }
                trans.Add(name, tx);
                areas.Add(name, NTS.NTSConversion.ConvertMPolygonToNTSPolygon(mpg));
            }

            if (!IsLoadValid()) throw new Exception("Ler3d load failed!");
        }        
        protected override bool IsLoadValid() =>
            storage != null &&
            storage.Count > 0 &&
            !storage.Values.Any(x => x == null);
        public override void Dispose(bool disposing)
        {
            if (_disposed) return;
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

                        List<Point3d> p3dcol = new List<Point3d>();
                        al.IntersectWithValidation(pl, p3dcol);
                        if (p3dcol.Count > 0) result.Add(pl);
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
                if (hndl.Contains("_")) hndl = hndl.Split('_')[0];
                if (!storage.ContainsKey(area)) throw new Exception($"Area {area} not found in Ler3dManager!");
                var db = storage[area];
                return db.Go<Entity>(hndl);
            }
            else throw new Exception(
                $"ERR::2024:03:05:001\n" +
                $"Received handle {handle} which does not match the regex!");
        }
        public override HashSet<Database> GetDatabases() => [.. storage.Values];
        public override Database? GetDatabaseByFileName(string fileNameWithoutExtension)
        {
            if (storage.TryGetValue(fileNameWithoutExtension, out var db))
                return db;
            return null;
        }
        public override Database? GetDatabaseByIdString(string idString)
        {
            if (rgx.IsMatch(idString))
            {
                var match = rgx.Match(idString);
                string area = match.Groups["AREA"].Value;
                if (storage.TryGetValue(area, out var db))
                    return db;
            }
            return null;
        }
    }
    public static class Ler3dManagerFactory
    {
        public static ILer3dManager LoadLer3d(DataManager dm)
        {
            var dbs = dm.Ler();

            if (dbs.Count() == 1)
            {
                UtilsCommon.Utils.prdDbg("Loading Ler 3d from single file!");
                return new Ler3dManagerFile(dbs.First());
            }
            else if (dbs.Count() > 1)
            {
                UtilsCommon.Utils.prdDbg("Loading Ler 3d from a collection of files!");
                return new Ler3dManagerFolder(dbs);
            }
            else
            {
                throw new Exception($"Ler3d cannot be loaded: {dm.StierKey}");
            }
        }
        //public static ILer3dManager LoadLer3d(string path)
        //{
        //    if (File.Exists(path))
        //    {
        //        UtilsCommon.Utils.prdDbg("Loading Ler 3d from single file!");
        //        if (Path.GetExtension(path).ToLower() == ".dwg")
        //        {
        //            var obj = new Ler3dManagerFile();
        //            obj.Load(path);
        //            return obj;
        //        }

        //        else throw new Exception("Ler3d has wrong extension: " + path);
        //    }
        //    else if (Directory.Exists(path))
        //    {
        //        UtilsCommon.Utils.prdDbg("Loading Ler 3d from a collection of files!");
        //        var obj = new Ler3dManagerFolder();
        //        obj.Load(path);
        //        return obj;
        //    }
        //    else
        //    {
        //        throw new Exception("Ler3d info not found: " + path);
        //    }
        //}
    }
}