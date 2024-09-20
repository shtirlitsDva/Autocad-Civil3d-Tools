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

        public Dim(int nominalDiameter, double outerDiameter, double innerDiameter, double wallThickness, double crossSectionArea)
        {
            NominalDiameter = nominalDiameter;
            OuterDiameter = outerDiameter;
            InnerDiameter = innerDiameter;
            WallThickness = wallThickness;
            CrossSectionArea = crossSectionArea;
        }
    }
}
