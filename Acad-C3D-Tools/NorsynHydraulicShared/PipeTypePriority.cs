using System;
using System.Collections.Generic;
using System.Linq;

using NorsynHydraulicCalc.Rules;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// A prioritized pipe type configuration with DN range and accept criteria.
    /// Plain POCO class for serialization and use across projects.
    /// </summary>
    public class PipeTypePriority
    {
        /// <summary>
        /// Priority order (1 = highest priority, used first when selecting pipes).
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// The pipe type (e.g., Stål, PertFlextra, AluPEX, etc.)
        /// </summary>
        public PipeType PipeType { get; set; }

        /// <summary>
        /// Minimum nominal diameter for this pipe type configuration.
        /// </summary>
        public int MinDn { get; set; }

        /// <summary>
        /// Maximum nominal diameter for this pipe type configuration.
        /// </summary>
        public int MaxDn { get; set; }

        /// <summary>
        /// Accept criteria for each DN size.
        /// </summary>
        public List<DnAcceptCriteria> AcceptCriteria { get; set; } = new List<DnAcceptCriteria>();

        /// <summary>
        /// Rules that must be satisfied for this priority to be used (SL only).
        /// Empty list means no rules - priority executes in sequence.
        /// Multiple rules are evaluated with OR logic (any match = priority applies).
        /// </summary>
        public List<IPipeRule> Rules { get; set; } = new List<IPipeRule>();

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public PipeTypePriority() { }

        /// <summary>
        /// Creates a new pipe type priority instance.
        /// </summary>
        public PipeTypePriority(int priority, PipeType pipeType, int minDn, int maxDn)
        {
            Priority = priority;
            PipeType = pipeType;
            MinDn = minDn;
            MaxDn = maxDn;
            AcceptCriteria = new List<DnAcceptCriteria>();
        }

        /// <summary>
        /// Gets the accept criteria for a specific DN, or null if not found.
        /// </summary>
        public DnAcceptCriteria GetCriteriaForDn(int dn)
        {
            return AcceptCriteria.FirstOrDefault(c => c.NominalDiameter == dn);
        }

        /// <summary>
        /// Gets all DN sizes within the configured Min-Max range that have criteria.
        /// </summary>
        public IEnumerable<DnAcceptCriteria> GetCriteriaInRange()
        {
            return AcceptCriteria
                .Where(c => c.NominalDiameter >= MinDn && c.NominalDiameter <= MaxDn)
                .OrderBy(c => c.NominalDiameter);
        }

        /// <summary>
        /// Creates a deep copy of this instance.
        /// </summary>
        public PipeTypePriority Clone()
        {
            var clone = new PipeTypePriority(Priority, PipeType, MinDn, MaxDn);
            foreach (var criteria in AcceptCriteria)
            {
                clone.AcceptCriteria.Add(criteria.Clone());
            }
            foreach (var rule in Rules)
            {
                clone.Rules.Add(rule.Clone());
            }
            return clone;
        }

        public override string ToString() => $"Pri {Priority}: {PipeType} DN{MinDn}-{MaxDn}";
    }
}
