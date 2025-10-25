using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using NTRExport.Enums;

namespace NTRExport.TopologyModel
{
    internal static class CadTolerance
    {
        public const double Node = 0.005;
    }

    internal static class MuffeInternReader
    {
        public static List<CadPort> ReadPorts(BlockReference owner)
        {
            var result = new List<CadPort>();
            var db = owner.Database;
            using var tr = db.TransactionManager.StartOpenCloseTransaction();
            var btr = (BlockTableRecord)tr.GetObject(owner.BlockTableRecord, OpenMode.ForRead);

            foreach (ObjectId id in btr)
            {
                if (!id.ObjectClass.IsDerivedFrom(RXClass.GetClass(typeof(BlockReference)))) continue;
                var nested = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                var name = nested.Name ?? string.Empty;
                if (!name.Contains("MuffeIntern", StringComparison.OrdinalIgnoreCase)) continue;

                var wpt = nested.Position.TransformBy(owner.BlockTransform);
                var role =
                    name.Contains("BRANCH", StringComparison.OrdinalIgnoreCase) ? PortRole.Branch :
                    name.Contains("MAIN", StringComparison.OrdinalIgnoreCase) ? PortRole.Main :
                    PortRole.Neutral;

                result.Add(new CadPort(new Point2d(wpt.X, wpt.Y), role));
            }

            tr.Commit();
            return result;
        }
    }

    internal readonly struct CadPort
    {
        public CadPort(Point2d position, PortRole role)
        {
            Position = position;
            Role = role;
        }

        public Point2d Position { get; }
        public PortRole Role { get; }
    }
}

