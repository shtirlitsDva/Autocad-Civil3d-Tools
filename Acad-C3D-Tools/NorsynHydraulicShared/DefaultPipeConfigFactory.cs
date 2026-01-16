using System;
using System.Collections.Generic;
using System.Linq;

using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Factory for creating default PipeTypeConfiguration based on medium type.
    /// Default accept criteria values come from each pipe type's GetDefaultAcceptCriteria method.
    /// </summary>
    public static class DefaultPipeConfigFactory
    {
        /// <summary>
        /// Creates a default FL (Fordelingsledninger) configuration for the specified medium.
        /// </summary>
        public static PipeTypeConfiguration CreateDefaultFL(MediumTypeEnum medium, PipeTypes pipeTypes)
        {
            var config = new PipeTypeConfiguration(SegmentType.Fordelingsledning);

            int priority = 1;

            // For Water medium: PertFlextraFL first, then Steel
            if (medium == MediumTypeEnum.Water)
            {
                // PertFlextra: DN 50-75 (typical FL range)
                var pertFlextra = CreatePipeTypePriorityWithDefaults(
                    priority++,
                    PipeType.PertFlextraFL,
                    50, 75,
                    SegmentType.Fordelingsledning,
                    pipeTypes);
                config.Priorities.Add(pertFlextra);

                // Steel: DN 65-600 (overlaps with PertFlextra for transition)
                var steel = CreatePipeTypePriorityWithDefaults(
                    priority++,
                    PipeType.Stål,
                    65, 600,
                    SegmentType.Fordelingsledning,
                    pipeTypes);
                config.Priorities.Add(steel);
            }
            else if (medium == MediumTypeEnum.Water72Ipa28)
            {
                // PE only for this medium
                var pe = CreatePipeTypePriorityWithDefaults(
                    priority++,
                    PipeType.Pe,
                    32, 250,
                    SegmentType.Fordelingsledning,
                    pipeTypes);
                config.Priorities.Add(pe);
            }

            return config;
        }

        /// <summary>
        /// Creates a default SL (Stikledninger) configuration for the specified medium.
        /// </summary>
        public static PipeTypeConfiguration CreateDefaultSL(MediumTypeEnum medium, PipeTypes pipeTypes)
        {
            var config = new PipeTypeConfiguration(SegmentType.Stikledning);

            int priority = 1;

            if (medium == MediumTypeEnum.Water)
            {
                // AluPEX: DN 26-32 (highest priority for SL)
                var aluPex = CreatePipeTypePriorityWithDefaults(
                    priority++,
                    PipeType.AluPEX,
                    26, 32,
                    SegmentType.Stikledning,
                    pipeTypes);
                config.Priorities.Add(aluPex);

                // Steel for larger SL
                var steel = CreatePipeTypePriorityWithDefaults(
                    priority++,
                    PipeType.Stål,
                    32, 150,
                    SegmentType.Stikledning,
                    pipeTypes);
                config.Priorities.Add(steel);
            }
            else if (medium == MediumTypeEnum.Water72Ipa28)
            {
                // PE only for this medium
                var pe = CreatePipeTypePriorityWithDefaults(
                    priority++,
                    PipeType.Pe,
                    32, 75,
                    SegmentType.Stikledning,
                    pipeTypes);
                config.Priorities.Add(pe);
            }

            return config;
        }

        #region Pipe Type Priority Creation
        /// <summary>
        /// Creates a PipeTypePriority using default accept criteria from the pipe type.
        /// </summary>
        public static PipeTypePriority CreatePipeTypePriorityWithDefaults(
            int priority,
            PipeType pipeType,
            int minDn,
            int maxDn,
            SegmentType segmentType,
            PipeTypes pipeTypes)
        {
            var ptp = new PipeTypePriority(priority, pipeType, minDn, maxDn);

            var pipe = pipeTypes.GetPipeType(pipeType) as PipeBase;
            if (pipe != null)
            {
                // Get default accept criteria from pipe type (uses segment type for FL/SL specific defaults)
                ptp.AcceptCriteria = pipe.GetAllDefaultAcceptCriteria(segmentType);
            }
            else
            {
                // Fallback with generic defaults (shouldn't happen)
                var availableDnValues = pipeTypes.GetAvailableDnValues(pipeType);
                foreach (var dn in availableDnValues)
                {
                    ptp.AcceptCriteria.Add(new DnAcceptCriteria(dn, 2.0, 100, true));
                }
            }

            return ptp;
        }

        /// <summary>
        /// Creates a PipeTypePriority with explicit velocity and gradient values.
        /// Use this when you need to override the pipe type defaults.
        /// </summary>
        public static PipeTypePriority CreatePipeTypePriority(
            int priority,
            PipeType pipeType,
            int minDn,
            int maxDn,
            PipeTypes pipeTypes,
            double velocity,
            int gradient)
        {
            return CreatePipeTypePriority(priority, pipeType, minDn, maxDn, pipeTypes,
                dn => velocity, dn => gradient);
        }

        /// <summary>
        /// Creates a PipeTypePriority with variable velocity and gradient based on DN.
        /// Use this when you need DN-specific values that differ from pipe type defaults.
        /// </summary>
        public static PipeTypePriority CreatePipeTypePriority(
            int priority,
            PipeType pipeType,
            int minDn,
            int maxDn,
            PipeTypes pipeTypes,
            Func<int, double> velocityFunc,
            Func<int, int> gradientFunc)
        {
            var ptp = new PipeTypePriority(priority, pipeType, minDn, maxDn);

            // Get DN values from the already-loaded pipe type
            var availableDnValues = pipeTypes.GetAvailableDnValues(pipeType);

            // Create accept criteria for all available DN values for this pipe type
            foreach (var dn in availableDnValues)
            {
                var criteria = new DnAcceptCriteria(dn, velocityFunc(dn), gradientFunc(dn));
                ptp.AcceptCriteria.Add(criteria);
            }

            return ptp;
        }
        #endregion

        #region Available DN Values
        /// <summary>
        /// Gets the available DN values for a specific pipe type.
        /// </summary>
        public static int[] GetAvailableDnValues(PipeType pipeType, PipeTypes pipeTypes)
        {
            return pipeTypes.GetAvailableDnValues(pipeType);
        }

        /// <summary>
        /// Gets the minimum available DN for a pipe type.
        /// </summary>
        public static int GetMinDn(PipeType pipeType, PipeTypes pipeTypes)
        {
            var values = GetAvailableDnValues(pipeType, pipeTypes);
            return values.Length > 0 ? values.Min() : 0;
        }

        /// <summary>
        /// Gets the maximum available DN for a pipe type.
        /// </summary>
        public static int GetMaxDn(PipeType pipeType, PipeTypes pipeTypes)
        {
            var values = GetAvailableDnValues(pipeType, pipeTypes);
            return values.Length > 0 ? values.Max() : 0;
        }
        #endregion
    }
}
