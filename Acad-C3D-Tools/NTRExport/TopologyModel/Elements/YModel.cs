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
    internal sealed class YModel : TFitting
    {
        public YModel(Handle source)
            : base(source, PipelineElementType.Y_Model) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Y_Model);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var assignment = ClassifyPorts(topo);

            // Twin port is at fork handle start, bonded ports are at prong ends
            var twinPos = assignment.Twin.Position;
            var bondSupplyPos = assignment.BondedSupply.Position;
            var bondReturnPos = assignment.BondedReturn.Position;

            //prdDbg($"YModel {Source}: Starting geometry calculation.");
            //prdDbg($"YModel {Source}: Twin port position: ({twinPos.X:0.###}, {twinPos.Y:0.###}, {twinPos.Z:0.###})");
            //prdDbg($"YModel {Source}: Bonded supply position: ({bondSupplyPos.X:0.###}, {bondSupplyPos.Y:0.###}, {bondSupplyPos.Z:0.###})");
            //prdDbg($"YModel {Source}: Bonded return position: ({bondReturnPos.X:0.###}, {bondReturnPos.Y:0.###}, {bondReturnPos.Z:0.###})");

            // Calculate midpoint between bonded ports
            var bondMidpoint = new Point3d(
                0.5 * (bondSupplyPos.X + bondReturnPos.X),
                0.5 * (bondSupplyPos.Y + bondReturnPos.Y),
                0.5 * (bondSupplyPos.Z + bondReturnPos.Z));

            // Calculate distance from twin port to midpoint between bonded ports
            var L = (bondMidpoint - twinPos).Length;

            //prdDbg($"YModel {Source}: Bond midpoint: ({bondMidpoint.X:0.###}, {bondMidpoint.Y:0.###}, {bondMidpoint.Z:0.###})");
            //prdDbg($"YModel {Source}: L (distance from twin to bond midpoint) = {L:0.###} m");

            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var r = Geometry.GetBogRadius5D(DN) / 1000.0; // Fillet radius in meters
            var twinPipeLength = 7.0 / 20.0 * L;
            var bondedPipeLength = 7.0 / 20.0 * L;

            //prdDbg($"YModel {Source}: zUp = {zUp:0.###} m, zLow = {zLow:0.###} m");
            //prdDbg($"YModel {Source}: Fillet radius r = {r:0.###} m");
            //prdDbg($"YModel {Source}: Twin pipe length = {twinPipeLength:0.###} m, Bonded pipe length = {bondedPipeLength:0.###} m");

            // Calculate geometry directly from port positions
            var geometry = CalculateGeometry(twinPos, bondSupplyPos, bondReturnPos, bondMidpoint, twinPipeLength, bondedPipeLength, zUp, zLow);

            // Emit geometry
            EmitGeometry(g, geometry, r, entryZ);

            // Compute exits
            return ComputeExits(entryPort, entryZ, entrySlope, assignment, geometry);
        }

        private PortAssignment ClassifyPorts(Topology topo)
        {
            if (Ports.Count != 3)
                throw new InvalidOperationException($"YModel {Source}: expected exactly 3 ports, found {Ports.Count}.");

            // All ports are neutral, so we need to infer flow roles from topology
            FlowRole InferFlow(TPort port) => topo.FindRoleFromPort(this, port);
            bool ConnectsToTwin(TPort port) => IsConnectedToTwin(topo, port);

            // Try to infer flow roles for all ports
            var portInstances = Ports.Select(p => new PortInstance(p, InferFlow(p))).ToList();

            // Find ports with inferred flow roles (bonded ports)
            var bondedPorts = portInstances.Where(p => p.Flow == FlowRole.Supply || p.Flow == FlowRole.Return).ToList();
            var unknownPorts = portInstances.Where(p => p.Flow == FlowRole.Unknown).ToList();

            PortInstance twinPort;
            List<PortInstance> bondedInstances;

            if (bondedPorts.Count == 2)
            {
                // Two bonded ports found - remaining is twin
                if (unknownPorts.Count != 1)
                    throw new InvalidOperationException($"YModel {Source}: expected exactly one twin port when 2 bonded ports found, found {unknownPorts.Count}.");
                twinPort = unknownPorts[0];
                bondedInstances = bondedPorts;
            }
            else if (bondedPorts.Count == 0)
            {
                // No bonded ports found - check if any port connects to twin
                var twinConnectedPorts = portInstances.Where(p => ConnectsToTwin(p.Port)).ToList();

                if (twinConnectedPorts.Count == 1)
                {
                    // One port connects to twin - that's the twin port
                    // Remaining ports are bonded - assign flow roles randomly
                    twinPort = twinConnectedPorts[0];
                    var remainingPorts = portInstances.Where(p => !ReferenceEquals(p.Port, twinPort.Port)).ToList();
                    if (remainingPorts.Count != 2)
                        throw new InvalidOperationException($"YModel {Source}: expected exactly 2 remaining ports when twin port found, found {remainingPorts.Count}.");

                    // Assign flow roles randomly - doesn't matter which is supply/return
                    bondedInstances = new List<PortInstance>
                    {
                        remainingPorts[0].WithFlow(FlowRole.Supply),
                        remainingPorts[1].WithFlow(FlowRole.Return)
                    };
                }
                else
                {
                    throw new InvalidOperationException($"YModel {Source}: unable to classify ports - no bonded ports found and {twinConnectedPorts.Count} twin-connected ports.");
                }
            }
            else
            {
                throw new InvalidOperationException($"YModel {Source}: unexpected number of bonded ports found: {bondedPorts.Count}.");
            }

            // Resolve which bonded port is supply and which is return
            var resolved = ResolveBondedPorts(twinPort, bondedInstances[0], bondedInstances[1]);

            return new PortAssignment(
                resolved.Twin,
                resolved.BondedSupply,
                resolved.BondedReturn);
        }

        private bool IsConnectedToTwin(Topology topo, TPort port)
        {
            if (port?.Node == null) return false;

            var visited = new HashSet<TNode>();
            var queue = new Queue<TNode>();
            visited.Add(port.Node);
            queue.Enqueue(port.Node);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var otherPort in node.Ports)
                {
                    var owner = otherPort.Owner;
                    if (ReferenceEquals(owner, this)) continue;

                    // Check if connected to twin pipe or twin fitting
                    if (owner.Type == PipeTypeEnum.Twin)
                        return true;

                    if (owner is TFitting fitting && fitting.Variant.IsTwin)
                        return true;

                    // Continue traversal
                    var nextNode = otherPort.Node;
                    if (ReferenceEquals(nextNode, node)) continue;
                    if (visited.Add(nextNode))
                        queue.Enqueue(nextNode);
                }
            }

            return false;
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

            // Both unknown - assign arbitrarily
            return new ResolvedPorts(twin, bondedA.WithFlow(FlowRole.Supply), bondedB.WithFlow(FlowRole.Return));
        }

        private string DescribePoint(Point3d pt) => $"({pt.X:0.###},{pt.Y:0.###},{pt.Z:0.###})";

        private YGeometry CalculateGeometry(
            Point3d twinPos, Point3d bondSupplyPos, Point3d bondReturnPos, Point3d bondMidpoint,
            double twinPipeLength, double bondedPipeLength, double zUp, double zLow)
        {
            //prdDbg($"YModel {Source}: CalculateGeometry - Input parameters:");
            //prdDbg($"YModel {Source}:   twinPipeLength = {twinPipeLength:0.###} m, bondedPipeLength = {bondedPipeLength:0.###} m");

            // Calculate direction from twin to midpoint (used for both twin and bonded pipes)
            var dirAxis = bondMidpoint - twinPos;
            if (dirAxis.Length < 1e-9)
                throw new InvalidOperationException($"YModel {Source}: Invalid port positions - twin and bond midpoint too close.");

            //prdDbg($"YModel {Source}:   dirAxis = ({dirAxis.X:0.###}, {dirAxis.Y:0.###}, {dirAxis.Z:0.###}), length = {dirAxis.Length:0.###} m");

            var dirAxisNorm = dirAxis.GetNormal();

            //prdDbg($"YModel {Source}:   dirAxisNorm = ({dirAxisNorm.X:0.###}, {dirAxisNorm.Y:0.###}, {dirAxisNorm.Z:0.###})");

            // Twin pipes start at twin port, extend along common axis
            var twinSupplyStart = new Point3d(twinPos.X, twinPos.Y, twinPos.Z + zLow);
            var twinReturnStart = new Point3d(twinPos.X, twinPos.Y, twinPos.Z + zUp);
            var twinSupplyEnd = twinSupplyStart + dirAxisNorm.MultiplyBy(twinPipeLength);
            var twinReturnEnd = twinReturnStart + dirAxisNorm.MultiplyBy(twinPipeLength);

            //prdDbg($"YModel {Source}: Twin pipes:");
            //prdDbg($"YModel {Source}:   twinSupplyStart = {DescribePoint(twinSupplyStart)}");
            //prdDbg($"YModel {Source}:   twinSupplyEnd = {DescribePoint(twinSupplyEnd)}");
            //prdDbg($"YModel {Source}:   twinReturnStart = {DescribePoint(twinReturnStart)}");
            //prdDbg($"YModel {Source}:   twinReturnEnd = {DescribePoint(twinReturnEnd)}");

            // Bonded pipes end at bonded ports, extend backwards along direction from twin to midpoint
            var bondSupplyPort = bondSupplyPos;
            var bondReturnPort = bondReturnPos;
            var bondSupplyStart = bondSupplyPort - dirAxisNorm.MultiplyBy(bondedPipeLength);
            var bondReturnStart = bondReturnPort - dirAxisNorm.MultiplyBy(bondedPipeLength);

            //prdDbg($"YModel {Source}: Bonded pipes:");
            //prdDbg($"YModel {Source}:   bondSupplyStart = {DescribePoint(bondSupplyStart)}");
            //prdDbg($"YModel {Source}:   bondSupplyPort = {DescribePoint(bondSupplyPort)}");
            //prdDbg($"YModel {Source}:   bondReturnStart = {DescribePoint(bondReturnStart)}");
            //prdDbg($"YModel {Source}:   bondReturnPort = {DescribePoint(bondReturnPort)}");

            // Connecting pipes connect twin end to bonded start
            // These follow the actual path between the points
            var connectSupplyStart = twinSupplyEnd;
            var connectSupplyEnd = bondSupplyStart;
            var connectReturnStart = twinReturnEnd;
            var connectReturnEnd = bondReturnStart;

            //prdDbg($"YModel {Source}: Connecting pipe directions:");
            var connectSupplyDir = connectSupplyEnd - connectSupplyStart;
            var connectReturnDir = connectReturnEnd - connectReturnStart;
            //prdDbg($"YModel {Source}:   connectSupplyDir = ({connectSupplyDir.X:0.###}, {connectSupplyDir.Y:0.###}, {connectSupplyDir.Z:0.###}), length = {connectSupplyDir.Length:0.###} m");
            //prdDbg($"YModel {Source}:   connectReturnDir = ({connectReturnDir.X:0.###}, {connectReturnDir.Y:0.###}, {connectReturnDir.Z:0.###}), length = {connectReturnDir.Length:0.###} m");

            //prdDbg($"YModel {Source}: Connecting pipes:");
            //prdDbg($"YModel {Source}:   connectSupplyStart = {DescribePoint(connectSupplyStart)}");
            //prdDbg($"YModel {Source}:   connectSupplyEnd = {DescribePoint(connectSupplyEnd)}");
            //prdDbg($"YModel {Source}:   connectReturnStart = {DescribePoint(connectReturnStart)}");
            //prdDbg($"YModel {Source}:   connectReturnEnd = {DescribePoint(connectReturnEnd)}");

            return new YGeometry(
                twinSupplyStart, twinSupplyEnd,
                twinReturnStart, twinReturnEnd,
                connectSupplyStart, connectSupplyEnd,
                connectReturnStart, connectReturnEnd,
                bondSupplyStart, bondSupplyPort,
                bondReturnStart, bondReturnPort);
        }

        private void EmitGeometry(RoutedGraph g, YGeometry geometry, double r, double entryZ)
        {
            var ltg = LTGMain(Source);

            // Apply entryZ offset to points
            Point3d ApplyZ(Point3d pt) => new Point3d(pt.X, pt.Y, pt.Z + entryZ);

            // Calculate fillet endpoints between two lines in a plane
            // ts = twin start, te = twin end (corner), bs = bonded start
            // Returns: (f1a, f1b) - fillet endpoints on first and second lines
            (Point3d f1a, Point3d f1b) CalculateFillet(Point3d ts, Point3d te, Point3d bs, double radius)
            {
                //prdDbg($"YModel {Source}: CalculateFillet - Input points:");
                //prdDbg($"YModel {Source}:   ts = {DescribePoint(ts)}");
                //prdDbg($"YModel {Source}:   te = {DescribePoint(te)}");
                //prdDbg($"YModel {Source}:   bs = {DescribePoint(bs)}");

                var tsWorld = ApplyZ(ts);
                var teWorld = ApplyZ(te);
                var bsWorld = ApplyZ(bs);

                // Create plane from three points: ts, te, bs
                var v1 = teWorld - tsWorld;
                var v2 = bsWorld - teWorld;

                if (v1.Length < 1e-9 || v2.Length < 1e-9)
                {
                    //prdDbg($"YModel {Source}:   DEGENERATE: vectors too small");
                    return (teWorld, teWorld);
                }

                // Plane normal (perpendicular to both vectors)
                var planeNormal = v1.CrossProduct(v2);
                if (planeNormal.Length < 1e-9)
                {
                    //prdDbg($"YModel {Source}:   PARALLEL: vectors are parallel");
                    return (teWorld, teWorld);
                }

                planeNormal = planeNormal.GetNormal();
                var planeOrigin = teWorld;

                //prdDbg($"YModel {Source}:   Plane: origin = {DescribePoint(planeOrigin)}, normal = ({planeNormal.X:0.###}, {planeNormal.Y:0.###}, {planeNormal.Z:0.###})");

                // Build orthonormal basis (u along first leg direction, v perpendicular within plane)
                var u = v1.GetNormal();
                Vector3d v = planeNormal.CrossProduct(u);
                if (v.Length < 1e-9)
                {
                    v = planeNormal.IsParallelTo(Vector3d.ZAxis)
                        ? Vector3d.XAxis.CrossProduct(planeNormal)
                        : planeNormal.CrossProduct(Vector3d.ZAxis);
                }
                v = v.GetNormal();

                //prdDbg($"YModel {Source}:   Plane basis: u = ({u.X:0.###}, {u.Y:0.###}, {u.Z:0.###}), v = ({v.X:0.###}, {v.Y:0.###}, {v.Z:0.###})");

                // Convert 3D points to 2D coordinates in the plane
                Point2d ToPlane2D(Point3d p3d)
                {
                    var w = p3d - planeOrigin;
                    return new Point2d(w.DotProduct(u), w.DotProduct(v));
                }

                // Convert 2D points back to 3D in the plane
                Point3d ToPlane3D(Point2d p2d)
                {
                    return planeOrigin + u.MultiplyBy(p2d.X) + v.MultiplyBy(p2d.Y);
                }

                // Project points to plane
                var ts2 = ToPlane2D(tsWorld);
                var te2 = ToPlane2D(teWorld);
                var bs2 = ToPlane2D(bsWorld);

                //prdDbg($"YModel {Source}:   Points in plane (2D):");
                //prdDbg($"YModel {Source}:   ts2 = ({ts2.X:0.###}, {ts2.Y:0.###})");
                //prdDbg($"YModel {Source}:   te2 = ({te2.X:0.###}, {te2.Y:0.###})");
                //prdDbg($"YModel {Source}:   bs2 = ({bs2.X:0.###}, {bs2.Y:0.###})");

                // Vectors: from corner towards start and from corner towards end
                var va = ts2 - te2;  // vector from corner towards start
                var vb = bs2 - te2;  // vector from corner towards end

                //prdDbg($"YModel {Source}:   Vectors in plane:");
                //prdDbg($"YModel {Source}:   va = ({va.X:0.###}, {va.Y:0.###}), length = {va.Length:0.###}");
                //prdDbg($"YModel {Source}:   vb = ({vb.X:0.###}, {vb.Y:0.###}), length = {vb.Length:0.###}");

                if (va.Length < 1e-9 || vb.Length < 1e-9)
                {
                    //prdDbg($"YModel {Source}:   DEGENERATE: va or vb too small");
                    return (teWorld, teWorld);
                }

                var ua = va.GetNormal();
                var ub = vb.GetNormal();
                var dot = Math.Max(-1.0, Math.Min(1.0, ua.DotProduct(ub)));
                var alpha = Math.Acos(dot);
                var sinHalf = Math.Sin(alpha * 0.5);
                var cosHalf = Math.Cos(alpha * 0.5);

                //prdDbg($"YModel {Source}:   ua = ({ua.X:0.###}, {ua.Y:0.###})");
                //prdDbg($"YModel {Source}:   ub = ({ub.X:0.###}, {ub.Y:0.###})");
                //prdDbg($"YModel {Source}:   dot = {dot:0.###}, alpha = {alpha * 180.0 / Math.PI:0.###} degrees");
                //prdDbg($"YModel {Source}:   sinHalf = {sinHalf:0.###}, cosHalf = {cosHalf:0.###}");

                if (sinHalf < 1e-9)
                {
                    //prdDbg($"YModel {Source}:   PARALLEL: sinHalf too small");
                    return (teWorld, teWorld);
                }

                var l = radius * (cosHalf / sinHalf); // R * cot(alpha/2)

                // Check feasibility
                var lenAT = va.Length;
                var lenBT = vb.Length;

                //prdDbg($"YModel {Source}:   Fillet calculation:");
                //prdDbg($"YModel {Source}:   r = {radius:0.###} m, l (calculated) = {l:0.###} m");
                //prdDbg($"YModel {Source}:   lenAT = {lenAT:0.###} m, lenBT = {lenBT:0.###} m");

                if (l > lenAT - 1e-9 || l > lenBT - 1e-9)
                {
                    // Radius too large - clamp
                    var oldL = l;
                    l = Math.Max(0.0, Math.Min(lenAT, lenBT) * 0.5);
                    //prdDbg($"YModel {Source}:   RADIUS TOO LARGE: clamped l from {oldL:0.###} to {l:0.###} m");
                }

                // Calculate fillet endpoints in plane (2D)
                // f1a is on first line (ts-te), f1b is on second line (te-bs)
                var f1a2 = new Point2d(te2.X + ua.X * l, te2.Y + ua.Y * l);  // along first line towards start
                var f1b2 = new Point2d(te2.X + ub.X * l, te2.Y + ub.Y * l);  // along second line towards end

                //prdDbg($"YModel {Source}:   Fillet endpoints in plane (2D):");
                //prdDbg($"YModel {Source}:   f1a2 = ({f1a2.X:0.###}, {f1a2.Y:0.###})");
                //prdDbg($"YModel {Source}:   f1b2 = ({f1b2.X:0.###}, {f1b2.Y:0.###})");

                // Project back to 3D world coordinates
                var f1a = ToPlane3D(f1a2);
                var f1b = ToPlane3D(f1b2);

                //prdDbg($"YModel {Source}:   Fillet endpoints (3D world):");
                //prdDbg($"YModel {Source}:   f1a = {DescribePoint(f1a)}");
                //prdDbg($"YModel {Source}:   f1b = {DescribePoint(f1b)}");

                return (f1a, f1b);
            }

            // Emit geometry for one flow path (supply or return)
            RoutedStraight EmitPath(Point3d ts, Point3d te, Point3d bs, Point3d be, FlowRole flow)
            {
                //prdDbg($"YModel {Source}: === EmitPath ({flow}) ===");
                //prdDbg($"YModel {Source}:   ts = {DescribePoint(ts)}");
                //prdDbg($"YModel {Source}:   te = {DescribePoint(te)}");
                //prdDbg($"YModel {Source}:   bs = {DescribePoint(bs)}");
                //prdDbg($"YModel {Source}:   be = {DescribePoint(be)}");

                // First fillet: between lines ts-te and te-bs
                var (f1a, f1b) = CalculateFillet(ts, te, bs, r);

                // Second fillet: between lines te-bs and bs-be
                var (f2a, f2b) = CalculateFillet(te, bs, be, r);

                //prdDbg($"YModel {Source}:   First fillet: f1a = {DescribePoint(f1a)}, f1b = {DescribePoint(f1b)}");
                //prdDbg($"YModel {Source}:   Second fillet: f2a = {DescribePoint(f2a)}, f2b = {DescribePoint(f2b)}");

                // Emit: ts → f1a (twin pipe straight)
                //prdDbg($"YModel {Source}:   Emitting straight: ts → f1a");
                var firstStraight = new RoutedStraight(Source, this)
                {
                    A = ApplyZ(ts),
                    B = f1a,
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flow,
                    LTG = ltg,
                };
                g.Members.Add(firstStraight);

                // Emit: f1a → f1b (first elbow at intersection te)
                //prdDbg($"YModel {Source}:   Emitting BEND: f1a → f1b, T = te");
                g.Members.Add(new RoutedBend(Source, this)
                {
                    A = f1a,
                    B = f1b,
                    T = ApplyZ(te),
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flow,
                    LTG = ltg,
                });

                // Emit: f1b → f2a (connecting pipe straight)
                //prdDbg($"YModel {Source}:   Emitting straight: f1b → f2a");
                g.Members.Add(new RoutedStraight(Source, this)
                {
                    A = f1b,
                    B = f2a,
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flow,
                    LTG = ltg,
                });

                // Emit: f2a → f2b (second elbow at intersection bs)
                //prdDbg($"YModel {Source}:   Emitting BEND: f2a → f2b, T = bs");
                g.Members.Add(new RoutedBend(Source, this)
                {
                    A = f2a,
                    B = f2b,
                    T = ApplyZ(bs),
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flow,
                    LTG = ltg,
                });

                // Emit: f2b → be (bonded pipe straight)
                //prdDbg($"YModel {Source}:   Emitting straight: f2b → be");
                g.Members.Add(new RoutedStraight(Source, this)
                {
                    A = f2b,
                    B = ApplyZ(be),
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flow,
                    LTG = ltg,
                });

                return firstStraight;
            }

            //prdDbg($"YModel {Source}: EmitGeometry - entryZ = {entryZ:0.###} m, r = {r:0.###} m");

            // Emit supply path: twin supply start → twin supply end → bonded supply start → bonded supply port
            var supplyStraight = EmitPath(geometry.TwinSupplyStart, geometry.TwinSupplyEnd, geometry.BondSupplyStart, geometry.BondSupplyPort, FlowRole.Supply);

            // Emit return path: twin return start → twin return end → bonded return start → bonded return port
            var returnStraight = EmitPath(geometry.TwinReturnStart, geometry.TwinReturnEnd, geometry.BondReturnStart, geometry.BondReturnPort, FlowRole.Return);


            var midpointSupply = supplyStraight.A.MidPoint(supplyStraight.B);
            var midpointReturn = returnStraight.A.MidPoint(returnStraight.B);

            g.Members.Add(new RoutedRigid(Source, this)
            {
                P1 = midpointSupply,
                P2 = new Point3d(midpointSupply.X, midpointSupply.Y, midpointReturn.Z),
                Material = Material,
            });


            //prdDbg($"YModel {Source}: Geometry emission complete. Total members added: {g.Members.Count}");
        }

        private List<(TPort exitPort, double exitZ, double exitSlope)> ComputeExits(
            TPort entryPort, double entryZ, double entrySlope,
            PortAssignment assignment, YGeometry geometry)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();

            double ComputeSlope(Vector3d dir)
            {
                var horiz = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                if (horiz < 1e-9) return 0.0;
                return dir.Z / horiz;
            }

            if (ReferenceEquals(entryPort, assignment.Twin.Port))
            {
                // Entry from twin - exits are bonded ports
                var bondSupplyDir = (geometry.BondSupplyPort - geometry.BondSupplyStart).GetNormal();
                var bondReturnDir = (geometry.BondReturnPort - geometry.BondReturnStart).GetNormal();
                exits.Add((assignment.BondedSupply.Port, entryZ, ComputeSlope(bondSupplyDir)));
                exits.Add((assignment.BondedReturn.Port, entryZ, ComputeSlope(bondReturnDir)));
            }
            else if (ReferenceEquals(entryPort, assignment.BondedSupply.Port))
            {
                // Entry from bonded supply - exit is twin (supply lane)
                var twinSupplyDir = (geometry.TwinSupplyEnd - geometry.TwinSupplyStart).GetNormal();
                exits.Add((assignment.Twin.Port, entryZ, ComputeSlope(twinSupplyDir)));
            }
            else if (ReferenceEquals(entryPort, assignment.BondedReturn.Port))
            {
                // Entry from bonded return - exit is twin (return lane)
                var twinReturnDir = (geometry.TwinReturnEnd - geometry.TwinReturnStart).GetNormal();
                exits.Add((assignment.Twin.Port, entryZ, ComputeSlope(twinReturnDir)));
            }
            else
            {
                throw new InvalidOperationException($"YModel {Source}: Entry port not part of YModel.");
            }

            return exits;
        }

        #region Helper types

        private readonly record struct YGeometry(
            Point3d TwinSupplyStart, Point3d TwinSupplyEnd,
            Point3d TwinReturnStart, Point3d TwinReturnEnd,
            Point3d ConnectSupplyStart, Point3d ConnectSupplyEnd,
            Point3d ConnectReturnStart, Point3d ConnectReturnEnd,
            Point3d BondSupplyStart, Point3d BondSupplyPort,
            Point3d BondReturnStart, Point3d BondReturnPort);

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

        #endregion
    }
}
