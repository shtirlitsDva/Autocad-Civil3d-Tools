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
    internal class TPipe : ElementBase
    {
        public TPort A { get; }
        public TPort B { get; }
        public override int DN => PipeScheduleV2.GetPipeDN(_entity);

        // Cushion spans along this pipe in meters (s0,s1) from A→B
        public List<(double s0, double s1)> CushionSpans { get; } = new();
        public double Length
        {
            get
            {
                var dx = B.Node.Pos.X - A.Node.Pos.X;
                var dy = B.Node.Pos.Y - A.Node.Pos.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public TPipe(Handle h, Curve2d s, Func<TPipe, TPort> makeA, Func<TPipe, TPort> makeB) : base(h)
        {
            A = makeA(this);
            B = makeB(this);
        }

        public override IReadOnlyList<TPort> Ports => [A, B];

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();
            var other = ReferenceEquals(entryPort, A) ? B : A;

            double ZAtParam(double t)
            {
                var s = ReferenceEquals(entryPort, A) ? t * Length : (1.0 - t) * Length;
                return entryZ + entrySlope * s;
            }
            double exitZ = ZAtParam(1.0);
            exits.Add((other, exitZ, entrySlope));

            var isTwin = Variant.IsTwin;
            var suffix = Variant.DnSuffix;
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var flow = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;
            var ltg = LTGMain(Source);

            // Build local normal to centerline (in vertical plane spanned by XY direction and Z)
            var abXY = (B.Node.Pos.To2d() - A.Node.Pos.To2d());
            var uHatAbs = abXY.Length < 1e-12 ? new Vector2d(1.0, 0.0) : abXY.GetNormal();
            var signEntryToAB = ReferenceEquals(entryPort, A) ? 1.0 : -1.0;
            var alphaAbs = Math.Atan(entrySlope) * signEntryToAB;
            var upVec3 =
                new Vector3d(uHatAbs.X, uHatAbs.Y, 0.0).MultiplyBy(-Math.Sin(alphaAbs)) +
                Vector3d.ZAxis.MultiplyBy(Math.Cos(alphaAbs));
            Point3d Off(Point3d p, double off) =>
                new Point3d(p.X + upVec3.X * off, p.Y + upVec3.Y * off, p.Z + upVec3.Z * off);

            var cuts = new SortedSet<double> { 0.0, Length };
            foreach (var (s0, s1) in CushionSpans)
            {
                cuts.Add(Math.Max(0.0, Math.Min(Length, s0)));
                cuts.Add(Math.Max(0.0, Math.Min(Length, s1)));
            }
            var segments = cuts.ToList();
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var s0 = segments[i];
                var s1 = segments[i + 1];
                if (s1 - s0 < 1e-6) continue;

                var soil = IsCovered(CushionSpans, s0, s1) ? new SoilProfile("Soil_C80", 0.08) : SoilProfile.Default;

                var t0 = Length <= 1e-9 ? 0.0 : s0 / Length;
                var t1 = Length <= 1e-9 ? 0.0 : s1 / Length;

                var aPos = Lerp(A.Node.Pos, B.Node.Pos, t0);
                var bPos = Lerp(A.Node.Pos, B.Node.Pos, t1);

                var zCenterA = ZAtParam(t0);
                var zCenterB = ZAtParam(t1);
                var aCenter = new Point3d(aPos.X, aPos.Y, zCenterA);
                var bCenter = new Point3d(bPos.X, bPos.Y, zCenterB);

                if (isTwin)
                {
                    g.Members.Add(new RoutedStraight(Source, this)
                        {
                            A = Off(aCenter, zUp),
                            B = Off(bCenter, zUp),
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = FlowRole.Return,
                            Soil = soil,
                            LTG = ltg,
                    });
                    g.Members.Add(new RoutedStraight(Source, this)
                        {
                            A = Off(aCenter, zLow),
                            B = Off(bCenter, zLow),
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = FlowRole.Supply,
                            Soil = soil,
                            LTG = ltg,
                    });
                }
                else
                {
                    g.Members.Add(new RoutedStraight(Source, this)
                        {
                            A = aCenter,
                            B = bCenter,
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = flow,
                            Soil = soil,
                            LTG = ltg,
                    });
                        }
                }

            return exits;
        }


        private static bool IsCovered(List<(double s0, double s1)> spans, double a, double b)
        {
            var mid = 0.5 * (a + b);
            return spans.Any(z => mid >= z.s0 - 1e-9 && mid <= z.s1 + 1e-9);
        }

        private static Point3d Lerp(Point3d a, Point3d b, double t) =>
            new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));
    }
}

