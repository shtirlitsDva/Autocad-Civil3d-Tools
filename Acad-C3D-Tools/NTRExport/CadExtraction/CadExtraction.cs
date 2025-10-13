using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;

using NTRExport.Enums;
using NTRExport.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.CadExtraction
{
    internal interface ICadPipe
    {
        Handle Handle {  get; }
        Pt2 Start {  get; }
        Pt2 End { get; }
        int Dn { get; }
        string Material { get; }
    }

    internal class CadPort
    {
        public Pt2 Position { get; init; }
        public PortRole Role { get; init; }
        public Handle Owner { get; init; }
        public string Tag { get; init; } = "";
    }

    internal interface ICadFitting
    {
        Handle Handle { get; }
        ElementKind Kind { get; }    // classify from RealName()
        string RealName();           // stub -> your ext
        string? ReadMaterial();      // stub
        IReadOnlyList<CadPort> GetPorts(); // stub -> your MuffeIntern scan
    }

    internal sealed class CadModel
    {
        public List<ICadPipe> Pipes { get; } = new();
        public List<ICadFitting> Fittings { get; } = new();
    }

    internal static class PolylineAdapterFactory
    {
        public static ICadPipe Create(Polyline pl) => new PolylineAdapter(pl);

        private sealed class PolylineAdapter : ICadPipe
        {
            private readonly Polyline _pl;
            public PolylineAdapter(Polyline pl) { _pl = pl; }
            public Handle Handle => _pl.Handle;
            public Pt2 Start => new(_pl.StartPoint.X, _pl.StartPoint.Y);
            public Pt2 End => new(_pl.EndPoint.X, _pl.EndPoint.Y);
            public int Dn => PipeScheduleV2.GetPipeDN(_pl);
            public string Material => "P235GH";            
        }
    }

    internal static class BlockRefAdapterFactory
    {
        public static ICadFitting Create(BlockReference br) => new BlockRefAdapter(br);

        private sealed class BlockRefAdapter : ICadFitting
        {
            private readonly BlockReference _br;
            public BlockRefAdapter(BlockReference br) { _br = br; }
            public Handle Handle => _br.Handle;

            public ElementKind Kind
            {
                get
                {
                    var n = RealName().ToUpperInvariant();
                    if (n.Contains("TEE") || n.Contains("AFGRSTUDS")) return ElementKind.Tee;
                    if (n.Contains("RED")) return ElementKind.Reducer;
                    if (n.Contains("BOG") || n.Contains("ELBOW") || n.Contains("BEND")) return ElementKind.Bend;
                    return ElementKind.Cap;
                }
            }

            public string RealName()
            {
                try { return _br.RealName(); } // your extension
                catch { return _br.Name; }
            }

            public string? ReadMaterial() => PipeScheduleV2Stub.ReadMaterial(_br); // stub: replace

            public IReadOnlyList<CadPort> GetPorts() => MuffeInternReaderStub.ReadPorts(_br);
        }
    }

    static class PipeScheduleV2Stub
    {
        public static string ReadDn(Entity e) => "DN200";     // replace with your PipeScheduleV2
        public static string? ReadMaterial(Entity e) => null; // replace if needed
    }

    // Finds nested MuffeIntern* blocks and returns world coordinates
    static class MuffeInternReaderStub
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

                result.Add(new CadPort
                {
                    Position = new Pt2(wpt.X, wpt.Y),
                    Role = role,
                    Owner = owner.Handle,
                    Tag = name
                });
            }

            tr.Commit();
            return result;
        }
    }
}
