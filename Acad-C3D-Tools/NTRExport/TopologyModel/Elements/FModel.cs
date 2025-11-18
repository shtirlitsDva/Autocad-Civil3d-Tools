using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.Enums;
using NTRExport.Routing;
using NTRExport.TopologyModel.Data;
using static NTRExport.Utils.Utils;

namespace NTRExport.TopologyModel
{
    internal sealed class FModel : TFitting
    {
        public FModel(Handle source)
            : base(source, PipelineElementType.F_Model) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.F_Model);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var assignment = ClassifyPorts(topo);
            var solution = ResolveVariant(assignment);            
            solution = solution.WithZOffset(entryZ);

            EmitGeometry(g, solution);

            var exits = ComputeExits(entryPort, entryZ, solution);
            return exits;
        }

        private PortAssignment ClassifyPorts(Topology topo)
        {
            var twinPorts = Ports.Where(p => p.Role == PortRole.Neutral).ToList();
            if (twinPorts.Count != 1)
                throw new InvalidOperationException($"FModel {Source}: expected exactly one neutral (twin) port.");

            var bondedPorts = Ports.Where(p => p.Role != PortRole.Neutral).ToList();
            if (bondedPorts.Count != 2)
                throw new InvalidOperationException($"FModel {Source}: expected exactly two bonded ports.");

            FlowRole InferFlow(TPort port) => topo.FindRoleFromPort(this, port);

            var twinInstance = new PortInstance(twinPorts[0], FlowRole.Unknown);
            var bondedInstances = bondedPorts
                .Select(p => new PortInstance(p, InferFlow(p)))
                .ToList();

            return new PortAssignment(
                twinInstance,
                bondedInstances[0],
                bondedInstances[1]);
        }

        private VariantSolution ResolveVariant(PortAssignment assignment)
        {
            VariantSolution? bestSolution = null;
            double bestError = double.MaxValue;

            foreach (var variant in FModelCatalog.EnumerateVariants(DN))
            {
                foreach (var candidate in assignment.ResolveCandidates())
                {
                    if (TryBuildTransform(variant, candidate, out var frame, out var bondedError))
                    {
                        if (bondedError < bestError)
                        {
                            bestError = bondedError;
                            bestSolution = new VariantSolution(variant, frame, candidate, 0.0);
                        }
                    }
                }
            }

            if (bestSolution.HasValue)
            {
                return bestSolution.Value;
            }

            throw new InvalidOperationException($"FModel {Source}: unable to match catalog variant for DN {DN}.");
        }

        private bool TryBuildTransform(FModelCatalog.VariantData variant, ResolvedPorts resolved, out FrameTransform transform, out double bondedError)
        {
            var canonTwinSup = variant.TwinPorts[FlowRole.Supply];
            var canonTwinRet = variant.TwinPorts[FlowRole.Return];
            var canonBondSup = variant.BondPorts[FlowRole.Supply];
            var canonBondRet = variant.BondPorts[FlowRole.Return];

            var canonFrame = BuildFrame(
                canonTwinSup.Position,
                canonTwinRet.Position,
                canonBondSup.Position,
                canonBondRet.Position);
            var actualFrame = BuildActualFrame(
                resolved.Twin.Position,
                resolved.BondedSupply.Position,
                resolved.BondedReturn.Position);

            double canonDist = (canonBondSup.Position - canonFrame.Origin).Length;
            double actualDist = (resolved.BondedSupply.Position - actualFrame.Origin).Length;
            if (canonDist < 1e-6 || actualDist < 1e-6)
            {
                transform = default;
                bondedError = double.PositiveInfinity;
                return false;
            }

            const double scale = 1.0;
            transform = new FrameTransform(canonFrame, actualFrame, scale);

            var predictedTwinSupply = transform.MapPoint(canonTwinSup.Position);
            var predictedTwinReturn = transform.MapPoint(canonTwinRet.Position);
            var predictedBondSupply = transform.MapPoint(canonBondSup.Position);
            var predictedBondReturn = transform.MapPoint(canonBondRet.Position);

            //DumpComparisonTable(variant.Variant, resolved, predictedTwinSupply, predictedTwinReturn, predictedBondSupply, predictedBondReturn);

            var bondSupplyError = predictedBondSupply.DistanceTo(resolved.BondedSupply.Position);
            var bondReturnError = predictedBondReturn.DistanceTo(resolved.BondedReturn.Position);
            bondedError = bondSupplyError + bondReturnError;

            if (bondSupplyError > 0.05 || bondReturnError > 0.05)
            {
                transform = default;
                bondedError = double.PositiveInfinity;
                return false;
            }

            return true;
        }

        private void DumpComparisonTable(
            string variantLabel,
            ResolvedPorts resolved,
            Point3d predictedTwinSupply,
            Point3d predictedTwinReturn,
            Point3d predictedBondSupply,
            Point3d predictedBondReturn)
        {
            prdDbg($"FModel {Source}: variant {variantLabel} point comparison table (world units).");
            prdDbg("  Role          | Actual (x,y,z)         | Predicted (x,y,z)      | Error (m)");

            void LogRow(string role, Point3d actual, Point3d predicted)
            {
                var error = actual.DistanceTo(predicted);
                prdDbg($"  {role.PadRight(12)}| {DescribePoint(actual).PadRight(23)} | {DescribePoint(predicted).PadRight(21)} | {error:0.###}");
            }

            var twinActual = resolved.Twin.Position;
            LogRow("Twin Supply", twinActual, predictedTwinSupply);
            LogRow("Twin Return", twinActual, predictedTwinReturn);
            LogRow("Bond Supply", resolved.BondedSupply.Position, predictedBondSupply);
            LogRow("Bond Return", resolved.BondedReturn.Position, predictedBondReturn);
        }

        private static LocalFrame BuildFrame(
            Point3d twinSupply,
            Point3d twinReturn,
            Point3d bondedSupply,
            Point3d bondedReturn)
        {
            var origin = new Point3d(
                0.5 * (twinSupply.X + twinReturn.X),
                0.5 * (twinSupply.Y + twinReturn.Y),
                0.5 * (twinSupply.Z + twinReturn.Z));

            var z = (twinReturn - twinSupply);
            z = z.Length < 1e-9 ? Vector3d.ZAxis : z.GetNormal();
            if (z.DotProduct(Vector3d.ZAxis) < 0.0)
                z = z.Negate();

            Vector3d MakeHorizontal(Vector3d v) => v - z.MultiplyBy(v.DotProduct(z));

            var yRaw = MakeHorizontal(bondedSupply - bondedReturn);
            if (yRaw.Length < 1e-9)
                yRaw = MakeHorizontal(bondedSupply - origin);
            if (yRaw.Length < 1e-9)
                yRaw = Vector3d.YAxis;
            var y = yRaw.GetNormal();

            var x = y.CrossProduct(z);
            if (x.Length < 1e-9)
            {
                x = Vector3d.XAxis;
            }
            else
            {
                x = x.GetNormal();
            }

            y = z.CrossProduct(x);
            if (y.Length < 1e-9)
                y = Vector3d.YAxis;
            else
                y = y.GetNormal();

            return new LocalFrame(origin, x, y, z);
        }

        private void EmitGeometry(RoutedGraph g, VariantSolution solution)
        {
            foreach (var prim in solution.Variant.Primitives)
            {
                var a = solution.MapPoint(prim.P1);
                var b = solution.MapPoint(prim.P2);
                if (prim.Kind == FModelCatalog.PrimitiveKind.Pipe)
                {
                    g.Members.Add(new RoutedStraight(Source, this)
                    {
                        A = a,
                        B = b,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = prim.Flow,
                        LTG = LTGMain(Source),
                    });
                }
                else
                {
                    var t = prim.Centre ?? throw new InvalidOperationException("Elbow primitive missing centre point.");
                    g.Members.Add(new RoutedBend(Source, this)
                    {
                        A = a,
                        B = b,
                        T = solution.MapPoint(t),
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = prim.Flow,
                        LTG = LTGMain(Source),
                    });
                }
            }
        }

        private List<(TPort exitPort, double exitZ, double exitSlope)> ComputeExits(
            TPort entryPort,
            double entryZ,
            VariantSolution solution)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();
            var assignment = solution.Assignment;

            if (ReferenceEquals(entryPort, assignment.Twin.Port))
            {
                AppendBondedExit(assignment.BondedSupply, FlowRole.Supply);
                AppendBondedExit(assignment.BondedReturn, FlowRole.Return);
            }
            else if (ReferenceEquals(entryPort, assignment.BondedSupply.Port))
            {
                AppendTwinExit(FlowRole.Supply);
                //AppendBondedExit(assignment.BondedReturn, FlowRole.Return);
            }
            else if (ReferenceEquals(entryPort, assignment.BondedReturn.Port))
            {
                AppendTwinExit(FlowRole.Return);
                //AppendBondedExit(assignment.BondedSupply, FlowRole.Supply);
            }
            else
            {
                throw new InvalidOperationException("Entry port not part of FModel.");
            }

            return exits;

            void AppendBondedExit(PortInstance bonded, FlowRole lane)
            {
                var tangent = solution.GetInwardTangent(bonded.Port, lane);
                var outward = tangent.Negate();
                var slope = ComputeSlope(outward);                
                exits.Add((bonded.Port, entryZ, slope));
            }

            void AppendTwinExit(FlowRole lane)
            {
                var twinPort = assignment.Twin.Port;
                var tangent = solution.GetInwardTangent(twinPort, lane);
                var outward = tangent.Negate();
                var slope = ComputeSlope(outward);                
                exits.Add((twinPort, entryZ, slope));
            }
        }

        private static double ComputeSlope(Vector3d dir)
        {
            var horiz = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            if (horiz < 1e-9) return 0.0;
            return dir.Z / horiz;
        }

        private static LocalFrame BuildActualFrame(
            Point3d twinCenter,
            Point3d bondedSupply,
            Point3d bondedReturn)
        {
            var z = Vector3d.ZAxis;

            Vector3d MakeHorizontal(Vector3d v) => v - z.MultiplyBy(v.DotProduct(z));

            var yRaw = MakeHorizontal(bondedSupply - bondedReturn);
            if (yRaw.Length < 1e-9)
                yRaw = MakeHorizontal(bondedSupply - twinCenter);
            if (yRaw.Length < 1e-9)
                yRaw = Vector3d.YAxis;
            var y = yRaw.GetNormal();

            var x = y.CrossProduct(z);
            if (x.Length < 1e-9)
            {
                x = Vector3d.XAxis;
            }
            else
            {
                x = x.GetNormal();
            }

            y = z.CrossProduct(x);
            if (y.Length < 1e-9)
                y = Vector3d.YAxis;
            else
                y = y.GetNormal();

            return new LocalFrame(twinCenter, x, y, z);
        }

        #region Helper types

        private readonly record struct PortInstance(TPort Port, FlowRole Flow)
        {
            public Point3d Position => Port.Node.Pos;
            public PortInstance WithFlow(FlowRole flow) => new(Port, flow);
        }

        private readonly record struct PortAssignment(
            PortInstance Twin,
            PortInstance BondedA,
            PortInstance BondedB)
        {
            public IEnumerable<ResolvedPorts> ResolveCandidates()
            {
                var a = BondedA;
                var b = BondedB;

                bool aSupply = a.Flow == FlowRole.Supply;
                bool aReturn = a.Flow == FlowRole.Return;
                bool bSupply = b.Flow == FlowRole.Supply;
                bool bReturn = b.Flow == FlowRole.Return;

                if (aSupply && bReturn)
                {
                    yield return new ResolvedPorts(Twin, a, b);
                    yield break;
                }

                if (bSupply && aReturn)
                {
                    yield return new ResolvedPorts(Twin, b, a);
                    yield break;
                }

                if (aSupply)
                {
                    yield return new ResolvedPorts(Twin, a, b.WithFlow(FlowRole.Return));
                    yield break;
                }

                if (aReturn)
                {
                    yield return new ResolvedPorts(Twin, b.WithFlow(FlowRole.Supply), a);
                    yield break;
                }

                if (bSupply)
                {
                    yield return new ResolvedPorts(Twin, b, a.WithFlow(FlowRole.Return));
                    yield break;
                }

                if (bReturn)
                {
                    yield return new ResolvedPorts(Twin, a.WithFlow(FlowRole.Supply), b);
                    yield break;
                }

                yield return new ResolvedPorts(Twin, a.WithFlow(FlowRole.Supply), b.WithFlow(FlowRole.Return));
                yield return new ResolvedPorts(Twin, b.WithFlow(FlowRole.Supply), a.WithFlow(FlowRole.Return));
            }
        }

        private readonly record struct ResolvedPorts(
            PortInstance Twin,
            PortInstance BondedSupply,
            PortInstance BondedReturn);

        private readonly record struct LocalFrame(Point3d Origin, Vector3d X, Vector3d Y, Vector3d Z);

        private readonly struct FrameTransform
        {
            private readonly LocalFrame _canon;
            private readonly LocalFrame _actual;
            private readonly double _scale;

            public FrameTransform(LocalFrame canon, LocalFrame actual, double scale)
            {
                _canon = canon;
                _actual = actual;
                _scale = scale;
            }

            public Point3d MapPoint(Point3d point)
            {
                var coords = Decompose(point - _canon.Origin, _canon);
                return _actual.Origin
                    + _actual.X.MultiplyBy(coords.X * _scale)
                    + _actual.Y.MultiplyBy(coords.Y * _scale)
                    + _actual.Z.MultiplyBy(coords.Z * _scale);
            }

            public Vector3d MapVector(Vector3d vector)
            {
                var coords = Decompose(vector, _canon);
                return _actual.X.MultiplyBy(coords.X * _scale)
                    + _actual.Y.MultiplyBy(coords.Y * _scale)
                    + _actual.Z.MultiplyBy(coords.Z * _scale);
            }

            private static Vector3d Decompose(Vector3d v, LocalFrame frame) =>
                new(
                    v.DotProduct(frame.X),
                    v.DotProduct(frame.Y),
                    v.DotProduct(frame.Z));
        }

        private record struct VariantSolution(
            FModelCatalog.VariantData Variant,
            FrameTransform Transform,
            ResolvedPorts Assignment,
            double ZOffset)
        {
            public VariantSolution WithZOffset(double offset) => this with { ZOffset = offset };

            public Point3d MapPoint(Point3d point) => AddZ(Transform.MapPoint(point));

            public Vector3d GetInwardTangent(TPort port, FlowRole lane)
            {
                if (ReferenceEquals(port, Assignment.Twin.Port))
                {
                    if (lane != FlowRole.Supply && lane != FlowRole.Return)
                        throw new InvalidOperationException("Twin tangent requires supply or return lane.");
                    return Transform.MapVector(Variant.TwinPorts[lane].Tangent);
                }

                if (ReferenceEquals(port, Assignment.BondedSupply.Port))
                {
                    if (lane != FlowRole.Supply && lane != FlowRole.Unknown)
                        throw new InvalidOperationException("Bonded supply lane mismatch.");
                    return Transform.MapVector(Variant.BondPorts[FlowRole.Supply].Tangent);
                }

                if (ReferenceEquals(port, Assignment.BondedReturn.Port))
                {
                    if (lane != FlowRole.Return && lane != FlowRole.Unknown)
                        throw new InvalidOperationException("Bonded return lane mismatch.");
                    return Transform.MapVector(Variant.BondPorts[FlowRole.Return].Tangent);
                }

                throw new InvalidOperationException("Missing tangent for port.");
            }

            public double GetPortElevation(TPort port, FlowRole lane) =>
                GetPortPoint(port, lane).Z;

            public Point3d GetPortPoint(TPort port, FlowRole lane)
            {
                if (ReferenceEquals(port, Assignment.Twin.Port))
                {
                    if (lane != FlowRole.Supply && lane != FlowRole.Return)
                        throw new InvalidOperationException("Twin point requires supply or return lane.");
                    return MapPoint(Variant.TwinPorts[lane].Position);
                }

                if (ReferenceEquals(port, Assignment.BondedSupply.Port))
                    return MapPoint(Variant.BondPorts[FlowRole.Supply].Position);

                if (ReferenceEquals(port, Assignment.BondedReturn.Port))
                    return MapPoint(Variant.BondPorts[FlowRole.Return].Position);

                throw new InvalidOperationException("Missing point for port.");
            }

            private Point3d AddZ(Point3d p) => new(p.X, p.Y, p.Z + ZOffset);
        }

        private string DescribePoint(Point3d pt) =>
            $"({pt.X:0.###},{pt.Y:0.###},{pt.Z:0.###})";

        #endregion
    }
}
