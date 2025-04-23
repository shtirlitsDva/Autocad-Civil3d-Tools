using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.MaxFlowCalc
{
    class MaxFlowCalcWater72Ipa28 : MaxFlowCalcBase
    {
        public MaxFlowCalcWater72Ipa28(IHydraulicSettings settings) : base(settings) { }

        public override void CalculateMaxFlowTableFL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow)
        {
            foreach (var dim in _pipeTypes.Pe.GetAllDimsSorted())
            {
                table.Add((dim,
                    calculateMaxFlow(dim, TempSetType.Supply, SegmentType.Fordelingsledning),
                    calculateMaxFlow(dim, TempSetType.Return, SegmentType.Fordelingsledning)));
            }
        }

        public override void CalculateMaxFlowTableSL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow)
        {
            foreach (var dim in _pipeTypes.Pe.GetAllDimsSorted())
            {
                table.Add((dim,
                    calculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                    calculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
            }
        }
    }
}
