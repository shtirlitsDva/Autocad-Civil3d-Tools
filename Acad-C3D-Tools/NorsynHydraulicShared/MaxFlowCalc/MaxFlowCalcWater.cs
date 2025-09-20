using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.MaxFlowCalc
{
    class MaxFlowCalcWater : MaxFlowCalcBase
    {
        public MaxFlowCalcWater(IHydraulicSettings settings) : base(settings) {}
        private const int steelMinDnBase = 32;
        public override void CalculateMaxFlowTableFL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow)
        {
            var steelMinDn = steelMinDnBase;

            if (_settings.UsePertFlextraFL)
            {
                foreach (var dim in _pipeTypes.PertFlextra.GetDimsRange(
                    50, _settings.PertFlextraMaxDnFL))
                {
                    table.Add((dim,
                        calculateMaxFlow(dim, TempSetType.Supply, SegmentType.Fordelingsledning),
                        calculateMaxFlow(dim, TempSetType.Return, SegmentType.Fordelingsledning)));
                }

                steelMinDn = translationBetweenMaxPertAndMinStål[_settings.PertFlextraMaxDnFL];
            }

            foreach (var dim in _pipeTypes.Stål.GetDimsRange(steelMinDn, 1000))
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
            switch (_settings.PipeTypeSL)
            {
                case PipeType.Stål:
                    throw new Exception("Stål-stikledninger er ikke tilladt!");
                case PipeType.PertFlextra:
                    foreach (var dim in _pipeTypes.PertFlextra.GetDimsRange(25, 75))
                    {
                        table.Add((dim,
                            calculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                            calculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                    }
                    break;
                case PipeType.AluPEX:
                    foreach (var dim in _pipeTypes.AluPex.GetDimsRange(26, 32))
                    {
                        table.Add((dim,
                            calculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                            calculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                    }
                    break;
                case PipeType.Kobber:
                    foreach (var dim in _pipeTypes.Cu.GetDimsRange(22, 28))
                    {
                        table.Add((dim,
                            calculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                            calculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                    }
                    break;
                default:
                    throw new NotImplementedException($"{_settings.PipeTypeSL} not Implemented!");
            }

            foreach (var dim in _pipeTypes.Stål.GetDimsRange(32, 1000))
            {
                table.Add((dim,
                    calculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                    calculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
            }
        }
    }
}