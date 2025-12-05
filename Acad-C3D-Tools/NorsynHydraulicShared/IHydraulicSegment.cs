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
        double KarFlowSupply { get; }
        double KarFlowReturn { get; }
        Dim Dim { get; }
        /// <summary>
        /// User has set a Dim. Dim must not be changed.
        /// </summary>
        bool ManualDim { get; }
        /// <summary>
        /// Client connections may have temperature delta specified for heating or 0.
        /// </summary>
        double TempDeltaVarme { get; }
        /// <summary>
        /// Client connections may have temperature delta specified for water heating or 0.
        /// </summary>
        double TempDeltaBV { get; }
    }
}
