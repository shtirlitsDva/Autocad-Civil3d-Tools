using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static IntersectUtilities.UtilsCommon.Utils;

namespace NTRExport.Routing
{
    internal class Geometry
    {
        internal readonly record struct BranchFilletSolution(
            Point2d BranchTangent,
            Point2d MainTangent,
            Point2d TangentIntersection,
            Point2d Center,
            double Radius
        );

        internal static double GetBogRadius5D(int dn)
        {
            if (BogRadius5D.TryGetValue(dn, out double radius))
            {
                return radius;
            }
            else
            {
                throw new ArgumentException($"No BOG radius 5D defined for DN {dn}");
            }
        }

        private static readonly Dictionary<int, double> BogRadius5D = new()
        {
            { 15, 45 },
            { 20, 57 },
            { 25, 72 },
            { 32, 93 },
            { 40, 108 },
            { 50, 135 },
            { 65, 175 },
            { 80, 205 },
            { 100, 270 },
            { 125, 330 },
            { 150, 390 },
            { 200, 510 },
            { 250, 650 },
            { 300, 775 },
            { 350, 850 },
            { 400, 970 },
            { 450, 1122 },
            { 500, 1245 },
            { 550, 1000 },
            { 600, 1524 },
            { 700, 1778 },
            { 800, 2033 },
            { 900, 2285 },
            { 1000, 2540 },
        };

        internal static double GetBogRadius3D(int dn)
        {
            if (BogRadius3D.TryGetValue(dn, out double radius))
            {
                return radius;
            }
            else
            {
                throw new ArgumentException($"No BOG radius 3D defined for DN {dn}");
            }
        }

        private static readonly Dictionary<int, double> BogRadius3D = new()
        {
            { 15, 28 },
            { 20, 29 },
            { 25, 38 },
            { 32, 48 },
            { 40, 57 },
            { 50, 76 },
            { 65, 95 },
            { 80, 114 },
            { 100, 152 },
            { 125, 190 },
            { 150, 229 },
            { 200, 305 },
            { 250, 381 },
            { 300, 457 },
            { 350, 533 },
            { 400, 610 },
            { 450, 686 },
            { 500, 762 },
            { 550, 1000 },
            { 600, 914 },
            { 700, 1067 },
            { 800, 1219 },
            { 900, 1372 },
            { 1000, 1524 },
        };

        internal static BranchFilletSolution? SolveBranchFillet(
            Point2d branchStart,
            Point2d branchEnd,
            Point2d mainCentre,
            double bendRadius,
            double stubLength
        )
        {
            const double tol = 1e-9;
            var branchVec = branchEnd - branchStart;
            if (branchVec.Length < tol)
            {
                prdDbg("SolveBranchFillet: invalid branch definition (zero length).");
                return null;
            }

            if (bendRadius <= tol)
            {
                prdDbg("SolveBranchFillet: bend radius must be positive.");
                return null;
            }

            if (stubLength <= tol)
            {
                prdDbg("SolveBranchFillet: stub length must be positive.");
                return null;
            }

            var u = branchVec.GetNormal();

            double s = (mainCentre - branchStart).DotProduct(u);
            var foot = branchStart + u.MultiplyBy(s);
            var w = mainCentre - foot;
            double dLineToM = w.Length;
            if (dLineToM < 1e-12)
            {
                prdDbg("SolveBranchFillet: main centre lies on the branch line.");
                return null;
            }

            var normal = w.MultiplyBy(1.0 / dLineToM);

            double signU = Math.Sign((mainCentre - branchStart).DotProduct(u));
            if (signU == 0)
            {
                signU = Math.Sign((mainCentre - branchEnd).DotProduct(u));
            }
            if (signU == 0)
            {
                signU = 1;
            }

            var or0 = branchStart + normal.MultiplyBy(bendRadius);
            double requiredMo = Math.Sqrt(bendRadius * bendRadius + stubLength * stubLength);
            double sProj = (mainCentre - or0).DotProduct(u);
            var q = or0 + u.MultiplyBy(sProj);
            double dPerp = Math.Abs((mainCentre - or0).DotProduct(normal));
            if (dPerp > requiredMo + 1e-9)
            {
                prdDbg("SolveBranchFillet: no solution, main centre too far from offset line.");
                return null;
            }

            double h = Math.Sqrt(Math.Max(0.0, requiredMo * requiredMo - dPerp * dPerp));
            var center1 = q + u.MultiplyBy(h);
            var center2 = q - u.MultiplyBy(h);

            var branchLine = new Line2d(branchStart, branchEnd);
            var tolGeom = new Tolerance(1e-9, 1e-9);

            BranchFilletSolution? Build(Point2d center)
            {
                var branchTangent = center - normal.MultiplyBy(bendRadius);
                var om = mainCentre - center;
                double d = om.Length;
                if (d <= bendRadius + 1e-9)
                {
                    return null;
                }

                var rhat = om.MultiplyBy(1.0 / d);
                var nhat = new Vector2d(-rhat.Y, rhat.X);
                var ptan = center + om.MultiplyBy((bendRadius * bendRadius) / (d * d));
                double k = (bendRadius / d) * Math.Sqrt(Math.Max(0.0, d * d - bendRadius * bendRadius));

                var candidate1 = ptan + nhat.MultiplyBy(k);
                var candidate2 = ptan - nhat.MultiplyBy(k);

                (Point2d MainTangent, Point2d TangentIntersection, double Sweep)? TryCandidate(Point2d candidate)
                {
                    var tVec = mainCentre - candidate;
                    double tLen = tVec.Length;
                    if (tLen < 1e-12 || Math.Abs(tLen - stubLength) > 1e-6)
                    {
                        return null;
                    }

                    if (tVec.DotProduct(normal) <= 0.0)
                    {
                        return null;
                    }

                    double tu = tVec.DotProduct(u);
                    if (Math.Sign(tu) != signU)
                    {
                        return null;
                    }

                    var stubLine = new Line2d(mainCentre, candidate);
                    var intersections = branchLine.IntersectWith(stubLine, tolGeom);
                    if (intersections == null || intersections.Length == 0)
                    {
                        return null;
                    }

                    var tangentIntersection = intersections[0];

                    double aB = Math.Atan2(branchTangent.Y - center.Y, branchTangent.X - center.X);
                    double aC = Math.Atan2(candidate.Y - center.Y, candidate.X - center.X);
                    double Norm(double angle)
                    {
                        angle %= 2 * Math.PI;
                        if (angle < 0)
                        {
                            angle += 2 * Math.PI;
                        }
                        return angle;
                    }

                    double delta = Norm(aC - aB);
                    double sweep = delta <= Math.PI ? delta : 2 * Math.PI - delta;

                    return (candidate, tangentIntersection, sweep);
                }

                var s1 = TryCandidate(candidate1);
                var s2 = TryCandidate(candidate2);

                if (s1 == null && s2 == null)
                {
                    return null;
                }

                var pick = s1 ?? s2;
                if (s1 != null && s2 != null)
                {
                    pick = s1.Value.Sweep <= s2.Value.Sweep ? s1 : s2;
                }

                return new BranchFilletSolution(
                    branchTangent,
                    pick.Value.MainTangent,
                    pick.Value.TangentIntersection,
                    center,
                    bendRadius
                );
            }

            BranchFilletSolution? solution = Build(center1);
            if (solution == null)
            {
                solution = Build(center2);
            }

            if (solution == null)
            {
                prdDbg("SolveBranchFillet: no admissible solution for given inputs.");
            }

            return solution;
        }
    }
}
