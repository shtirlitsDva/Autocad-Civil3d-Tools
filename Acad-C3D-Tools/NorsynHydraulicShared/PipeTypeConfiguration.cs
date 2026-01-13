using System;
using System.Collections.Generic;
using System.Linq;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Container for pipe type priorities for a segment type (FL or SL).
    /// Plain POCO class for serialization and use across projects.
    /// </summary>
    public class PipeTypeConfiguration
    {
        /// <summary>
        /// The segment type this configuration applies to (FL or SL).
        /// </summary>
        public SegmentType SegmentType { get; set; }

        /// <summary>
        /// Ordered list of pipe type priorities. Lower priority number = higher priority (used first).
        /// </summary>
        public List<PipeTypePriority> Priorities { get; set; } = new List<PipeTypePriority>();

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public PipeTypeConfiguration() { }

        /// <summary>
        /// Creates a new configuration for the specified segment type.
        /// </summary>
        public PipeTypeConfiguration(SegmentType segmentType)
        {
            SegmentType = segmentType;
            Priorities = new List<PipeTypePriority>();
        }

        /// <summary>
        /// Gets all pipe types configured in this configuration.
        /// </summary>
        public IEnumerable<PipeType> GetConfiguredPipeTypes()
        {
            return Priorities.Select(p => p.PipeType).Distinct();
        }

        /// <summary>
        /// Gets the priority entry for a specific pipe type, or null if not configured.
        /// </summary>
        public PipeTypePriority GetPriorityForPipeType(PipeType pipeType)
        {
            return Priorities.FirstOrDefault(p => p.PipeType == pipeType);
        }

        /// <summary>
        /// Renumbers priorities to be sequential starting from 1.
        /// </summary>
        public void RenumberPriorities()
        {
            int index = 1;
            foreach (var priority in Priorities.OrderBy(p => p.Priority))
            {
                priority.Priority = index++;
            }
        }

        /// <summary>
        /// Validates that all configured pipe types are valid for the given medium.
        /// </summary>
        public bool ValidateForMedium(MediumTypeEnum medium, out List<string> errors)
        {
            errors = new List<string>();

            foreach (var priority in Priorities)
            {
                if (!MediumPipeTypeRules.IsValidPipeType(medium, SegmentType, priority.PipeType))
                {
                    errors.Add($"{priority.PipeType} is not valid for {medium} {SegmentType}");
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Creates a deep copy of this configuration.
        /// </summary>
        public PipeTypeConfiguration Clone()
        {
            var clone = new PipeTypeConfiguration(SegmentType);
            foreach (var priority in Priorities)
            {
                clone.Priorities.Add(priority.Clone());
            }
            return clone;
        }

        public override string ToString() => $"{SegmentType}: {Priorities.Count} pipe types";
    }
}
