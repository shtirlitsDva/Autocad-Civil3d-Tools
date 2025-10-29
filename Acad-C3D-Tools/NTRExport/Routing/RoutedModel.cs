using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NTRExport.Enums;
using NTRExport.SoilModel;

namespace NTRExport.Routing
{
    internal abstract class RoutedMember
    {
        protected RoutedMember(Handle source) { Source = source; }
        public Handle Source { get; }
        public int Dn { get; set; }
        public string? Material { get; set; }
        public FlowRole FlowRole { get; set; } = FlowRole.Unknown;
        public double ZOffsetMeters { get; set; } = 0.0;
        public string DnSuffix { get; set; } = "s";
        public string LTG { get; init; } = "STD";
    }

    internal sealed class RoutedStraight : RoutedMember
    {
        public RoutedStraight(Handle src) : base(src) { }
        public Point3d A { get; set; }
        public Point3d B { get; set; }
        public SoilProfile Soil { get; set; } = SoilProfile.Default;
        public double Length => A.DistanceTo(B);

        public RoutedStraight With(Point3d a, Point3d b, SoilProfile soil)
        {
            return new RoutedStraight(Source)
            {
                A = a,
                B = b,
                Dn = Dn,
                Material = Material,
                FlowRole = FlowRole,
                DnSuffix = DnSuffix,
                LTG = LTG,
                Soil = soil,
                ZOffsetMeters = ZOffsetMeters,
            };
        }
    }

    internal sealed class RoutedBend : RoutedMember
    {
        public RoutedBend(Handle src) : base(src) { }
        public Point3d A { get; set; }
        public Point3d B { get; set; }
        public Point3d T { get; set; }
    }

    internal sealed class RoutedGraph
    {
        public List<RoutedMember> Members { get; } = new();
    }

    internal sealed class RoutedReducer : RoutedMember
    {
        public RoutedReducer(Handle src) : base(src) { }
        public Point3d P1 { get; set; }
        public Point3d P2 { get; set; }
        public int Dn1 { get; set; }
        public int Dn2 { get; set; }
        public string Dn1Suffix { get; set; } = "s";
        public string Dn2Suffix { get; set; } = "s";
    }

    internal sealed class RoutedTee : RoutedMember
    {
        public RoutedTee(Handle src) : base(src) { }
        public Point3d Ph1 { get; set; }
        public Point3d Ph2 { get; set; }
        public Point3d Pa1 { get; set; }
        public Point3d Pa2 { get; set; }
        public int DnBranch { get; set; }
        public string DnMainSuffix { get; set; } = "s";
        public string DnBranchSuffix { get; set; } = "s";
    }

    internal sealed class RoutedInstrument : RoutedMember
    {
        public RoutedInstrument(Handle src) : base(src) { }
        public Point3d P1 { get; set; }
        public Point3d P2 { get; set; }
        public Point3d Pm { get; set; }
        public int Dn1 { get; set; }
        public int Dn2 { get; set; }
        public string Dn1Suffix { get; set; } = "s";
        public string Dn2Suffix { get; set; } = "s";
    }
}


