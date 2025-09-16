using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

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
        Dim Dim { get; }
        /// <summary>
        /// User has set a Dim. Dim must not be changed.
        /// </summary>
        bool ManualDim { get; }
    }
}
