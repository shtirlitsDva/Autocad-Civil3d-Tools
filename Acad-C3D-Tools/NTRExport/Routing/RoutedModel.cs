using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NTRExport.SoilModel;

namespace NTRExport.Routing
{
    internal enum RoutedFlow { Unknown, Supply, Return }

    internal abstract class RoutedMember
    {
        protected RoutedMember(Handle source) { Source = source; }
        public Handle Source { get; }
        public int Dn { get; set; }
        public string? Material { get; set; }
        public RoutedFlow Flow { get; set; } = RoutedFlow.Unknown;
        public double ZOffsetMeters { get; set; } = 0.0;
        public string DnSuffix { get; set; } = "s";
    }

    internal sealed class RoutedStraight : RoutedMember
    {
        public RoutedStraight(Handle src) : base(src) { }
        public Point2d A { get; set; }
        public Point2d B { get; set; }
        public SoilProfile Soil { get; set; } = SoilProfile.Default;
        public double? ZA { get; set; }
        public double? ZB { get; set; }
    }

    internal sealed class RoutedBend : RoutedMember
    {
        public RoutedBend(Handle src) : base(src) { }
        public Point2d A { get; set; }
        public Point2d B { get; set; }
        public Point2d T { get; set; }
        // Optional distinct Z values per bend point; when null, ZOffsetMeters is used for all
        public double? Z1 { get; set; }
        public double? Z2 { get; set; }
        public double? Zt { get; set; }
    }

    internal sealed class RoutedGraph
    {
        public List<RoutedMember> Members { get; } = new();
    }

    internal sealed class RoutedReducer : RoutedMember
    {
        public RoutedReducer(Handle src) : base(src) { }
        public Point2d P1 { get; set; }
        public Point2d P2 { get; set; }
        public int Dn1 { get; set; }
        public int Dn2 { get; set; }
        public string Dn1Suffix { get; set; } = "s";
        public string Dn2Suffix { get; set; } = "s";
    }

    internal sealed class RoutedTee : RoutedMember
    {
        public RoutedTee(Handle src) : base(src) { }
        public Point2d Ph1 { get; set; }
        public Point2d Ph2 { get; set; }
        public Point2d Pa1 { get; set; }
        public Point2d Pa2 { get; set; }
        public int DnBranch { get; set; }
        public string DnMainSuffix { get; set; } = "s";
        public string DnBranchSuffix { get; set; } = "s";
    }
}


