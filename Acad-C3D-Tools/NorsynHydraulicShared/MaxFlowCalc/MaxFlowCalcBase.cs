using System;
using System.Collections.Generic;
using System.Linq;
using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc.MaxFlowCalc
{
    /// <summary>
    /// Calculates max flow tables using priority-based pipe type configuration.
    /// The table is built in priority order: all DN sizes from priority 1, then priority 2, etc.
    /// Within each priority, DNs are ordered smallest to largest.
    /// </summary>
    internal class MaxFlowCalcBase : IMaxFlowCalc
    {
        protected IHydraulicSettings _settings;
        protected PipeTypes _pipeTypes;

        public MaxFlowCalcBase(IHydraulicSettings settings)
        {
            _settings = settings;
            _pipeTypes = new PipeTypes(settings);
        }

        /// <summary>
        /// Calculates the max flow table for supply lines (Fordelingsledninger).
        /// Table is built in priority order: priority 1 (MinDn to MaxDn), then priority 2, etc.
        /// </summary>
        public void CalculateMaxFlowTableFL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow)
        {
            CalculateMaxFlowTable(table, calculateMaxFlow, _settings.PipeConfigFL, SegmentType.Fordelingsledning);
        }

        /// <summary>
        /// Calculates the max flow table for service lines (Stikledninger).
        /// Table is built in priority order: priority 1 (MinDn to MaxDn), then priority 2, etc.
        /// </summary>
        public void CalculateMaxFlowTableSL(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow)
        {
            CalculateMaxFlowTable(table, calculateMaxFlow, _settings.PipeConfigSL, SegmentType.Stikledning);
        }

        /// <summary>
        /// Generic method to calculate max flow table using pipe type configuration.
        /// </summary>
        private void CalculateMaxFlowTable(
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table,
            Func<Dim, TempSetType, SegmentType, double> calculateMaxFlow,
            PipeTypeConfiguration config,
            SegmentType segmentType)
        {
            if (config == null || config.Priorities.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No pipe type configuration for {segmentType}. " +
                    "Ensure PipeConfigFL/PipeConfigSL is properly initialized.");
            }

            // Process priorities in order (lowest priority number first = highest priority)
            foreach (var priority in config.Priorities.OrderBy(p => p.Priority))
            {
                // Get pipe instance to retrieve dimension data
                PipeBase pipe = GetPipeForType(priority.PipeType);

                // Get dimensions within the configured range, ordered by DN (smallest first)
                var dims = pipe.GetDimsRange(priority.MinDn, priority.MaxDn);

                foreach (var dim in dims)
                {
                    table.Add((dim,
                        calculateMaxFlow(dim, TempSetType.Supply, segmentType),
                        calculateMaxFlow(dim, TempSetType.Return, segmentType)));
                }
            }
        }

        /// <summary>
        /// Gets the pipe instance for a given pipe type.
        /// </summary>
        private PipeBase GetPipeForType(PipeType pipeType)
        {
            return pipeType switch
            {
                PipeType.Stål => _pipeTypes.Stål,
                PipeType.PertFlextra => _pipeTypes.PertFlextra,
                PipeType.AluPEX => _pipeTypes.AluPex,
                PipeType.Kobber => _pipeTypes.Cu,
                PipeType.Pe => _pipeTypes.Pe,
                PipeType.AquaTherm11 => _pipeTypes.AT11,
                _ => throw new NotSupportedException($"Unknown pipe type: {pipeType}")
            };
        }
    }
}
