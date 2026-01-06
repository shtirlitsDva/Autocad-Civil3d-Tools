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
        double HeatingDemandConnected { get; }
        int NumberOfBuildingsConnected { get; }
        int NumberOfUnitsConnected { get; }
        double HeatingDemandSupplied { get; }
        int NumberOfBuildingsSupplied { get; }
        int NumberOfUnitsSupplied { get; }
        double Length { get; }
        double KarFlowHeatSupply { get; }
        double KarFlowBVSupply { get; }
        double KarFlowHeatReturn { get; }
        double KarFlowBVReturn { get; }
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
        
        /// <summary>
        /// Building nyttetimer value for this segment.
        /// Pre-populated before calculation based on AnvendelsesKode lookup.
        /// Only used for Stikledning segments.
        /// </summary>
        int Bygningsnyttetimer { get; set; }
    }
}
