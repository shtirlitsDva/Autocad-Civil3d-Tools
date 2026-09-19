using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;

namespace NTRExport.CadExtraction
{
    internal interface ICadPipe
    {
        Handle Handle { get; }
        Point2d Start { get; }
        Point2d End { get; }
        int Dn { get; }
        string Material { get; }
        PipeSystemEnum System { get; }
        PipeTypeEnum Type { get; }
        PipeSeriesEnum Series { get; }
        double PipeOd { get; }      // mm
        double PipeId { get; }      // mm
        double PipeWallThk { get; } // mm
        double JacketOd { get; }    // mm
        IEnumerable<Curve2d> GetSegments();
    }    

    internal class CadPort
    {
        public Point2d Position { get; init; }
        public ComponentPortRole Role { get; init; }
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

    internal static class PolylineAdapterFactory
    {
        public static ICadPipe Create(Polyline pl) => new PolylineAdapter(pl);

        private sealed class PolylineAdapter : ICadPipe
        {
            private readonly Polyline _pl;
            public PolylineAdapter(Polyline pl) { _pl = pl; }
            public Handle Handle => _pl.Handle;
            public Point2d Start => new(_pl.StartPoint.X, _pl.StartPoint.Y);
            public Point2d End => new(_pl.EndPoint.X, _pl.EndPoint.Y);
            public int Dn => PipeScheduleV2.GetPipeDN(_pl);
            public string Material => "P235GH";
            public PipeSystemEnum System => PipeScheduleV2.GetPipeSystem(_pl);
            public PipeTypeEnum Type => PipeScheduleV2.GetPipeType(_pl);
            public PipeSeriesEnum Series => PipeScheduleV2.GetPipeSeriesV2(_pl);
            public double PipeOd => PipeScheduleV2.GetPipeOd(_pl);
            public double PipeId => PipeScheduleV2.GetPipeId(_pl);
            public double PipeWallThk => Math.Max(0.0, (PipeOd - PipeId) / 2.0);
            public double JacketOd => PipeScheduleV2.GetPipeKOd(_pl);

            public IEnumerable<Curve2d> GetSegments()
            {
                // Iterate segments between vertices; ignore closing segment
                int n = _pl.NumberOfVertices;
                for (int i = 0; i < n; i++)
                {
                    switch (_pl.GetSegmentType(i))
                    {
                        case SegmentType.Line:
                            yield return _pl.GetLineSegment2dAt(i);                            
                            break;
                        case SegmentType.Arc:
                            yield return _pl.GetArcSegment2dAt(i);                            
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

    // The component's ports as CadPorts, read by the shared ComponentPorts reader
    static class MuffeInternReader
    {
        public static List<CadPort> ReadPorts(BlockReference owner)
        {
            using var tr = owner.Database.TransactionManager.StartOpenCloseTransaction();
            List<CadPort> result = ComponentPorts.Read(owner, tr)
                .Select(p => new CadPort
                {
                    Position = new Point2d(p.Position.X, p.Position.Y),
                    Role = p.Role,
                    Owner = owner.Handle,
                    Tag = p.Tag
                })
                .ToList();
            tr.Commit();
            return result;
        }
    }
}
