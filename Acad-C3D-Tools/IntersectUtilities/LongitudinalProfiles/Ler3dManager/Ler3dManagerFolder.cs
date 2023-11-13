using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles
{
    public class Ler3dManagerFolder : Ler3dManagerBase
    {
        private Dictionary<string, Database> storage = new Dictionary<string, Database>();
        private Dictionary<string, MPolygon> areas = new Dictionary<string, MPolygon>();
        public override void Load(string path)
        {
            var files = Directory.EnumerateFiles(path, "*_3DLER.dwg", SearchOption.TopDirectoryOnly);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var db in storage?.Values)
                {
                    if (db?.TransactionManager?.TopTransaction != null)
                        throw new Exception("Cannot dispose before transaction is closed!");
                    db?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
