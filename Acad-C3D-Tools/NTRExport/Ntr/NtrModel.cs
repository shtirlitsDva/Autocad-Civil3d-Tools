using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NTRExport.NtrConfiguration;
using NTRExport.SoilModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Ntr
{
    internal static class NtrFormat
    {
        public const double MetersToMillimeters = 1000.0;
        public const double DefaultSoilCoverM = 0.6;      // SOIL_H
        public const double CushionThkM = 0.08;           // SOIL_CUSH_THK

        public static string Pt(Point2d p, double zMeters = 0.0)
        {
            var x = (p.X - NtrCoord.OffsetX) * MetersToMillimeters;
            var y = (p.Y - NtrCoord.OffsetY) * MetersToMillimeters;
            var z = zMeters * MetersToMillimeters;
            return $"'" + $"{x:0.#}, {y:0.#}, {z:0.#}" + "'";
        }

        public static string SoilTokens(SoilProfile? soil)
        {
            // Always include cover; add cushion tokens when present
            var baseTok = $" SOIL_H={DefaultSoilCoverM:0.###}";
            if (soil != null && soil.CushionThk > 0)
                return baseTok + $" SOIL_CUSH_TYPE=2 SOIL_CUSH_THK={CushionThkM:0.###}";
            return baseTok;
        }
    }

    internal enum FlowRole { Unknown, Supply, Return }

    internal abstract class NtrMember
    {
        protected NtrMember(Handle source)
        {
            Source = source;
        }

        public Handle Source { get; }
        public int Dn { get; set; } = 0;
        public string? Material { get; set; }
        public IReadOnlyList<Handle> Provenance { get; init; } = Array.Empty<Handle>();
        public FlowRole Flow { get; set; } = FlowRole.Unknown;
        public double ZOffsetMeters { get; set; } = 0.0;
        public abstract IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf);
    }

    internal class NtrPipe : NtrMember
    {
        public NtrPipe(Handle source) : base(source)
        {
        }

        public Point2d A { get; init; }
        public Point2d B { get; init; }
        public SoilProfile Soil { get; set; } = SoilProfile.Default;
        public string DnSuffix { get; init; } = "s"; // "s" or "t" from variant
        public double Length => A.GetDistanceTo(B);


        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            string last = "";
            if (conf != null)
            {
                var Last = Flow switch
                {
                    FlowRole.Supply => conf.SupplyLast,
                    FlowRole.Return => conf.ReturnLast,
                    _ => null
                };

                if (Last != null)
                {
                    last = " " + Last.EmitRecord() + " ";
                }
            }

            yield return $"RO P1={NtrFormat.Pt(A, ZOffsetMeters)} P2={NtrFormat.Pt(B, ZOffsetMeters)} DN=DN{Dn}.{DnSuffix}" +
                (Material != null ? $" MAT={Material}" : "") +
                last +
                NtrFormat.SoilTokens(Soil);
        }

        public NtrPipe With(Point2d a, Point2d b, SoilProfile s) => new(Source)
        {
            A = a,
            B = b,
            Dn = Dn,
            Material = Material,
            Soil = s,
            Provenance = Provenance
        };
    }

    internal class NtrBend : NtrMember
    {
        public NtrBend(Handle source) : base(source)
        {
        }

        public Point2d A { get; init; }     // end 1
        public Point2d B { get; init; }     // end 2
        public Point2d T { get; init; }     // tangency/angle point
        public string DnSuffix { get; init; } = "s";
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return $"BOG P1={NtrFormat.Pt(A, ZOffsetMeters)} P2={NtrFormat.Pt(B, ZOffsetMeters)} PT={NtrFormat.Pt(T, ZOffsetMeters)} DN=DN{Dn}.{DnSuffix}" +
                         (Material != null ? $" MAT={Material}" : "") +
                         NtrFormat.SoilTokens(null);
        }
    }

    internal class NtrTee : NtrMember
    {
        public NtrTee(Handle source) : base(source)
        {
        }

        public Point2d Ph1 { get; init; }
        public Point2d Ph2 { get; init; }
        public Point2d Pa1 { get; init; }
        public Point2d Pa2 { get; init; }
        public int DnBranch { get; init; } = 0;
        public string DnMainSuffix { get; init; } = "s";
        public string DnBranchSuffix { get; init; } = "s";
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return $"TEE PH1={NtrFormat.Pt(Ph1, ZOffsetMeters)} PH2={NtrFormat.Pt(Ph2, ZOffsetMeters)} PA1={NtrFormat.Pt(Pa1, ZOffsetMeters)} PA2={NtrFormat.Pt(Pa2, ZOffsetMeters)} " +
                         $"DNH=DN{Dn}.{DnMainSuffix} DNA=DN{DnBranch}.{DnBranchSuffix}" + (Material != null ? $" MAT={Material}" : "") +
                         NtrFormat.SoilTokens(null);
        }
    }

    internal class NtrReducer : NtrMember
    {
        public NtrReducer(Handle source) : base(source)
        {
        }

        public Point2d P1 { get; init; }
        public Point2d P2 { get; init; }
        public int Dn1 { get; init; }
        public int Dn2 { get; init; }
        public string Dn1Suffix { get; init; } = "s";
        public string Dn2Suffix { get; init; } = "s";
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return $"RED P1={NtrFormat.Pt(P1, ZOffsetMeters)} P2={NtrFormat.Pt(P2, ZOffsetMeters)} DN1=DN{Dn1}.{Dn1Suffix} DN2=DN{Dn2}.{Dn2Suffix}" +
                         (Material != null ? $" MAT={Material}" : "") +
                         NtrFormat.SoilTokens(null);
        }
    }

    internal class NtrInstrument : NtrMember
    {
        public NtrInstrument(Handle source) : base(source)
        {
        }

        public Point2d P1 { get; init; }
        public Point2d P2 { get; init; }
        public Point2d Pm { get; init; }
        public int Dn1 { get; init; }
        public int Dn2 { get; init; }
        public string Dn1Suffix { get; init; } = "s";
        public string Dn2Suffix { get; init; } = "s";
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield return $"ARM P1={NtrFormat.Pt(P1, ZOffsetMeters)} P2={NtrFormat.Pt(P2, ZOffsetMeters)} PM={NtrFormat.Pt(Pm, ZOffsetMeters)} DN1=DN{Dn1}.{Dn1Suffix} DN2=DN{Dn2}.{Dn2Suffix}" +
                         (Material != null ? $" MAT={Material}" : "") +
                         NtrFormat.SoilTokens(null);
        }
    }

    internal class NtrStub : NtrMember
    {
        public NtrStub(Handle source) : base(source)
        {
        }

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil, ConfigurationData conf)
        {
            yield break; // placeholder for F/Y models or unsupported types
        }
    }

    internal class NtrGraph
    {
        public List<NtrMember> Members { get; } = new();        
    }
}
