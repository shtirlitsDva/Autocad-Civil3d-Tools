using System;
using System.Collections.Generic;
using System.Linq;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Defines which pipe types are valid for each medium type.
    /// This is the authoritative source for pipe type restrictions.
    /// </summary>
    public static class MediumPipeTypeRules
    {
        /// <summary>
        /// Gets the valid pipe types for supply lines (Fordelingsledninger) for the given medium.
        /// Water medium: All pipe types EXCEPT Pe
        /// Water72Ipa28 medium: ONLY Pe
        /// </summary>
        public static IEnumerable<PipeType> GetValidPipeTypesForSupply(MediumTypeEnum medium)
        {
            return medium switch
            {
                // Water can use all pipe types except PE
                MediumTypeEnum.Water => new[] { PipeType.Stål, PipeType.PertFlextra, PipeType.AluPEX, PipeType.Kobber, PipeType.AquaTherm11 },
                // Water72Ipa28 can ONLY use PE
                MediumTypeEnum.Water72Ipa28 => new[] { PipeType.Pe },
                _ => throw new NotSupportedException($"Unknown medium: {medium}")
            };
        }

        /// <summary>
        /// Gets the valid pipe types for service lines (Stikledninger) for the given medium.
        /// Water medium: All pipe types EXCEPT Pe
        /// Water72Ipa28 medium: ONLY Pe
        /// </summary>
        public static IEnumerable<PipeType> GetValidPipeTypesForService(MediumTypeEnum medium)
        {
            return medium switch
            {
                // Water can use all pipe types except PE
                MediumTypeEnum.Water => new[] { PipeType.AluPEX, PipeType.PertFlextra, PipeType.Kobber, PipeType.Stål, PipeType.AquaTherm11 },
                // Water72Ipa28 can ONLY use PE
                MediumTypeEnum.Water72Ipa28 => new[] { PipeType.Pe },
                _ => throw new NotSupportedException($"Unknown medium: {medium}")
            };
        }

        /// <summary>
        /// Checks if a pipe type is valid for the given medium and segment type.
        /// </summary>
        public static bool IsValidPipeType(MediumTypeEnum medium, SegmentType segmentType, PipeType pipeType)
        {
            var validTypes = segmentType == SegmentType.Fordelingsledning
                ? GetValidPipeTypesForSupply(medium)
                : GetValidPipeTypesForService(medium);

            return validTypes.Contains(pipeType);
        }

        /// <summary>
        /// Indicates whether PertFlextra pipes are supported for the given medium.
        /// </summary>
        public static bool SupportsPertFlextra(MediumTypeEnum medium)
        {
            return medium switch
            {
                MediumTypeEnum.Water => true,
                MediumTypeEnum.Water72Ipa28 => false,
                _ => false
            };
        }
    }
}
