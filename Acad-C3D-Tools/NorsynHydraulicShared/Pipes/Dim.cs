using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public struct Dim : IEquatable<Dim>
    {
        public int NominalDiameter;
        public double OuterDiameter;
        public double InnerDiameter_mm;
        public double InnerDiameter_m;
        public double WallThickness;
        public double CrossSectionArea;
        public double Roughness_m;
        public string DimName;
        public PipeType PipeType;
        public int OrderingPriority;
        public int[] RGB;
        public double Price_m;
        private double price_stk;
        public double RelativeRoughness { get => Roughness_m / InnerDiameter_m; }
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
        public static Dim NA => new Dim(0, 0, 0, 0, 0, 0, "NA ", PipeType.Stål, 0, [0, 0, 0], 0, 0);
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
