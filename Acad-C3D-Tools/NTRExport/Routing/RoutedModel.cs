using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System.Collections.Generic;

using NTRExport.Enums;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.SoilModel;
using IntersectUtilities.UtilsCommon.Enums;
using NTRExport.TopologyModel;

namespace NTRExport.Routing
{
    internal abstract class RoutedMember
    {
        protected RoutedMember(Handle source, ElementBase elementBase)
        { Source = source; Emitter = elementBase; }
        public Handle Source { get; }
        public ElementBase Emitter { get; } 
        public int DN { get; set; }
        public string? Material { get; set; }
        public string Norm { get; init; } = "";
        protected string NormField => string.IsNullOrEmpty(Norm) ? string.Empty : $" NORM=\'{Norm}\'";
        public FlowRole FlowRole { get; set; } = FlowRole.Unknown;
        public double ZOffsetMeters { get; set; } = 0.0;
        public string DnSuffix { get; set; } = "s";
        public string LTG { get; init; } = "STD";
        public PipeSystemEnum System => Emitter.System;
        public PipeTypeEnum Type => Emitter.Type;
        public PipeSeriesEnum Series => Emitter.Series;

        protected string FormatDnSuffix(string baseSuffix)
        {
            var seriesSuffix = Series switch
            {
                PipeSeriesEnum.S1 => "s1",
                PipeSeriesEnum.S2 => "s2",
                PipeSeriesEnum.S3 => "s3",
                _ => string.Empty
            };
            return baseSuffix + seriesSuffix;
        }

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
        public RoutedStraight(Handle src, ElementBase elementBase) : base(src, elementBase) { }
        public Point3d A { get; set; }
        public Point3d B { get; set; }
        public SoilProfile Soil { get; set; } = SoilProfile.Default;
        public double Length => A.DistanceTo(B);

        public RoutedStraight WithSegment(Point3d a, Point3d b, SoilProfile soil) => new(Source, Emitter)
        {
            A = a,
            B = b,
            Soil = soil,
            DN = this.DN,
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
                $"DN=DN{DN}.{FormatDnSuffix(DnSuffix)}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                PipelineToken +
                NtrFormat.SoilTokens(Soil);
        }
    }

    internal sealed class RoutedBend : RoutedMember
    {
        public RoutedBend(Handle src, ElementBase elementBase) : base(src, elementBase) { }
        public Point3d A { get; set; }
        public Point3d B { get; set; }
        public Point3d T { get; set; }        

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return "BOG " +
                $"P1={NtrFormat.Pt(A)} " +
                $"P2={NtrFormat.Pt(B)} " +
                $"PT={NtrFormat.Pt(T)} " +
                $"DN=DN{DN}.{FormatDnSuffix(DnSuffix)}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                NormField +
                PipelineToken +
                NtrFormat.SoilTokens(null);
        }
    }

    internal sealed class RoutedRigid : RoutedMember
    {
        public RoutedRigid(Handle src, ElementBase elementBase) : base(src, elementBase) { }
        public Point3d P1 { get; set; }
        public Point3d P2 { get; set; }

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return "PROF" +
                $" P1={NtrFormat.Pt(P1)}" +
                $" P2={NtrFormat.Pt(P2)}" +
                 " MAT=P235GH" +
                " TYP=_RIGID_";                
        }
    }

    internal sealed class RoutedGraph
    {
        public List<RoutedMember> Members { get; } = new();
    }

    internal sealed class RoutedReducer : RoutedMember
    {
        public RoutedReducer(Handle src, ElementBase elementBase) : base(src, elementBase) { }
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
                $"DN1=DN{Dn1}.{FormatDnSuffix(Dn1Suffix)} " +
                $"DN2=DN{Dn2}.{FormatDnSuffix(Dn2Suffix)}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                NormField +
                PipelineToken +
                NtrFormat.SoilTokens(null);
        }
    }

    internal sealed class RoutedTee : RoutedMember
    {
        public RoutedTee(Handle src, ElementBase elementBase) : base(src, elementBase) { }
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
                $"DNH=DN{DN}.{FormatDnSuffix(DnMainSuffix)} " +
                $"DNA=DN{DnBranch}.{FormatDnSuffix(DnBranchSuffix)}" +
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                NormField +
                PipelineToken +
                NtrFormat.SoilTokens(null);
        }
    }

    internal sealed class RoutedValve : RoutedMember
    {
        public RoutedValve(Handle src, ElementBase elementBase) : base(src, elementBase) { }
        public Point3d P1 { get; set; }
        public Point3d P2 { get; set; }
        public Point3d Pm { get; set; }
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return "ARM " +
                $"P1={NtrFormat.Pt(P1)} " +
                $"P2={NtrFormat.Pt(P2)} " +
                $"PM={NtrFormat.Pt(Pm)} " +
                $"DN1=DN{DN}.{FormatDnSuffix(DnSuffix)} " +
                $"DN2=DN{DN}.{FormatDnSuffix(DnSuffix)} " +                
                (Material != null ? $" MAT={Material}" : string.Empty) +
                Last(conf) +
                PipelineToken +
                NtrFormat.SoilTokens(null);
        }
    }
}


