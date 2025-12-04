using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;
using NTRExport.SoilModel;

using static IntersectUtilities.UtilsCommon.Utils;
using static NTRExport.Utils.Utils;

namespace NTRExport.TopologyModel
{
    internal class ElbowFromPolyArc : TFitting
    {
        public Point3d TangentPoint { get; }

        public ElbowFromPolyArc(Handle source, Point3d tangentPoint, PipelineElementType kind)
            : base(source, kind)
        {
            TangentPoint = tangentPoint;
        }

        public ElbowFromPolyArc(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = source.Go<BlockReference>(db);
            if (br == null)
                throw new Exception($"Received {source} for ElbowFormstykke!");

            TangentPoint = br.Position;
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Kedelrørsbøjning);
            allowed.Add(PipelineElementType.Bøjning45gr);
            allowed.Add(PipelineElementType.Bøjning30gr);
            allowed.Add(PipelineElementType.Bøjning15gr);
        }

        internal override void AttachPropertySet()
        {            
            var ntr = new NtrData(_entity);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();

            // Emit elbow with constant Z across this element; offsets applied as in Route()
            var ends = Ports.Take(2).ToArray();
            if (ends.Length >= 2)
            {
            var a = ends[0].Node.Pos;
            var b = ends[1].Node.Pos;
            var t = TangentPoint;
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);

                var flowMain = Variant.IsTwin ? FlowRole.Return : Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;

                var mainBend = new RoutedBend(Source, this)
                {
                    A = a.Z(zUp + entryZ),
                    B = b.Z(zUp + entryZ),
                    T = t.Z(zUp + entryZ),
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flowMain,
                    LTG = LTGMain(Source),
                };
                g.Members.Add(mainBend);

                if (Variant.IsTwin)
                {
                    var supplyBend = new RoutedBend(Source, this)
                    {
                        A = a.Z(zLow + entryZ),
                        B = b.Z(zLow + entryZ),
                        T = t.Z(zLow + entryZ),
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    };
                    g.Members.Add(supplyBend);
                }
            }

            // Propagate same Z and slope to all other ports
            foreach (var p in Ports)
            {
                if (ReferenceEquals(p, entryPort)) continue;
                exits.Add((p, entryZ, entrySlope));
            }
            return exits;
        }

    }    
}