using Autodesk.AutoCAD.DatabaseServices;

using NTRExport.Geometry;
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

        public static string Pt(Pt2 p)
        {
            var x = (p.X - NtrCoord.OffsetX) * MetersToMillimeters;
            var y = (p.Y - NtrCoord.OffsetY) * MetersToMillimeters;
            return $"'" + $"{x:0.#}, {y:0.#}, 0" + "'";
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

    internal abstract class NtrMember
    {
        public int Dn { get; set; } = 0;
        public string? Material { get; set; }
        public IReadOnlyList<Handle> Provenance { get; init; } = Array.Empty<Handle>();
        public abstract IEnumerable<string> ToNtr(INtrSoilAdapter soil);
    }

    internal class NtrPipe : NtrMember
    {
        public Pt2 A { get; init; }
        public Pt2 B { get; init; }
        public SoilProfile Soil { get; set; } = SoilProfile.Default;
        public string DnSuffix { get; init; } = "s"; // "s" or "t" from variant
        public double Length => Math.Sqrt(Math.Pow(B.X - A.X, 2) + Math.Pow(B.Y - A.Y, 2));

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield return $"RO P1={NtrFormat.Pt(A)} P2={NtrFormat.Pt(B)} DN=DN{Dn}.{DnSuffix}" +
                (Material != null ? $" MAT={Material}" : "") +
                NtrFormat.SoilTokens(Soil);
        }

        public NtrPipe With(Pt2 a, Pt2 b, SoilProfile s) => new()
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
        public Pt2 A { get; init; }     // end 1
        public Pt2 B { get; init; }     // end 2
        public Pt2 T { get; init; }     // tangency/angle point
        public string DnSuffix { get; init; } = "s";
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield return $"BOG P1={NtrFormat.Pt(A)} P2={NtrFormat.Pt(B)} PT={NtrFormat.Pt(T)} DN=DN{Dn}.{DnSuffix}" +
                         (Material != null ? $" MAT={Material}" : "") +
                         NtrFormat.SoilTokens(null);
        }
    }

    internal class NtrTee : NtrMember
    {
        public Pt2 Ph1 { get; init; }
        public Pt2 Ph2 { get; init; }
        public Pt2 Pa1 { get; init; }
        public Pt2 Pa2 { get; init; }
        public int DnBranch { get; init; } = 0;
        public string DnMainSuffix { get; init; } = "s";
        public string DnBranchSuffix { get; init; } = "s";
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield return $"TEE PH1={NtrFormat.Pt(Ph1)} PH2={NtrFormat.Pt(Ph2)} PA1={NtrFormat.Pt(Pa1)} PA2={NtrFormat.Pt(Pa2)} " +
                         $"DNH=DN{Dn}.{DnMainSuffix} DNA=DN{DnBranch}.{DnBranchSuffix}" + (Material != null ? $" MAT={Material}" : "") +
                         NtrFormat.SoilTokens(null);
        }
    }

    internal class NtrReducer : NtrMember
    {
        public Pt2 P1 { get; init; }
        public Pt2 P2 { get; init; }
        public int Dn1 { get; init; }
        public int Dn2 { get; init; }
        public string Dn1Suffix { get; init; } = "s";
        public string Dn2Suffix { get; init; } = "s";
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield return $"RED P1={NtrFormat.Pt(P1)} P2={NtrFormat.Pt(P2)} DN1=DN{Dn1}.{Dn1Suffix} DN2=DN{Dn2}.{Dn2Suffix}" +
                         (Material != null ? $" MAT={Material}" : "") +
                         NtrFormat.SoilTokens(null);
        }
    }

    internal class NtrInstrument : NtrMember
    {
        public Pt2 P1 { get; init; }
        public Pt2 P2 { get; init; }
        public Pt2 Pm { get; init; }
        public int Dn1 { get; init; }
        public int Dn2 { get; init; }
        public string Dn1Suffix { get; init; } = "s";
        public string Dn2Suffix { get; init; } = "s";
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield return $"ARM P1={NtrFormat.Pt(P1)} P2={NtrFormat.Pt(P2)} PM={NtrFormat.Pt(Pm)} DN1=DN{Dn1}.{Dn1Suffix} DN2=DN{Dn2}.{Dn2Suffix}" +
                         (Material != null ? $" MAT={Material}" : "") +
                         NtrFormat.SoilTokens(null);
        }
    }

    internal class NtrStub : NtrMember
    {
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield break; // placeholder for F/Y models or unsupported types
        }
    }

    internal class NtrGraph
    {
        public List<NtrMember> Members { get; } = new();

        // Collect DN usages for catalog production
        public IEnumerable<int> EnumerateDns()
        {
            foreach (var m in Members)
            {
                switch (m)
                {
                    case NtrPipe p:
                        if (p.Dn > 0) yield return p.Dn; break;
                    case NtrBend b:
                        if (b.Dn > 0) yield return b.Dn; break;
                    case NtrTee t:
                        if (t.Dn > 0) yield return t.Dn; if (t.DnBranch > 0) yield return t.DnBranch; break;
                    case NtrReducer r:
                        if (r.Dn1 > 0) yield return r.Dn1; if (r.Dn2 > 0) yield return r.Dn2; break;
                    case NtrInstrument i:
                        if (i.Dn1 > 0) yield return i.Dn1; if (i.Dn2 > 0) yield return i.Dn2; break;
                }
            }
        }
    }
}
