using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public struct Dim : IEquatable<Dim>
    {
        public int NominalDiameter { get; set; }
        public double OuterDiameter { get; set; }
        public double InnerDiameter_mm { get; set; }
        public double InnerDiameter_m { get; set; }
        public double WallThickness { get; set; }
        public double CrossSectionArea { get; set; }
        public double Roughness_m { get; set; }
        public string DimName { get; set; }
        public PipeType PipeType { get; set; }
        public int OrderingPriority { get; set; }
        public int[] RGB { get; set; }
        public double Price_m { get; set; }
        private double price_stk { get; set; }
        public double RelativeRoughness { get => InnerDiameter_m == 0 ? 0 : Roughness_m / InnerDiameter_m; }
        public double Price_stk(SegmentType st) => st == SegmentType.Stikledning ? price_stk : 0;

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
            this.price_stk = price_stk;
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
