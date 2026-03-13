using System;
using System.Collections.Generic;
using System.Text;

using MessagePack;

namespace NorsynHydraulicCalc.Pipes
{
    [MessagePackObject]
    public partial struct Dim : IEquatable<Dim>
    {
        [Key(0)] public int NominalDiameter { get; set; }
        [Key(1)] public double OuterDiameter { get; set; }
        [Key(2)] public double InnerDiameter_mm { get; set; }
        [Key(3)] public double InnerDiameter_m { get; set; }
        [Key(4)] public double WallThickness { get; set; }
        [Key(5)] public double CrossSectionArea { get; set; }
        [Key(6)] public double Roughness_m { get; set; }
        [Key(7)] public string DimName { get; set; }
        [Key(8)] public PipeType PipeType { get; set; }
        [Key(9)] public int OrderingPriority { get; set; }
        [Key(10)] public int[] RGB { get; set; }
        [Key(11)] public double Price_m { get; set; }
        [Key(12)] public double Price_stk { get; set; }
        [IgnoreMember] public double RelativeRoughness { get => InnerDiameter_m == 0 ? 0 : Roughness_m / InnerDiameter_m; }
        public double Price_stk_calc(SegmentType st) => st == SegmentType.Stikledning ? Price_stk : 0;

        public Dim(
            int nominalDiameter,
            double outerDiameter,
            double innerDiameter,
            double wallThickness,
            double crossSectionArea,
            double roughness_m,
            string dimName,
            PipeType pipeType,
            int orderingPriority,
            int[] RGB,
            double price_m,
            double price_stk)
        {
            NominalDiameter = nominalDiameter;
            OuterDiameter = outerDiameter;
            InnerDiameter_mm = innerDiameter;
            InnerDiameter_m = innerDiameter / 1000;
            WallThickness = wallThickness;
            CrossSectionArea = crossSectionArea;
            Roughness_m = roughness_m;
            DimName = dimName + nominalDiameter.ToString("D3");
            PipeType = pipeType;
            OrderingPriority = orderingPriority;
            this.RGB = RGB;
            Price_m = price_m;
            Price_stk = price_stk;
        }
        public Dim()
        {
            DimName = "";
            RGB = Array.Empty<int>();
        }

        public static Dim NA => new Dim(0, 0, 0, 0, 0, 0, "NA ", PipeType.Stål, 0, new int[] { 0, 0, 0 }, 0, 0);
        public override string ToString()
        {
            return DimName;
        }

        public static bool operator ==(Dim left, Dim right)
        {
            return left.PipeType == right.PipeType && left.NominalDiameter == right.NominalDiameter;
        }

        public static bool operator !=(Dim left, Dim right)
        {
            return !(left == right);
        }

        public bool Equals(Dim other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + NominalDiameter.GetHashCode();
                hash = hash * 23 + PipeType.GetHashCode();
                return hash;
            }
        }
    }
}
