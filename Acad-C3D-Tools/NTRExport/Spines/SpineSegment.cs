using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NTRExport.Enums;

namespace NTRExport.Spines
{
    internal abstract class SpineSegment
    {
        protected SpineSegment(Handle source, Point3d a, Point3d b, int dn, FlowRole flowRole)
        {
            Source = source;
            A = a;
            B = b;
            DN = dn;
            FlowRole = flowRole;
        }

        public Handle Source { get; }
        public Point3d A { get; }
        public Point3d B { get; }
        public int DN { get; }
        public FlowRole FlowRole { get; }
        public double Length => A.DistanceTo(B);
    }

    internal sealed class SpineStraight : SpineSegment
    {
        public SpineStraight(Handle source, Point3d a, Point3d b, int dn, FlowRole role)
            : base(source, a, b, dn, role)
        {
        }
    }

    internal sealed class SpineBend : SpineSegment
    {
        public SpineBend(Handle source, Point3d a, Point3d b, Point3d tangent, int dn, FlowRole role)
            : base(source, a, b, dn, role)
        {
            Tangent = tangent;
        }

        public Point3d Tangent { get; }
    }
}


