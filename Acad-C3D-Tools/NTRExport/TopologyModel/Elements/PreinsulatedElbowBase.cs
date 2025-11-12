using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;
using NTRExport.SoilModel;

using static NTRExport.Utils.Utils;

namespace NTRExport.TopologyModel
{
    internal abstract class PreinsulatedElbowBase : ElbowFormstykke
    {
        protected PreinsulatedElbowBase(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.PræisoleretBøjning90gr);
            allowed.Add(PipelineElementType.PræisoleretBøjning45gr);
            allowed.Add(PipelineElementType.PræisoleretBøjningVariabel);
        }

        protected abstract int ThresholdDN { get; }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();

            var ends = Ports.Take(2).ToArray();
            if (ends.Length < 2)
            {
                // Propagate same Z and slope to all other ports
                foreach (var p in Ports)
                {
                    if (ReferenceEquals(p, entryPort)) continue;
                    exits.Add((p, entryZ, entrySlope));
                }
                return exits;
            }

            var a = ends[0].Node.Pos;
            var b = ends[1].Node.Pos;
            var t = TangentPoint;
            var r = (DN <= ThresholdDN ? Geometry.GetBogRadius5D(DN) : Geometry.GetBogRadius3D(DN)) / 1000.0;

            // Solve fillet points a' and b' for a radius r between lines (a↔t) and (b↔t)
            Point2d a2 = a.To2d();
            Point2d b2 = b.To2d();
            Point2d t2 = t.To2d();

            var va = a2 - t2; var vb = b2 - t2;
            if (va.Length < 1e-9 || vb.Length < 1e-9)
            {
                // Propagate same Z and slope to all other ports
                foreach (var p in Ports)
                {
                    if (ReferenceEquals(p, entryPort)) continue;
                    exits.Add((p, entryZ, entrySlope));
                }
                return exits;
            }

            var ua = va.GetNormal();
            var ub = vb.GetNormal();
            var dot = Math.Max(-1.0, Math.Min(1.0, ua.DotProduct(ub)));
            var alpha = Math.Acos(dot);
            var sinHalf = Math.Sin(alpha * 0.5);
            var cosHalf = Math.Cos(alpha * 0.5);
            if (sinHalf < 1e-9)
            {
                // parallel/degenerate - propagate same Z and slope to all other ports
                foreach (var p in Ports)
                {
                    if (ReferenceEquals(p, entryPort)) continue;
                    exits.Add((p, entryZ, entrySlope));
                }
                return exits;
            }

            var l = r * (cosHalf / sinHalf); // R * cot(alpha/2)

            // Check feasibility: tangent points must lie between a↔t and b↔t
            var lenAT = va.Length; var lenBT = vb.Length;
            if (l > lenAT - 1e-9 || l > lenBT - 1e-9)
            {
                // radius too large for available leg length; clamp
                l = Math.Max(0.0, Math.Min(lenAT, lenBT) * 0.5);
            }

            var aPrime2 = new Point2d(t2.X + ua.X * l, t2.Y + ua.Y * l);
            var bPrime2 = new Point2d(t2.X + ub.X * l, t2.Y + ub.Y * l);

            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var mainFlow = Variant.IsTwin ? FlowRole.Return : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return);
            var ltg = LTGMain(Source);

            void EmitFor(double zOffset, FlowRole flow)
            {
                var z = entryZ + zOffset;
                var aPrime = aPrime2.To3d(z);
                var bPrime = bPrime2.To3d(z);
                var aZ = a.Z(z);
                var bZ = b.Z(z);
                var tZ = t.Z(z);

                // a → a'
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = aZ,
                        B = aPrime,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                    }
                );

                // bend a' → b' with PT = t
                g.Members.Add(
                    new RoutedBend(Source, this)
                    {
                        A = aPrime,
                        B = bPrime,
                        T = tZ,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                        Norm = DN <= ThresholdDN ? "" : "EN 10253-2 - Type A"
                    }
                );

                // b' → b
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = bPrime,
                        B = bZ,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                    }
                );
            }

            // Emit main flow
            EmitFor(Variant.IsTwin ? zUp : 0.0, mainFlow);

            // Emit supply for twin
            if (Variant.IsTwin)
            {
                EmitFor(zLow, FlowRole.Supply);
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

    internal sealed class PreinsulatedElbowAbove45deg : PreinsulatedElbowBase
    {
        public PreinsulatedElbowAbove45deg(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override int ThresholdDN => 400;
    }

    internal sealed class PreinsulatedElbowAtOrBelow45deg : PreinsulatedElbowBase
    {
        public PreinsulatedElbowAtOrBelow45deg(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override int ThresholdDN => 200;
    }
}

