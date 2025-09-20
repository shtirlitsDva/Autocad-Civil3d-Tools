using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System.Globalization;
using System.IO;
using System.Linq;

namespace PipeScheduleV2Tests
{
    internal static class PipeScheduleV2EntityRegistry
    {
        public static void EnsureRegistryFileExists(string path)
        {
            if (File.Exists(path)) return;
            var header = "Key;Layer;ConstWidth;Handle";
            File.WriteAllText(path, header + System.Environment.NewLine, System.Text.Encoding.UTF8);
        }

        public static void EnsureBaselineRegistry(string path)
        {
            var lines = File.ReadAllLines(path).ToList();
            bool exists = lines.Skip(1).Any(l => l.StartsWith("DN20_ENKELT;", System.StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                lines.Add("DN20_ENKELT;FJV-FREM-DN20;0.09;");
                File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
            }
        }

        public static void EnsurePolylinesFromRegistry(Database db, string path)
        {
            if (!File.Exists(path)) return;
            var lines = File.ReadAllLines(path).ToList();
            if (lines.Count <= 1) return;

            bool changed = false;
            using (var tx = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                for (int i = 1; i < lines.Count; i++)
                {
                    var row = lines[i];
                    if (string.IsNullOrWhiteSpace(row)) continue;
                    var parts = row.Split(';');
                    if (parts.Length < 4) continue;
                    string layer = parts[1];
                    if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double cwidth)) continue;
                    string handleStr = parts[3];

                    ObjectId oid = ObjectId.Null;
                    if (!string.IsNullOrWhiteSpace(handleStr))
                    {
                        if (long.TryParse(handleStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hv))
                        {
                            if (db.TryGetObjectId(new Handle(hv), out ObjectId found)) oid = found;
                        }
                    }

                    if (oid == ObjectId.Null || !oid.IsValid)
                    {
                        CreateLayerIfMissing(db, tx, layer);
                        var pl = new Polyline();
                        pl.SetDatabaseDefaults();
                        pl.Layer = layer;
                        pl.ConstantWidth = cwidth;
                        // place procedurally to avoid needing coordinates in registry
                        double sx = i * 20.0;
                        double ex = sx + 10.0;
                        double y = 0.0;
                        pl.AddVertexAt(0, new Point2d(sx, y), 0, 0, 0);
                        pl.AddVertexAt(1, new Point2d(ex, y), 0, 0, 0);
                        btr.AppendEntity(pl);
                        tx.AddNewlyCreatedDBObject(pl, true);
                        parts[3] = pl.Handle.ToString();
                        lines[i] = string.Join(";", parts);
                        changed = true;
                    }
                }
                tx.Commit();
            }

            if (changed) File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
        }

        public static void CreateLayerIfMissing(Database db, Transaction tx, string layer)
        {
            var lt = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layer))
            {
                lt.UpgradeOpen();
                using var ltr = new LayerTableRecord { Name = layer };
                lt.Add(ltr);
                tx.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        public static Polyline? GetPolylineByRegistryKey(string registryPath, string key)
        {
            if (!File.Exists(registryPath)) return null;
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var line = File.ReadAllLines(registryPath)
                .Skip(1)
                .Select(l => l.Split(';'))
                .FirstOrDefault(p => p.Length >= 4 && string.Equals(p[0], key, System.StringComparison.OrdinalIgnoreCase));
            if (line == null) return null;
            string handleStr = line[3];
            if (long.TryParse(handleStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hv))
            {
                if (db.TryGetObjectId(new Handle(hv), out ObjectId id))
                {
                    using var tx = db.TransactionManager.StartOpenCloseTransaction();
                    return tx.GetObject(id, OpenMode.ForRead) as Polyline;
                }
            }
            return null;
        }
        public static string GetRegistryPath()
        {
            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            return System.IO.Path.Combine(baseDir, PipeScheduleV2TestsClass.RegistryFileName);
        }
    }
}


