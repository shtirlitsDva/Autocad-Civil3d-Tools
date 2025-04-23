using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc.MaxFlowCalc
{
    internal abstract class MaxFlowCalcBase : IMaxFlowCalc
    {
        protected IHydraulicSettings _settings;
        protected PipeTypes _pipeTypes;
        protected static Dictionary<int, int> translationBetweenMaxPertAndMinStål =
            new Dictionary<int, int>()
            {
                //{ 75, 65 },
                //{ 63, 50 },
                //{ 50, 40 },
                //{ 40, 32 },
                //{ 32, 25 }

                { 75, 65 },
                { 63, 50 },
                { 50, 40 },
            };

        protected MaxFlowCalcBase(IHydraulicSettings settings)
        {
            _settings = settings;
            _pipeTypes = new PipeTypes(settings);
        }

        public abstract void CalculateMaxFlowTableFL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow);
        public abstract void CalculateMaxFlowTableSL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow);
    }
}