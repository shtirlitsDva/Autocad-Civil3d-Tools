using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System.Collections.Generic;

using NTRExport.Enums;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
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

        public abstract IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf);

        protected string Last(ConfigurationData conf)
        {
            if (conf == null) return string.Empty;

            var last = FlowRole switch
            {
                FlowRole.Supply => conf.SupplyLast,
                FlowRole.Return => conf.ReturnLast,
                _ => null
            };

            return last != null ? " " + last.EmitRecord() : string.Empty;
        }

        protected string PipelineToken => string.IsNullOrWhiteSpace(LTG) ? string.Empty : " LTG=" + LTG;
    }

    internal sealed class RoutedStraight : RoutedMember
    {
        public RoutedStraight(Handle src) : base(src) { }
        public Point3d A { get; set; }
        public Point3d B { get; set; }
        public SoilProfile Soil { get; set; } = SoilProfile.Default;
        public double Length => A.DistanceTo(B);

        public RoutedStraight WithSegment(Point3d a, Point3d b, SoilProfile soil) => new(Source)
        {
            A = a,
            B = b,
            Soil = soil,
            Dn = this.Dn,
            Material = this.Material,
            FlowRole = this.FlowRole,
            ZOffsetMeters = this.ZOffsetMeters,
            DnSuffix = this.DnSuffix,
            LTG = this.LTG,
        };

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return "RO " +
                $"P1={NtrFormat.Pt(A)} " +
                $"P2={NtrFormat.Pt(B)} " +
                $"DN=DN{Dn}.{DnSuffix}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                PipelineToken +
                NtrFormat.SoilTokens(Soil);
        }
    }

    internal sealed class RoutedBend : RoutedMember
    {
        public RoutedBend(Handle src) : base(src) { }
        public Point3d A { get; set; }
        public Point3d B { get; set; }
        public Point3d T { get; set; }

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return "BOG " +
                $"P1={NtrFormat.Pt(A)} " +
                $"P2={NtrFormat.Pt(B)} " +
                $"PT={NtrFormat.Pt(T)} " +
                $"DN=DN{Dn}.{DnSuffix}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                PipelineToken +
                NtrFormat.SoilTokens(null);
        }
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

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return "RED " +
                $"P1={NtrFormat.Pt(P1)} " +
                $"P2={NtrFormat.Pt(P2)} " +
                $"DN1=DN{Dn1}.{Dn1Suffix} " +
                $"DN2=DN{Dn2}.{Dn2Suffix}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                PipelineToken +
                NtrFormat.SoilTokens(null);
        }
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

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return "TEE " +
                $"PH1={NtrFormat.Pt(Ph1)} " +
                $"PH2={NtrFormat.Pt(Ph2)} " +
                $"PA1={NtrFormat.Pt(Pa1)} " +
                $"PA2={NtrFormat.Pt(Pa2)} " +
                $"DNH=DN{Dn}.{DnMainSuffix} " +
                $"DNA=DN{DnBranch}.{DnBranchSuffix}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                PipelineToken +
                NtrFormat.SoilTokens(null);
        }
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

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return "ARM " +
                $"P1={NtrFormat.Pt(P1)} " +
                $"P2={NtrFormat.Pt(P2)} " +
                $"PM={NtrFormat.Pt(Pm)} " +
                $"DN1=DN{Dn1}.{Dn1Suffix} " +
                $"DN2=DN{Dn2}.{Dn2Suffix}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                PipelineToken +
                NtrFormat.SoilTokens(null);
        }
    }
}


