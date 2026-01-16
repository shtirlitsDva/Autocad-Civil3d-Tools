using System;
using System.Collections.Generic;
using System.Linq;

using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Defines which pipe types are valid for each medium type.
    /// Queries pipe metadata dynamically from PipeTypes instead of hardcoded lists.
    /// </summary>
    public static class MediumPipeTypeRules
    {
        // Cache to avoid repeated instantiation
        private static PipeTypes? _pipeTypesCache;

        private static PipeTypes GetPipeTypes()
        {
            return _pipeTypesCache ??= new PipeTypes(new DefaultHydraulicSettings());
        }

        /// <summary>
        /// Gets the valid pipe types for supply lines (Fordelingsledninger) for the given medium.
        /// Queries pipe metadata dynamically - pipe types declare their supported segments and mediums.
        /// </summary>
        public static IEnumerable<PipeType> GetValidPipeTypesForSupply(MediumTypeEnum medium)
        {
            return GetPipeTypes().GetPipeTypesFor(SegmentType.Fordelingsledning, medium);
        }

        /// <summary>
        /// Gets the valid pipe types for service lines (Stikledninger) for the given medium.
        /// Queries pipe metadata dynamically - pipe types declare their supported segments and mediums.
        /// </summary>
        public static IEnumerable<PipeType> GetValidPipeTypesForService(MediumTypeEnum medium)
        {
            return GetPipeTypes().GetPipeTypesFor(SegmentType.Stikledning, medium);
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
    }
}
