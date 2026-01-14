using System;
using System.Collections.Generic;
using System.Linq;

using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Factory for creating default PipeTypeConfiguration based on medium type.
    /// Provides programmatic creation of configurations for use without UI.
    /// </summary>
    public static class DefaultPipeConfigFactory
    {
        #region Default Values
        // Default velocity values in m/s
        private const double DefaultVelocitySmall = 1.5;      // DN 20-150
        private const double DefaultVelocityMedium = 2.5;     // DN 200-300
        private const double DefaultVelocityLarge = 3.0;      // DN 350+
        private const double DefaultVelocityFlexible = 1.0;   // Flexible pipes (SL)
        private const double DefaultVelocitySLSteel = 1.5;    // Steel service lines

        // Default pressure gradient values in Pa/m
        private const int DefaultGradientFL = 100;            // Fordelingsledninger
        private const int DefaultGradientFLLarge = 120;       // DN 350+ FL
        private const int DefaultGradientSL = 600;            // Stikledninger
        #endregion

        /// <summary>
        /// Creates a default FL (Fordelingsledninger) configuration for the specified medium.
        /// </summary>
        public static PipeTypeConfiguration CreateDefaultFL(MediumTypeEnum medium, PipeTypes pipeTypes)
        {
            var config = new PipeTypeConfiguration(SegmentType.Fordelingsledning);

            int priority = 1;

            // For Water medium: PertFlextra first (if supported), then Steel
            if (medium == MediumTypeEnum.Water)
            {

                // PertFlextra: DN 50-75 (typical FL range)
                var pertFlextra = CreatePipeTypePriority(
                    priority++,
                    PipeType.PertFlextra,
                    50, 75,
                    pipeTypes,
                    DefaultVelocitySmall,
                    DefaultGradientFL);
                config.Priorities.Add(pertFlextra);

                // Steel: DN 65-600 (overlaps with PertFlextra for transition)
                var steel = CreatePipeTypePriority(
                    priority++,
                    PipeType.Stål,
                    65, 600,
                    pipeTypes,
                    GetSteelVelocityFL,
                    GetSteelGradientFL);
                config.Priorities.Add(steel);
            }
            else if (medium == MediumTypeEnum.Water72Ipa28)
            {
                // PE only for this medium
                var pe = CreatePipeTypePriority(
                    priority++,
                    PipeType.Pe,
                    32, 250,
                    pipeTypes,
                    DefaultVelocitySmall,
                    DefaultGradientFL);
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
                var aluPex = CreatePipeTypePriority(
                    priority++,
                    PipeType.AluPEX,
                    26, 32,
                    pipeTypes,
                    DefaultVelocityFlexible,
                    DefaultGradientSL);
                config.Priorities.Add(aluPex);

                // Steel for larger SL
                var steel = CreatePipeTypePriority(
                    priority++,
                    PipeType.Stål,
                    32, 150,
                    pipeTypes,
                    DefaultVelocitySLSteel,
                    DefaultGradientSL);
                config.Priorities.Add(steel);
            }
            else if (medium == MediumTypeEnum.Water72Ipa28)
            {
                // PE only for this medium
                var pe = CreatePipeTypePriority(
                    priority++,
                    PipeType.Pe,
                    32, 75,
                    pipeTypes,
                    DefaultVelocityFlexible,
                    DefaultGradientSL);
                config.Priorities.Add(pe);
            }

            return config;
        }

        #region Pipe Type Priority Creation
        /// <summary>
        /// Creates a PipeTypePriority with uniform velocity and gradient for all DN values.
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

        #region DN-based Default Values
        /// <summary>
        /// Gets the default velocity for steel pipes in FL based on DN.
        /// </summary>
        public static double GetSteelVelocityFL(int dn)
        {
            if (dn <= 150) return DefaultVelocitySmall;
            if (dn <= 300) return DefaultVelocityMedium;
            return DefaultVelocityLarge;
        }

        /// <summary>
        /// Gets the default pressure gradient for steel pipes in FL based on DN.
        /// </summary>
        public static int GetSteelGradientFL(int dn)
        {
            if (dn <= 300) return DefaultGradientFL;
            return DefaultGradientFLLarge;
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
