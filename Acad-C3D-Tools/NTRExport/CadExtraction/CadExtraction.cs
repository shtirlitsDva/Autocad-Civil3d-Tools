using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Geometry;

namespace NTRExport.CadExtraction
{
    internal interface ICadPipe
    {
        Handle Handle { get; }
        Pt2 Start { get; }
        Pt2 End { get; }
        int Dn { get; }
        string Material { get; }
        PipeSystemEnum System { get; }
        PipeTypeEnum Type { get; }
        PipeSeriesEnum Series { get; }
        double PipeOd { get; }      // mm
        double PipeId { get; }      // mm
        double PipeWallThk { get; } // mm
        double JacketOd { get; }    // mm
        IEnumerable<CadPipeSegment> GetSegments();
    }

    internal enum CadSegmentKind { Line, Arc }

    internal struct CadPipeSegment
    {
        public CadSegmentKind Kind { get; init; }
        public Pt2 Start { get; init; }
        public Pt2 End { get; init; }
        public Pt2 Center { get; init; } // only for Arc
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
        PipelineElementType Kind { get; }
        string RealName();
        string? ReadMaterial();
        IReadOnlyList<CadPort> GetPorts();
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
            public PipeSystemEnum System => PipeScheduleV2.GetPipeSystem(_pl);
            public PipeTypeEnum Type => PipeScheduleV2.GetPipeType(_pl);
            public PipeSeriesEnum Series => PipeScheduleV2.GetPipeSeriesV2(_pl);
            public double PipeOd => PipeScheduleV2.GetPipeOd(_pl);
            public double PipeId => PipeScheduleV2.GetPipeId(_pl);
            public double PipeWallThk => Math.Max(0.0, (PipeOd - PipeId) / 2.0);
            public double JacketOd => PipeScheduleV2.GetPipeKOd(_pl);

            public IEnumerable<CadPipeSegment> GetSegments()
            {
                // Iterate segments between vertices; ignore closing segment
                int n = _pl.NumberOfVertices;
                for (int i = 0; i < n - 1; i++)
                {
                    switch (_pl.GetSegmentType(i))
                    {
                        case SegmentType.Line:
                            var ls = _pl.GetLineSegment2dAt(i);
                            yield return new CadPipeSegment
                            {
                                Kind = CadSegmentKind.Line,
                                Start = new Pt2(ls.StartPoint.X, ls.StartPoint.Y),
                                End = new Pt2(ls.EndPoint.X, ls.EndPoint.Y),
                            };
                            break;
                        case SegmentType.Arc:
                            var ar = _pl.GetArcSegment2dAt(i);
                            yield return new CadPipeSegment
                            {
                                Kind = CadSegmentKind.Arc,
                                Start = new Pt2(ar.StartPoint.X, ar.StartPoint.Y),
                                End = new Pt2(ar.EndPoint.X, ar.EndPoint.Y),
                                Center = new Pt2(ar.Center.X, ar.Center.Y)
                            };
                            break;
                    }
                }
            }
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
            public PipelineElementType Kind => _br.GetPipelineType();
            public string RealName()
            {
                try { return _br.RealName(); }
                catch { return _br.Name; }
            }
            public string? ReadMaterial() => "P235GH";
            public IReadOnlyList<CadPort> GetPorts() => MuffeInternReader.ReadPorts(_br);
        }
    }

    // Finds nested MuffeIntern* blocks and returns world coordinates
    static class MuffeInternReader
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
