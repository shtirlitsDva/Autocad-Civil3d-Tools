using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicShared
{
    internal interface IHydraulicSegment
    {
        SegmentType SegmentType { get; }
        double HeatingDemandSupplied { get; }
        int NumberOfBuildingsSupplied { get; }
        int NumberOfUnitsSupplied { get; }
    }
}
