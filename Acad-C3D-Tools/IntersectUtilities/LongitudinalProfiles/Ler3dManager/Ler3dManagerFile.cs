using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.LongitudinalProfiles
{
    public class Ler3dManagerFile : Ler3dManagerBase
    {
        private Database _db;
        public override void Load(string path)
        {
            if (!File.Exists(path)) throw new Exception("Ler3d file does not exist!: " + path);

            var db = new Database(false, true);
            db.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, false, null);
            _db = db;

            if (!IsLoadValid()) throw new Exception("Ler3d load failed!: \n" + path);
        }
        protected override bool IsLoadValid() => _db != null;
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_db?.TransactionManager?.TopTransaction != null)
                    throw new Exception("Cannot dispose before transaction is closed!");
                _db?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
