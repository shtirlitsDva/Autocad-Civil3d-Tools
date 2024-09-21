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
        public double WallThickness;
        public double CrossSectionArea;
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
            WallThickness = wallThickness;
            CrossSectionArea = crossSectionArea;
            DimName = dimName + nominalDiameter;
            PipeType = pipeType;
        }
    }
}
