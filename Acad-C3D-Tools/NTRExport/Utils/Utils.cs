using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities;

using static IntersectUtilities.UtilsCommon.Utils;

namespace NTRExport.Utils
{
    internal static class Utils
    {
        public static string LTGMain(Handle source)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var ent = source.Go<Entity>(db);

            return PropertySetManager.ReadNonDefinedPropertySetString(
                ent, "DriPipelineData", "BelongsToAlignment");
        }

        public static string LTGBranch(Handle source)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var ent = source.Go<Entity>(db);

            return PropertySetManager.ReadNonDefinedPropertySetString(
                ent, "DriPipelineData", "BranchesOffToAlignment");
        }

        public static Point3d GetTangentPoint(CircularArc2d arc)
        {
            // Simple version for XY-plane arcs - maintains backward compatibility
            var s = arc.StartPoint;
            var e = arc.EndPoint;
            var c = arc.Center;

            var rs = s - c;
            var re = e - c;

            var ts = new Vector2d(-rs.Y, rs.X);
            var te = new Vector2d(-re.Y, re.X);

            var denom = ts.X * te.Y - ts.Y * te.X;

            if (Math.Abs(denom) < 1e-9)
            {
                prdDbg($"Parallel tangents! {denom} {ts} {te}");
                return default;
            }

            var es = e - s;
            var l = (es.X * te.Y - es.Y * te.X) / denom;

            var inter = s + ts.MultiplyBy(l);

            return inter.To3d();
        }

        public static Point3d GetTangentPoint(CircularArc2d arc, Vector3d planeNormal, Point3d planeOrigin)
        {
            // Work in the specified plane to handle rotated geometry
            var n = planeNormal.GetNormal();
            
            // Build orthonormal basis (u, v) for the plane
            Vector3d u, v;
            if (Math.Abs(Math.Abs(n.DotProduct(Vector3d.ZAxis)) - 1.0) < 1e-9)
            {
                // Plane is XY plane (or parallel), use standard basis
                u = Vector3d.XAxis;
                v = Vector3d.YAxis;
            }
            else
            {
                // Build basis from plane normal
                var z = Vector3d.ZAxis;
                u = n.CrossProduct(z).GetNormal();
                v = n.CrossProduct(u).GetNormal();
            }

            // Convert 2D arc points to 3D points in the plane
            Point3d ToPlane3D(Point2d p2d)
            {
                return planeOrigin + (u.MultiplyBy(p2d.X)) + (v.MultiplyBy(p2d.Y));
            }

            // Convert 3D points back to 2D coordinates in plane
            Vector2d ToPlane2D(Point3d p3d)
            {
                var w = p3d - planeOrigin;
                return new Vector2d(w.DotProduct(u), w.DotProduct(v));
            }

            // Get 3D points in the plane
            var s3 = ToPlane3D(arc.StartPoint);
            var e3 = ToPlane3D(arc.EndPoint);
            var c3 = ToPlane3D(arc.Center);

            // Convert to 2D for calculation
            var s = ToPlane2D(s3);
            var e = ToPlane2D(e3);
            var c = ToPlane2D(c3);

            var rs = s - c;
            var re = e - c;

            var ts = new Vector2d(-rs.Y, rs.X);
            var te = new Vector2d(-re.Y, re.X);

            var denom = ts.X * te.Y - ts.Y * te.X;

            if (Math.Abs(denom) < 1e-9)
            {
                prdDbg($"Parallel tangents! {denom} {ts} {te}");
                return default;
            }

            var es = e - s;
            var l = (es.X * te.Y - es.Y * te.X) / denom;
            var inter2 = s + ts.MultiplyBy(l);

            // Lift back to 3D in the plane
            var inter3 = planeOrigin + (u.MultiplyBy(inter2.X)) + (v.MultiplyBy(inter2.Y));
            return inter3;
        }

        public static Point3d GetTangentPoint(CircularArc2d arc, Matrix3d transform)
        {
            // Extract coordinate system from transform matrix
            // Transform the standard basis vectors to get the coordinate system
            var origin = Point3d.Origin.TransformBy(transform);
            var xAxisPt = (Point3d.Origin + Vector3d.XAxis).TransformBy(transform);
            var yAxisPt = (Point3d.Origin + Vector3d.YAxis).TransformBy(transform);
            var zAxisPt = (Point3d.Origin + Vector3d.ZAxis).TransformBy(transform);
            
            var zAxis = (zAxisPt - origin).GetNormal();
            
            return GetTangentPoint(arc, zAxis, origin);
        }

        public static Point3d GetTangentPoint(Arc arc)
        {
            // Work in the local plane of the arc to avoid XY projection errors
            var c3 = arc.Center;
            var n = arc.Normal.GetNormal();
            var u = (arc.StartPoint - c3);
            if (u.Length < 1e-9) u = (arc.EndPoint - c3);
            if (u.Length < 1e-9)
            {
                prdDbg($"Degenerate arc for tangent computation: {arc.Handle}");
                return default;
            }
            u = u.GetNormal();
            var v = n.CrossProduct(u).GetNormal();

            // Project 3D points into 2D plane coords relative to center
            Vector2d To2D(Point3d p)
            {
                var w = p - c3;
                return new Vector2d(w.DotProduct(u), w.DotProduct(v));
            }

            var s = To2D(arc.StartPoint);
            var e = To2D(arc.EndPoint);
            var c = new Vector2d(0.0, 0.0);

            var rs = s - c;
            var re = e - c;

            var ts = new Vector2d(-rs.Y, rs.X);
            var te = new Vector2d(-re.Y, re.X);

            var denom = ts.X * te.Y - ts.Y * te.X;
            if (Math.Abs(denom) < 1e-9)
            {
                prdDbg($"Parallel tangents! {denom} {ts} {te}");
                return default;
            }

            var es = e - s;
            var l = (es.X * te.Y - es.Y * te.X) / denom;
            var inter2 = s + ts.MultiplyBy(l);

            // Lift back to 3D
            var inter3 = c3 + (u.MultiplyBy(inter2.X)) + (v.MultiplyBy(inter2.Y));
            return inter3;
        }

        public static Point3d GetTangentPoint(Arc arc, Matrix3d blockTransform)
        {
            var p = GetTangentPoint(arc);
            return p == default ? default : p.TransformBy(blockTransform);
        }
    }
}
