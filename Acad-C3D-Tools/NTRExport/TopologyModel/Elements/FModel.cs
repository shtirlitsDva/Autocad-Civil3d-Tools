using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.Enums;
using NTRExport.Routing;
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
            var axes = BuildAxes(assignment);
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var radius = Geometry.GetBogRadius3D(DN) / 1000.0;

            var supplyRun = BuildPipeRun(assignment, axes, radius, assignment.BondedSupply, zLow, entryZ);
            var returnRun = BuildPipeRun(assignment, axes, radius, assignment.BondedReturn, zUp, entryZ);

            var members = new List<RoutedMember>();
            EmitPipeRun(members, supplyRun);
            EmitPipeRun(members, returnRun);
            AddRigidMembers(members);

            foreach (var member in members)
                g.Members.Add(member);

            return ComputeExits(entryPort, supplyRun, returnRun, assignment.Twin.Port, entryZ, entrySlope);
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

            var resolved = ResolveBondedPorts(
                twinInstance,
                bondedInstances[0],
                bondedInstances[1]);

            return new PortAssignment(
                resolved.Twin,
                resolved.BondedSupply,
                resolved.BondedReturn);
        }

        private void EmitPipeRun(List<RoutedMember> members, PipeRun run)
        {
            const double tol = 1e-6;
            var ltg = LTGMain(Source);

            if ((run.FirstStraightEnd - run.TwinStart).Length > tol)
            {
                members.Add(CreateStraight(run.TwinStart, run.FirstStraightEnd, run.Flow, ltg));
            }

            members.Add(CreateBend(run.FirstStraightEnd, run.JointPoint, run.TangentFirst, run.Flow, ltg));
            members.Add(CreateBend(run.JointPoint, run.FinalStraightStart, run.TangentSecond, run.Flow, ltg));

            if ((run.BondedPoint - run.FinalStraightStart).Length > tol)
            {
                members.Add(CreateStraight(run.FinalStraightStart, run.BondedPoint, run.Flow, ltg));
            }
        }

        private RoutedStraight CreateStraight(Point3d a, Point3d b, FlowRole flow, string ltg) =>
            new(Source, this)
            {
                A = a,
                B = b,
                DN = DN,
                Material = Material,
                DnSuffix = Variant.DnSuffix,
                FlowRole = flow,
                LTG = ltg,
            };

        private RoutedBend CreateBend(Point3d a, Point3d b, Point3d t, FlowRole flow, string ltg) =>
            new(Source, this)
            {
                A = a,
                B = b,
                T = t,
                DN = DN,
                Material = Material,
                DnSuffix = Variant.DnSuffix,
                FlowRole = flow,
                LTG = ltg,
            };

        private void AddRigidMembers(List<RoutedMember> members)
        {
            const double tol = 0.001;
            var straights = members.OfType<RoutedStraight>().ToList();
            var pairs = straights
                .SelectMany((s1, i) => straights.Skip(i + 1).Select(s2 => (s1, s2)))
                .Where(pair => AreVerticalPair(pair.s1, pair.s2, tol));

            foreach (var (s1, s2) in pairs)
            {
                var shorter = s1.Length < s2.Length ? s1 : s2;
                var longer = s1.Length < s2.Length ? s2 : s1;
                var midpoint = shorter.A.MidPoint(shorter.B);
                var otherEndZ = longer.A.Z;

                members.Add(new RoutedRigid(Source, this)
                {
                    P1 = midpoint,
                    P2 = new Point3d(midpoint.X, midpoint.Y, otherEndZ),
                    Material = Material,
                });
            }
        }

        private static bool AreVerticalPair(RoutedStraight s1, RoutedStraight s2, double tol)
        {
            return s1.A.HorizontalEqualz(s2.A, tol) ||
                   s1.A.HorizontalEqualz(s2.B, tol) ||
                   s1.B.HorizontalEqualz(s2.A, tol) ||
                   s1.B.HorizontalEqualz(s2.B, tol);
        }

        private List<(TPort exitPort, double exitZ, double exitSlope)> ComputeExits(
            TPort entryPort,
            PipeRun supply,
            PipeRun ret,
            TPort twinPort,
            double entryZ,
            double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();
            var runs = new[] { supply, ret };

            if (ReferenceEquals(entryPort, twinPort))
            {
                foreach (var run in runs)
                {
                    exits.Add((run.BondedPort, entryZ, entrySlope));
                }
                return exits;
            }

            var matched = runs.FirstOrDefault(r => ReferenceEquals(r.BondedPort, entryPort));
            if (matched.Equals(default(PipeRun)))
                throw new InvalidOperationException("Entry port not part of FModel.");

            exits.Add((twinPort, entryZ, entrySlope));
            return exits;
        }

        private ResolvedPorts ResolveBondedPorts(
            PortInstance twin,
            PortInstance bondedA,
            PortInstance bondedB)
        {
            bool aSupply = bondedA.Flow == FlowRole.Supply;
            bool aReturn = bondedA.Flow == FlowRole.Return;
            bool bSupply = bondedB.Flow == FlowRole.Supply;
            bool bReturn = bondedB.Flow == FlowRole.Return;

            if (aSupply && bReturn)
                return new ResolvedPorts(twin, bondedA, bondedB);

            if (bSupply && aReturn)
                return new ResolvedPorts(twin, bondedB, bondedA);

            if (aSupply)
                return new ResolvedPorts(twin, bondedA, bondedB.WithFlow(FlowRole.Return));

            if (aReturn)
                return new ResolvedPorts(twin, bondedB.WithFlow(FlowRole.Supply), bondedA);

            if (bSupply)
                return new ResolvedPorts(twin, bondedB, bondedA.WithFlow(FlowRole.Return));

            if (bReturn)
                return new ResolvedPorts(twin, bondedA.WithFlow(FlowRole.Supply), bondedB);

            return new ResolvedPorts(
                twin,
                bondedA.WithFlow(FlowRole.Supply),
                bondedB.WithFlow(FlowRole.Return));
        }

        private PipeAxes BuildAxes(PortAssignment assignment)
        {
            var supplyPos = assignment.BondedSupply.Position;
            var returnPos = assignment.BondedReturn.Position;
            var twinPos = assignment.Twin.Position;

            var axisTwin = ToHorizontal(returnPos - supplyPos);
            if (axisTwin.Length < 1e-6)
            {
                var midBond = supplyPos.MidPoint(returnPos);
                axisTwin = ToHorizontal(midBond - twinPos);
            }
            if (axisTwin.Length < 1e-9)
                axisTwin = new Vector3d(0.0, 1.0, 0.0);
            axisTwin = axisTwin.GetNormal();

            var distSupply = supplyPos.DistanceTo(twinPos);
            var distReturn = returnPos.DistanceTo(twinPos);
            var far = distSupply >= distReturn ? supplyPos : returnPos;
            if ((far - twinPos).DotProduct(axisTwin) < 0.0)
                axisTwin = axisTwin.Negate();

            var axisBond = Vector3d.ZAxis.CrossProduct(axisTwin);
            if (axisBond.Length < 1e-9)
                axisBond = axisTwin.X != 0.0 || axisTwin.Y != 0.0
                    ? new Vector3d(-axisTwin.Y, axisTwin.X, 0.0)
                    : new Vector3d(1.0, 0.0, 0.0);
            axisBond = axisBond.GetNormal();

            var avgBond = new Point3d(
                0.5 * (supplyPos.X + returnPos.X),
                0.5 * (supplyPos.Y + returnPos.Y),
                0.5 * (supplyPos.Z + returnPos.Z));
            if ((avgBond - twinPos).DotProduct(axisBond) < 0.0)
                axisBond = axisBond.Negate();

            return new PipeAxes(axisTwin, axisBond);
        }

        private PipeRun BuildPipeRun(
            PortAssignment assignment,
            PipeAxes axes,
            double radius,
            PortInstance bonded,
            double twinOffset,
            double entryZ)
        {
            var twinCenter = assignment.Twin.Port.Node.Pos;
            var twinStart = new Point3d(
                twinCenter.X,
                twinCenter.Y,
                entryZ + twinOffset);

            var bondedPos = bonded.Position;
            var bondPoint = new Point3d(
                bondedPos.X,
                bondedPos.Y,
                entryZ);

            var tiePoint = IntersectLines2D(twinStart, axes.Twin, bondPoint, axes.Bond, twinStart.Z);
            var twinToTie = (tiePoint - twinStart).DotProduct(axes.Twin);
            if (twinToTie < radius - 1e-6)
                throw new InvalidOperationException($"FModel {Source}: insufficient distance between twin port and elbow.");

            var firstStraightEnd = tiePoint - axes.Twin.MultiplyBy(radius);

            var heightDelta = bondPoint.Z - twinStart.Z;
            var sign = heightDelta >= 0.0 ? 1.0 : -1.0;
            var absHeight = Math.Abs(heightDelta);

            var elbowSolution = ElbowTransitionSolver.Solve(absHeight, radius);
            var phi = elbowSolution.PhiRad;
            var cy = elbowSolution.Cy;

            var bondAlong = (bondPoint - tiePoint).DotProduct(axes.Bond);
            if (bondAlong + 1e-6 < cy)
                throw new InvalidOperationException($"FModel {Source}: bonded port too close for computed transition.");

            var verticalDir = Vector3d.ZAxis.MultiplyBy(sign);
            var intermediateDir = axes.Bond.MultiplyBy(Math.Cos(phi)) + verticalDir.MultiplyBy(Math.Sin(phi));

            var jointPoint = tiePoint
                + axes.Bond.MultiplyBy(Math.Cos(phi) * radius)
                + verticalDir.MultiplyBy(Math.Sin(phi) * radius);

            var finalStraightStart = tiePoint
                + axes.Bond.MultiplyBy(cy)
                + Vector3d.ZAxis.MultiplyBy(heightDelta);

            var tangentFirst = ElbowTransitionSolver.ComputeTangentIntersection(
                firstStraightEnd,
                axes.Twin,
                jointPoint,
                intermediateDir,
                radius);

            var tangentSecond = ElbowTransitionSolver.ComputeTangentIntersection(
                jointPoint,
                intermediateDir,
                finalStraightStart,
                axes.Bond,
                radius);

            var twinExitVec = twinStart - firstStraightEnd;
            var twinExitDir = twinExitVec.Length > 1e-6 ? twinExitVec.GetNormal() : axes.Twin.Negate();

            var bondExitVec = bondPoint - finalStraightStart;
            var bondExitDir = bondExitVec.Length > 1e-6 ? bondExitVec.GetNormal() : axes.Bond;

            return new PipeRun(
                bonded.Flow,
                bonded.Port,
                bondPoint,
                twinStart,
                firstStraightEnd,
                jointPoint,
                finalStraightStart,
                axes.Twin,
                axes.Bond,
                intermediateDir,
                tangentFirst,
                tangentSecond,
                twinExitDir,
                bondExitDir);
        }

        private static Point3d IntersectLines2D(
            Point3d originA,
            Vector3d dirA,
            Point3d originB,
            Vector3d dirB,
            double z)
        {
            var a2 = new Vector2d(dirA.X, dirA.Y);
            var b2 = new Vector2d(dirB.X, dirB.Y);
            var denom = a2.X * b2.Y - a2.Y * b2.X;
            if (Math.Abs(denom) < 1e-12)
                throw new InvalidOperationException("FModel axes are parallel; unable to find intersection.");

            var delta = new Vector2d(originB.X - originA.X, originB.Y - originA.Y);
            var s = (delta.X * b2.Y - delta.Y * b2.X) / denom;
            var point = originA + dirA.MultiplyBy(s);
            return new Point3d(point.X, point.Y, z);
        }

        private static Vector3d ToHorizontal(Vector3d v) => new(v.X, v.Y, 0.0);

        private static double ComputeSlope(Vector3d dir)
        {
            var horiz = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
            if (horiz < 1e-9) return 0.0;
            return dir.Z / horiz;
        }

        #region Helper types

        private readonly record struct PortInstance(TPort Port, FlowRole Flow)
        {
            public Point3d Position => Port.Node.Pos;
            public PortInstance WithFlow(FlowRole flow) => new(Port, flow);
        }

        private readonly record struct PortAssignment(
            PortInstance Twin,
            PortInstance BondedSupply,
            PortInstance BondedReturn);
        private readonly record struct ResolvedPorts(
            PortInstance Twin,
            PortInstance BondedSupply,
            PortInstance BondedReturn);

        private readonly record struct PipeAxes(Vector3d Twin, Vector3d Bond);

        private readonly record struct PipeRun(
            FlowRole Flow,
            TPort BondedPort,
            Point3d BondedPoint,
            Point3d TwinStart,
            Point3d FirstStraightEnd,
            Point3d JointPoint,
            Point3d FinalStraightStart,
            Vector3d AxisTwin,
            Vector3d AxisBond,
            Vector3d IntermediateDirection,
            Point3d TangentFirst,
            Point3d TangentSecond,
            Vector3d TwinExitDirection,
            Vector3d BondedExitDirection);

        #endregion
    }
}
