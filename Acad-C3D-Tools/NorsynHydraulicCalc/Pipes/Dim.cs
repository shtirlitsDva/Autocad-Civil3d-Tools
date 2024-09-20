using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    internal struct Dim
    {
        public int NominalDiameter;
        public double OuterDiameter;
        public double InnerDiameter;
        public double WallThickness;
        public double CrossSectionArea;
        public string DimName;

        public Dim(
            int nominalDiameter,
            double outerDiameter,
            double innerDiameter,
            double wallThickness,
            double crossSectionArea,
            double roughness_m,
            string dimName)
        {
            NominalDiameter = nominalDiameter;
            OuterDiameter = outerDiameter;
            InnerDiameter = innerDiameter;
            WallThickness = wallThickness;
            CrossSectionArea = crossSectionArea;
            DimName = dimName + nominalDiameter;
        }
    }
}
