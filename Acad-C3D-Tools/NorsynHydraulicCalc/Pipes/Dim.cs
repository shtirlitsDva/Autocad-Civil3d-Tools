using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    internal struct Dim
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

        public Dim(
            int nominalDiameter,
            double outerDiameter,
            double innerDiameter,
            double wallThickness,
            double crossSectionArea,
            double roughness_m,
            string dimName,
            PipeType pipeType)
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
        }
    }
}
