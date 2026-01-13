using System;
using System.Collections.Generic;
using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc.MaxFlowCalc
{
    /// <summary>
    /// Interface for max flow table calculation.
    /// </summary>
    internal interface IMaxFlowCalc
    {
        /// <summary>
        /// Calculates and populates the max flow table for supply lines (Fordelingsledninger).
        /// </summary>
        void CalculateMaxFlowTableFL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow);

        /// <summary>
        /// Calculates and populates the max flow table for service lines (Stikledninger).
        /// </summary>
        void CalculateMaxFlowTableSL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow);
    }
}
