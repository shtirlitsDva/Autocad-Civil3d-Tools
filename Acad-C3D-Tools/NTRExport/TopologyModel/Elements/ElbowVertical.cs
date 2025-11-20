using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;

using static IntersectUtilities.UtilsCommon.Utils;
using static NTRExport.Utils.Utils;

namespace NTRExport.TopologyModel
{
    internal class ElbowVertical : TFitting
    {
        BlockReference _br;

        public ElbowVertical(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = source.Go<BlockReference>(db);
            if (br == null)
                throw new Exception($"Received {source} for ElbowFormstykke!");

            _br = br;
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Kedelrørsbøjning);
        }

        internal override void AttachPropertySet()
        {
            var ntr = new NtrData(_entity);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            double angleDeg = Convert.ToDouble(
                _br.ReadDynamicCsvProperty(DynamicProperty.Vinkel));

            var ntr = new NtrData(_entity);
            var dirUp = ntr.VertikalBøjningDir == "Up";

            var radius = Geometry.GetBogRadius3D(DN) / 1000.0; // m

            var otherPort = GetOtherPort(entryPort);

            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();

            List<(TPort exitPort, double exitZ, double exitSlope)> PropagateUnchanged()
            {
                var fallback = new List<(TPort exitPort, double exitZ, double exitSlope)>();
                foreach (var p in Ports)
                {
                    if (ReferenceEquals(p, entryPort)) continue;
                    fallback.Add((p, entryZ, entrySlope));
                }
                return fallback;
            }

            void AddRigid(Point3d first, Point3d second)
            {
                var top = first;
                var bottom = second;
                if (bottom.Z > top.Z)
                {
                    var temp = top;
                    top = bottom;
                    bottom = temp;
                }

                g.Members.Add(new RoutedRigid(Source, this)
                {
                    P1 = bottom,
                    P2 = top,
                    Material = Material,
                });
            }

            static bool TryIntersect(Point2d e, Vector2d de, Point2d o, Vector2d d0, out Point2d inter, out double t, out double s)
            {
                inter = default; t = 0.0; s = 0.0;
                double cross(Vector2d a, Vector2d b) => a.X * b.Y - a.Y * b.X;
                var denom = cross(de, d0);
                if (Math.Abs(denom) < 1e-12) return false;
                var w = new Vector2d(o.X - e.X, o.Y - e.Y);
                t = cross(w, d0) / denom;
                s = cross(w, de) / denom;
                inter = new Point2d(e.X + de.X * t, e.Y + de.Y * t);
                return true;
            }

            if (Variant.IsTwin)
            {
                var entryPos = entryPort.Node.Pos;
                var otherPos = otherPort.Node.Pos;

                var entryXY = entryPos.To2d();
                var otherXY = otherPos.To2d();
                var dirXY = otherXY - entryXY;
                var dirLen = dirXY.Length;

                //prdDbg($"ElbowVertical {Source} twin inputs: entryZ={entryZ:0.###}, slope={entrySlope:0.####}, angleDeg={angleDeg:0.###}, radius={radius:0.###}, dirUp={dirUp}, entryXY=({entryXY.X:0.###},{entryXY.Y:0.###}), otherXY=({otherXY.X:0.###},{otherXY.Y:0.###})");

                if (dirLen < 1e-9)
                {
                    //prdDbg($"ElbowVertical {Source} twin: degenerate XY baseline (|dir|={dirLen:0.###}); propagating unchanged.");
                    return PropagateUnchanged();
                }

                var uHat = dirXY.GetNormal();

                var alphaE = Math.Atan(entrySlope);
                var theta = Math.Abs(angleDeg) * Math.PI / 180.0;
                if (theta < 1e-6 || radius < 1e-9)
                {
                    //prdDbg($"ElbowVertical {Source} twin: invalid theta/radius (theta={theta:0.######}, radius={radius:0.###}).");
                    return PropagateUnchanged();
                }

                var signedTheta = dirUp ? theta : -theta;
                var alphaO = alphaE + signedTheta;
                var entryDir = new Vector2d(Math.Cos(alphaE), Math.Sin(alphaE));
                var exitDir = new Vector2d(Math.Cos(alphaO), Math.Sin(alphaO));

                Vector2d RotateVec(Vector2d v, double ang)
                {
                    var ca = Math.Cos(ang);
                    var sa = Math.Sin(ang);
                    return new Vector2d(v.X * ca - v.Y * sa, v.X * sa + v.Y * ca);
                }

                var entryLocal = new Point2d(0.0, entryZ);
                var leftNormal = new Vector2d(-entryDir.Y, entryDir.X);
                var centerLocal = entryLocal.Add(leftNormal.MultiplyBy(dirUp ? radius : -radius));
                var vecCenterToEntry = entryLocal - centerLocal;
                var vecCenterToExit = RotateVec(vecCenterToEntry, signedTheta);
                var exitLocal = centerLocal.Add(vecCenterToExit);
                var deltaU = exitLocal.X - entryLocal.X;
                var deltaW = exitLocal.Y - entryLocal.Y;

                //prdDbg($"ElbowVertical {Source} twin local geom: alphaE={alphaE:0.####}, alphaO={alphaO:0.####}, center=({centerLocal.X:0.###},{centerLocal.Y:0.###}), deltaU={deltaU:0.###}, deltaW={deltaW:0.###}, exitLocal=({exitLocal.X:0.###},{exitLocal.Y:0.###})");

                if (!TryIntersect(entryLocal, entryDir, exitLocal, exitDir, out var tangentLocal, out var tEntry, out var tExit))
                {
                    //prdDbg($"ElbowVertical {Source} twin: tangent intersection failed.");
                    return PropagateUnchanged();
                }

                if (tEntry <= 1e-9)
                {
                    //prdDbg($"ElbowVertical {Source} twin: tangent length invalid (tEntry={tEntry:0.###}).");
                    return PropagateUnchanged();
                }

                var vecTA = entryLocal - tangentLocal;
                var vecTB = exitLocal - tangentLocal;
                if (vecTA.Length <= 1e-9 || vecTB.Length <= 1e-9)
                {
                    //prdDbg($"ElbowVertical {Source} twin: tangent vectors too short (|TA|={vecTA.Length:0.###}, |TB|={vecTB.Length:0.###}).");
                    return PropagateUnchanged();
                }

                var bisectorVec = vecTA.GetNormal() + vecTB.GetNormal();
                if (bisectorVec.Length <= 1e-9)
                {
                    //prdDbg($"ElbowVertical {Source} twin: bisector undefined.");
                    return PropagateUnchanged();
                }
                var bisectorUnit = bisectorVec.GetNormal();

                var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
                //prdDbg($"ElbowVertical {Source} twin tangents: T=({tangentLocal.X:0.###},{tangentLocal.Y:0.###}), tEntry={tEntry:0.###}, tExit={tExit:0.###}, bisector=({bisectorUnit.X:0.####},{bisectorUnit.Y:0.####}), offsets=({zUp:0.###},{zLow:0.###})");

                Point3d MapToWorld(Point2d uw)
                {
                    var x = entryXY.X + uHat.X * uw.X;
                    var y = entryXY.Y + uHat.Y * uw.X;
                    return new Point3d(x, y, uw.Y);
                }

                Point2d OffsetPoint(Point2d pt, Vector2d offset) => pt.Add(offset);

                (Point3d A, Point3d T, Point3d B) BuildPoints(double offsetMag)
                {
                    var offsetLocal = bisectorUnit.MultiplyBy(offsetMag);
                    var aLocal = OffsetPoint(entryLocal, offsetLocal);
                    var tLocal = OffsetPoint(tangentLocal, offsetLocal);
                    var bLocal = OffsetPoint(exitLocal, offsetLocal);
                    return (MapToWorld(aLocal), MapToWorld(tLocal), MapToWorld(bLocal));
                }

                //offset along bisector needs to be scaled because the bisector is our hypotenuse
                //and our offset is our near side of a right triangle, near angle is half of our bend
                var f = Math.Cos(angleDeg * Math.PI / 360.0);
                var returnPts = BuildPoints(zUp / f);
                var supplyPts = BuildPoints(zLow / f);

                double AvgZ((Point3d A, Point3d T, Point3d B) pts) =>
                    (pts.A.Z + pts.T.Z + pts.B.Z) / 3.0;

                var avgReturnZ = AvgZ(returnPts);
                var avgSupplyZ = AvgZ(supplyPts);
                const double roleSwapTol = 1e-6;
                if (avgReturnZ + roleSwapTol < avgSupplyZ)
                {
                    //prdDbg($"ElbowVertical {Source} twin: swapping flow roles to keep return above supply (avgReturnZ={avgReturnZ:0.###}, avgSupplyZ={avgSupplyZ:0.###}).");
                    var temp = returnPts;
                    returnPts = supplyPts;
                    supplyPts = temp;
                }

                //prdDbg($"ElbowVertical {Source} twin return pts: A=({returnPts.A.X:0.###},{returnPts.A.Y:0.###},{returnPts.A.Z:0.###}), T=({returnPts.T.X:0.###},{returnPts.T.Y:0.###},{returnPts.T.Z:0.###}), B=({returnPts.B.X:0.###},{returnPts.B.Y:0.###},{returnPts.B.Z:0.###})");
                //prdDbg($"ElbowVertical {Source} twin supply pts: A=({supplyPts.A.X:0.###},{supplyPts.A.Y:0.###},{supplyPts.A.Z:0.###}), T=({supplyPts.T.X:0.###},{supplyPts.T.Y:0.###},{supplyPts.T.Z:0.###}), B=({supplyPts.B.X:0.###},{supplyPts.B.Y:0.###},{supplyPts.B.Z:0.###})");

                g.Members.Add(new RoutedBend(Source, this)
                {
                    A = returnPts.A,
                    B = returnPts.B,
                    T = returnPts.T,
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = FlowRole.Return,
                    LTG = LTGMain(Source),
                });

                g.Members.Add(new RoutedBend(Source, this)
                {
                    A = supplyPts.A,
                    B = supplyPts.B,
                    T = supplyPts.T,
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = FlowRole.Supply,
                    LTG = LTGMain(Source),
                });

                var exitZVal = exitLocal.Y;
                var exitSlopeVal = Math.Tan(alphaO);
                //prdDbg($"ElbowVertical {Source} twin exit: exitZ={exitZVal:0.###}, exitSlope={exitSlopeVal:0.####}");

                AddRigid(returnPts.A, supplyPts.A);
                AddRigid(returnPts.B, supplyPts.B);

                exits.Add((otherPort, exitZVal, exitSlopeVal));
                return exits;
            }
            if (Variant.IsTwin == false)
            {
                var aPort = entryPort;
                var bPort = otherPort;
                var a = aPort.Node.Pos;
                var b = bPort.Node.Pos;

                // Local 2D in the vertical plane: u along entry→other in XY, w = Z
                var entryIsA = ReferenceEquals(entryPort, aPort);
                var pEntry = entryIsA ? a : b;
                var pOther = entryIsA ? b : a;

                var entryXY = pEntry.To2d();
                var otherXY = pOther.To2d();
                var dirXY = (otherXY - entryXY);
                var c = dirXY.Length;
                if (c < 1e-9)
                {
                    // Degenerate: fall back to simple propagation
                    return PropagateUnchanged();
                }
                var uHat = dirXY.GetNormal(); // unit vector from entry to other in XY

                //prdDbg($"ElbowVertical {Source}: angleDeg={angleDeg:0.###}, dirUp={dirUp}, entrySlope={entrySlope:0.####}, entryZ={entryZ:0.###}, cXY={c:0.###}");
                //prdDbg($"ElbowVertical {Source}: entry=({entryXY.X:0.###},{entryXY.Y:0.###}), other=({otherXY.X:0.###},{otherXY.Y:0.###})");

                // End elevations using entry slope projected along XY distance
                var zEntry = entryZ;
                // Provisional zOther (for initial sign selection); will be recomputed from arc geometry
                var zOther = entryZ + entrySlope * c;

                // Map endpoints to (u,w)
                var uE = 0.0; var wE = zEntry;
                var uO = c; var wO = zOther;

                // Tangent angles in (u,w) plane
                var alphaE = Math.Atan(entrySlope);
                var theta = Math.Abs(angleDeg) * Math.PI / 180.0;

                var e2 = new Point2d(uE, wE);
                var o2 = new Point2d(uO, wO);
                var dE = new Vector2d(Math.Cos(alphaE), Math.Sin(alphaE));

                // Preferred exit tangent angle strictly from dirUp
                Point2d t2 = default;
                var preferredAlphaO = dirUp ? (alphaE + theta) : (alphaE - theta);
                var chosenAlphaO = preferredAlphaO;

                // Recompute exit elevation from circular-arc geometry:
                // Δu = c = R (sin αO - sin αE)  => R = c / (sin αO - sin αE)
                // Δw = -R (cos αO - cos αE)
                {
                    var denom = Math.Sin(chosenAlphaO) - Math.Sin(alphaE);
                    if (Math.Abs(denom) > 1e-12)
                    {
                        var R = c / denom;
                        var deltaW = -R * (Math.Cos(chosenAlphaO) - Math.Cos(alphaE));
                        zOther = zEntry + deltaW;
                        //prdDbg($"ElbowVertical {Source}: alphaE={alphaE:0.####}, alphaO(preferred)={chosenAlphaO:0.####}, R={R:0.###}, Δw={deltaW:0.###}, zOther={zOther:0.###}");
                    }
                    else
                    {
                        //prdDbg($"ElbowVertical {Source}: near-singular denom for alphaO={chosenAlphaO:0.####}; using provisional zOther.");
                    }
                }

                // With updated zOther, recompute tangent intersection for correct PT
                {
                    var e2b = new Point2d(0.0, zEntry);
                    var o2b = new Point2d(c, zOther);
                    var dEb = new Vector2d(Math.Cos(alphaE), Math.Sin(alphaE));
                    var dOb = new Vector2d(Math.Cos(chosenAlphaO), Math.Sin(chosenAlphaO));
                    if (TryIntersect(e2b, dEb, o2b, dOb, out var ti, out var tFwd, out _))
                    {
                        t2 = ti;
                        if (tFwd <= 1e-9)
                        {
                            //prdDbg($"ElbowVertical {Source}: PT behind entry for preferred alphaO; t={tFwd:0.###}. Trying alternate sign.");
                            t2 = default;
                        }
                    }
                    else
                    {
                        //prdDbg($"ElbowVertical {Source}: intersection failed for preferred alphaO. Trying alternate sign.");
                    }

                    // Fallback to alternate sign if needed
                    if (t2 == default)
                    {
                        var altAlphaO = dirUp ? (alphaE - theta) : (alphaE + theta);
                        var denomAlt = Math.Sin(altAlphaO) - Math.Sin(alphaE);
                        if (Math.Abs(denomAlt) > 1e-12)
                        {
                            var Ralt = c / denomAlt;
                            var dWalt = -Ralt * (Math.Cos(altAlphaO) - Math.Cos(alphaE));
                            var zOtherAlt = zEntry + dWalt;
                            var o2c = new Point2d(c, zOtherAlt);
                            var dOc = new Vector2d(Math.Cos(altAlphaO), Math.Sin(altAlphaO));
                            if (TryIntersect(e2b, dEb, o2c, dOc, out var ti2, out var tFwd2, out _))
                            {
                                if (tFwd2 > 1e-9)
                                {
                                    chosenAlphaO = altAlphaO;
                                    zOther = zEntry + dWalt;
                                    t2 = ti2;
                                    //prdDbg($"ElbowVertical {Source}: using alternate alphaO={chosenAlphaO:0.####}, R={Ralt:0.###}, Δw={dWalt:0.###}, zOther={zOther:0.###}");
                                }
                                else
                                {
                                    //prdDbg($"ElbowVertical {Source}: alternate PT also behind entry (t={tFwd2:0.###}); keeping preferred alpha despite PT issue.");
                                }
                            }
                        }
                    }
                }

                // Map back to world
                Point2d ToXY(double u) => new Point2d(entryXY.X + uHat.X * u, entryXY.Y + uHat.Y * u);

                var aZ = entryIsA ? zEntry : zOther;
                var bZ = entryIsA ? zOther : zEntry;

                var aWorld = new Point3d(a.X, a.Y, aZ);
                var bWorld = new Point3d(b.X, b.Y, bZ);
                var tXY = ToXY(t2.X);
                var tWorld = new Point3d(tXY.X, tXY.Y, t2.Y);
                //prdDbg($"ElbowVertical {Source}: A=({aWorld.X:0.###},{aWorld.Y:0.###},{aWorld.Z:0.###}) B=({bWorld.X:0.###},{bWorld.Y:0.###},{bWorld.Z:0.###}) T=({tWorld.X:0.###},{tWorld.Y:0.###},{tWorld.Z:0.###})");

                var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);

                // Twin offsets should be applied along normal to local centerline, not world Z
                var uHat3 = new Vector3d(uHat.X, uHat.Y, 0.0);
                Vector3d NormalVec(double alpha) =>
                    uHat3.MultiplyBy(-Math.Sin(alpha)) + Vector3d.ZAxis.MultiplyBy(Math.Cos(alpha));
                var nA = NormalVec(alphaE);
                var nB = NormalVec(chosenAlphaO);
                var nT = NormalVec(0.5 * (alphaE + chosenAlphaO));
                Point3d Off(Point3d p, Vector3d n, double off) =>
                    new Point3d(p.X + n.X * off, p.Y + n.Y * off, p.Z + n.Z * off);

                var flowMain = base.ResolveBondedFlowRole(topo);

                g.Members.Add(new RoutedBend(Source, this)
                {
                    A = Off(aWorld, nA, zUp),
                    B = Off(bWorld, nB, zUp),
                    T = Off(tWorld, nT, zUp),
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flowMain,
                    LTG = LTGMain(Source),
                });

                // Single exit: propagate computed Z at other end and same slope                
                var exitZVal = entryIsA ? zOther : zEntry;
                var exitSlopeVal = Math.Tan(chosenAlphaO);
                //prdDbg($"ElbowVertical {Source}: exitZ={exitZVal:0.###}, exitSlope={exitSlopeVal:0.####}");
                exits.Add((otherPort, exitZVal, exitSlopeVal));

                return exits;
            }

            return PropagateUnchanged();
        }

        private TPort GetOtherPort(TPort port)
        {
            foreach (var p in Ports)
            {
                if (!ReferenceEquals(p, port))
                    return p;
            }
            throw new Exception("ElbowVertical has less than 2 ports!");
        }
    }
}