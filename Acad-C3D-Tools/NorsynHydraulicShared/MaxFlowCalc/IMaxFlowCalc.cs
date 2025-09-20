using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.MaxFlowCalc
{
    interface IMaxFlowCalc
    {
        void CalculateMaxFlowTableFL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow);
        void CalculateMaxFlowTableSL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow);
    }
}