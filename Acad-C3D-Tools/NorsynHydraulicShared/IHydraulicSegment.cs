using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicShared
{
    public interface IHydraulicSegment
    {
        SegmentType SegmentType { get; }
        double HeatingDemandSupplied { get; }
        int NumberOfBuildingsSupplied { get; }
        int NumberOfUnitsSupplied { get; }
        double Length { get; }
    }
}
