using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public struct Dim
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
        public double RelativeRoughness { get => Roughness_m / InnerDiameter_m; }

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
            int[] RGB)
        {
            NominalDiameter = nominalDiameter;
            OuterDiameter = outerDiameter;
            InnerDiameter_mm = innerDiameter;
            InnerDiameter_m = innerDiameter / 1000;
            WallThickness = wallThickness;
            CrossSectionArea = crossSectionArea;
            Roughness_m = roughness_m;
            DimName = dimName + nominalDiameter;
            PipeType = pipeType;
            OrderingPriority = orderingPriority;
            this.RGB = RGB;
        }
        public override string ToString()
        {
            return DimName;
        }
    }
}
