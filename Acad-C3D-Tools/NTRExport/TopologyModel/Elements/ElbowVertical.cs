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

            // Vertical elbow projected in plan: compute tangent intersection from endpoints and angle
            var ends = Ports.Take(2).ToArray();
            if (ends.Length >= 2)
            {
                var aPort = ends[0];
                var bPort = ends[1];
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
                    foreach (var p in Ports)
                    {
                        if (ReferenceEquals(p, entryPort)) continue;
                        exits.Add((p, entryZ, entrySlope));
                    }
                    return exits;
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
                var uO = c;   var wO = zOther;

                // Tangent angles in (u,w) plane
                var alphaE = Math.Atan(entrySlope);
                var theta = Math.Abs(angleDeg) * Math.PI / 180.0;

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

                var flowMain = Variant.IsTwin ? FlowRole.Return : Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;

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

                if (Variant.IsTwin)
                {
                    g.Members.Add(new RoutedBend(Source, this)
                    {
                        A = Off(aWorld, nA, zLow),
                        B = Off(bWorld, nB, zLow),
                        T = Off(tWorld, nT, zLow),
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    });
                }

                // Single exit: propagate computed Z at other end and same slope                
                var exitZVal = entryIsA ? zOther : zEntry;
                var exitSlopeVal = Math.Tan(chosenAlphaO);
                //prdDbg($"ElbowVertical {Source}: exitZ={exitZVal:0.###}, exitSlope={exitSlopeVal:0.####}");
                exits.Add((otherPort, exitZVal, exitSlopeVal));

                return exits;
            }

            // Fallback: propagate unchanged
            foreach (var p in Ports)
            {
                if (ReferenceEquals(p, entryPort)) continue;
                exits.Add((p, entryZ, entrySlope));
            }
            return exits;
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