using System.Collections.Generic;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Accept criteria for a single pipe dimension.
    /// </summary>
    public interface IDnAcceptCriteria
    {
        /// <summary>
        /// Nominal diameter (e.g., 50, 63, 75, 100, etc.)
        /// </summary>
        int NominalDiameter { get; }

        /// <summary>
        /// Maximum allowed velocity in m/s.
        /// </summary>
        double MaxVelocity { get; }

        /// <summary>
        /// Maximum allowed pressure gradient in Pa/m.
        /// </summary>
        int MaxPressureGradient { get; }
    }

    /// <summary>
    /// A prioritized pipe type configuration with DN range and accept criteria.
    /// </summary>
    public interface IPipeTypePriority
    {
        /// <summary>
        /// Priority order (1 = highest priority, used first when selecting pipes).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// The pipe type (e.g., Stål, PertFlextra, AluPEX, etc.)
        /// </summary>
        PipeType PipeType { get; }

        /// <summary>
        /// Minimum nominal diameter for this pipe type configuration.
        /// </summary>
        int MinDn { get; }

        /// <summary>
        /// Maximum nominal diameter for this pipe type configuration.
        /// </summary>
        int MaxDn { get; }

        /// <summary>
        /// Accept criteria for each DN size.
        /// </summary>
        IEnumerable<IDnAcceptCriteria> AcceptCriteria { get; }

        /// <summary>
        /// Gets the accept criteria for a specific DN, or null if not found.
        /// </summary>
        IDnAcceptCriteria GetCriteriaForDn(int dn);
    }

    /// <summary>
    /// Container for pipe type priorities for a segment type (FL or SL).
    /// </summary>
    public interface IPipeTypeConfiguration
    {
        /// <summary>
        /// The segment type this configuration applies to (FL or SL).
        /// </summary>
        SegmentType SegmentType { get; }

        /// <summary>
        /// Ordered list of pipe type priorities. Lower priority number = higher priority (used first).
        /// </summary>
        IEnumerable<IPipeTypePriority> Priorities { get; }

        /// <summary>
        /// Gets the priority entry for a specific pipe type, or null if not configured.
        /// </summary>
        IPipeTypePriority GetPriorityForPipeType(PipeType pipeType);
    }
}
