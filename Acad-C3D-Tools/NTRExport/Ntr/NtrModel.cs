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
        public double Length => Math.Sqrt(Math.Pow(B.X - A.X, 2) + Math.Pow(B.Y - A.Y, 2));

        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            var soilRef = soil.RefToken(Soil);
            yield return $"RO P1=({A.X},{A.Y}) P2=({B.X},{B.Y}) DN={Dn}" +
                (Material != null ? $" MAT={Material}" : "") + (soilRef != null ? $" {soilRef}" : "");
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
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield return $"BOG P1=({A.X},{A.Y}) P2=({B.X},{B.Y}) PT=({T.X},{T.Y}) DN={Dn}" +
                         (Material != null ? $" MAT={Material}" : "");
        }
    }

    internal class NtrTee : NtrMember
    {
        public Pt2 Ph1 { get; init; }
        public Pt2 Ph2 { get; init; }
        public Pt2 Pa1 { get; init; }
        public Pt2 Pa2 { get; init; }
        public int DnBranch { get; init; } = 0;
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield return $"TEE PH1=({Ph1.X},{Ph1.Y}) PH2=({Ph2.X},{Ph2.Y}) PA1=({Pa1.X},{Pa1.Y}) PA2=({Pa2.X},{Pa2.Y}) " +
                         $"DNH={Dn} DNA={DnBranch}" + (Material != null ? $" MAT={Material}" : "");
        }
    }

    internal class NtrReducer : NtrMember
    {
        public Pt2 Ph1 { get; init; }
        public Pt2 Ph2 { get; init; }
        public Pt2 Pa1 { get; init; }
        public Pt2 Pa2 { get; init; }
        public int DnBranch { get; init; } = 0;
        public override IEnumerable<string> ToNtr(INtrSoilAdapter soil)
        {
            yield return $"TEE PH1=({Ph1.X},{Ph1.Y}) PH2=({Ph2.X},{Ph2.Y}) PA1=({Pa1.X},{Pa1.Y}) PA2=({Pa2.X},{Pa2.Y}) " +
                         $"DNH={Dn} DNA={DnBranch}" + (Material != null ? $" MAT={Material}" : "");
        }
    }

    internal class NtrGraph
    {
        public List<NtrMember> Members { get; } = new();
    }
}
