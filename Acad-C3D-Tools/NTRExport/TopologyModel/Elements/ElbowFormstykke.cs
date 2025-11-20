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
    internal class ElbowFormstykke : TFitting
    {
        public Point3d TangentPoint { get; }

        public ElbowFormstykke(Handle source, Point3d tangentPoint, PipelineElementType kind)
            : base(source, kind)
        {
            TangentPoint = tangentPoint;
        }

        public ElbowFormstykke(Handle source, PipelineElementType kind)
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
                EmitSoilHint(g, ctx, mainBend.A, flowMain, "Elbow-A");
                EmitSoilHint(g, ctx, mainBend.B, flowMain, "Elbow-B");

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
                    EmitSoilHint(g, ctx, supplyBend.A, FlowRole.Supply, "Elbow-A-Supply");
                    EmitSoilHint(g, ctx, supplyBend.B, FlowRole.Supply, "Elbow-B-Supply");

                    foreach (var port in Ports)
                    {
                        var basePos = port.Node.Pos;
                        g.Members.Add(new RoutedRigid(Source, this)
                        {
                            P1 = new Point3d(basePos.X, basePos.Y, entryZ + zLow),
                            P2 = new Point3d(basePos.X, basePos.Y, entryZ + zUp),
                            Material = Material,
                        });
                    }
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

        protected void EmitSoilHint(RoutedGraph g, RouterContext ctx, Point3d anchor, FlowRole flow, string tag)
        {
            if (ctx.CushionReach <= 1e-6) return;
            g.SoilHints.Add(new SoilHint(
                anchor,
                flow,
                ctx.CushionReach,
                SoilHintKind.Cushion,
                Source,
                includeAnchorMember: false,
                description: tag));
        }
    }

    internal sealed class Bueror : ElbowFormstykke
    {
        public Bueror(Handle source, PipelineElementType kind)
            : base(source, CalculateTangentPoint(source), kind) { }

        private static Point3d CalculateTangentPoint(Handle source)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = source.Go<BlockReference>(db);
            if (br == null)
                throw new Exception($"Received {source} for Buerør! Must be BlockReference!");

            using var tx = db.TransactionManager.StartOpenCloseTransaction();
            var btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

            foreach (ObjectId id in btr)
            {
                if (!id.IsDerivedFrom<Arc>()) continue;

                var arc = id.Go<Arc>(tx);

                // Find tangent intersection using pure math (apply block transform)
                var tangentPoint = GetTangentPoint(arc, br.BlockTransform);

                if (tangentPoint != default)
                {
                    return tangentPoint;
                }

                break;
            }

            throw new Exception(
                $"Buerør: Arc not found for buerør {source}!");
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Buerør);
        }

        internal override void AttachPropertySet()
        {
            base.AttachPropertySet();
        }
    }
}

