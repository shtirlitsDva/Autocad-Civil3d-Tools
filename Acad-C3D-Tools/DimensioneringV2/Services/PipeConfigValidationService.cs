using NorsynHydraulicCalc;

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DimensioneringV2.Services
{
    /// <summary>
    /// Result of validating a pipe type configuration.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();

        public static ValidationResult Success() => new() { IsValid = true };

        public static ValidationResult Failed(params string[] errors) => new()
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }

    /// <summary>
    /// Service for validating PipeTypeConfiguration objects.
    /// </summary>
    public class PipeConfigValidationService
    {
        /// <summary>
        /// Validates a pipe type configuration.
        /// </summary>
        /// <param name="config">The configuration to validate.</param>
        /// <returns>Validation result with errors and warnings.</returns>
        public ValidationResult Validate(PipeTypeConfiguration config)
        {
            var result = new ValidationResult { IsValid = true };

            if (config == null)
            {
                return ValidationResult.Failed("Konfiguration er null.");
            }

            if (config.Priorities.Count == 0)
            {
                result.Errors.Add("Mindst én rørtype skal være konfigureret.");
                result.IsValid = false;
                return result;
            }

            // Check for uninitialized dimensions
            var uninitializedDns = GetUninitializedDimensions(config);
            if (uninitializedDns.Any())
            {
                var sb = new StringBuilder();
                sb.AppendLine("Følgende DN størrelser har ikke initialiserede acceptkriterier:");
                foreach (var (pipeType, dn) in uninitializedDns)
                {
                    sb.AppendLine($"  - {pipeType}: DN {dn}");
                }
                result.Warnings.Add(sb.ToString());
            }

            // Check for overlapping DN ranges
            if (HasOverlappingDnRanges(config))
            {
                result.Warnings.Add("Der er overlappende DN-områder mellem rørtyper. " +
                    "Den højeste prioritet vil blive brugt for overlappende størrelser.");
            }

            // Check for gaps in DN coverage
            var gaps = FindDnCoverageGaps(config);
            if (gaps.Any())
            {
                var gapText = string.Join(", ", gaps.Select(g => $"DN {g.Start}-{g.End}"));
                result.Warnings.Add($"Der er huller i DN-dækningen: {gapText}");
            }

            // Check that MinDn <= MaxDn for all priorities
            foreach (var priority in config.Priorities)
            {
                if (priority.MinDn > priority.MaxDn)
                {
                    result.Errors.Add($"{priority.PipeType}: Min DN ({priority.MinDn}) er større end Max DN ({priority.MaxDn}).");
                    result.IsValid = false;
                }
            }

            return result;
        }

        /// <summary>
        /// Gets all dimensions where IsInitialized is false within the configured DN range.
        /// </summary>
        public List<(PipeType PipeType, int Dn)> GetUninitializedDimensions(PipeTypeConfiguration config)
        {
            var result = new List<(PipeType, int)>();

            foreach (var priority in config.Priorities)
            {
                foreach (var criteria in priority.AcceptCriteria)
                {
                    if (!criteria.IsInitialized &&
                        criteria.NominalDiameter >= priority.MinDn &&
                        criteria.NominalDiameter <= priority.MaxDn)
                    {
                        result.Add((priority.PipeType, criteria.NominalDiameter));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if there are overlapping DN ranges between different priorities.
        /// Overlapping is allowed (higher priority wins) but user should be warned.
        /// </summary>
        public bool HasOverlappingDnRanges(PipeTypeConfiguration config)
        {
            var priorities = config.Priorities.ToList();

            for (int i = 0; i < priorities.Count; i++)
            {
                for (int j = i + 1; j < priorities.Count; j++)
                {
                    var a = priorities[i];
                    var b = priorities[j];

                    // Check if ranges overlap
                    if (a.MinDn <= b.MaxDn && b.MinDn <= a.MaxDn)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Finds gaps in DN coverage across all priorities.
        /// </summary>
        public List<(int Start, int End)> FindDnCoverageGaps(PipeTypeConfiguration config)
        {
            if (!config.Priorities.Any())
                return new List<(int, int)>();

            // Get all covered DN values
            var coveredDns = new HashSet<int>();
            foreach (var priority in config.Priorities)
            {
                foreach (var criteria in priority.AcceptCriteria)
                {
                    if (criteria.NominalDiameter >= priority.MinDn &&
                        criteria.NominalDiameter <= priority.MaxDn)
                    {
                        coveredDns.Add(criteria.NominalDiameter);
                    }
                }
            }

            if (!coveredDns.Any())
                return new List<(int, int)>();

            // This is a simplified gap detection - in practice, gaps between
            // standard DN sizes might not be meaningful
            // For now, we don't report gaps as the DN values are discrete
            return new List<(int, int)>();
        }

        /// <summary>
        /// Validates that a configuration is ready for calculation.
        /// This is stricter than the general Validate method.
        /// </summary>
        public ValidationResult ValidateForCalculation(PipeTypeConfiguration config)
        {
            var result = Validate(config);

            // For calculation, uninitialized dimensions are errors, not warnings
            var uninitializedDns = GetUninitializedDimensions(config);
            if (uninitializedDns.Any())
            {
                result.IsValid = false;
                var errorText = new StringBuilder();
                errorText.AppendLine("Beregning kan ikke startes - følgende DN størrelser mangler initialisering:");
                foreach (var (pipeType, dn) in uninitializedDns)
                {
                    errorText.AppendLine($"  - {pipeType}: DN {dn}");
                }
                result.Errors.Add(errorText.ToString());
            }

            return result;
        }

        /// <summary>
        /// Generates a human-readable summary of the configuration.
        /// </summary>
        public string GetConfigurationSummary(PipeTypeConfiguration config)
        {
            if (config == null || !config.Priorities.Any())
                return "Ingen rørtyper konfigureret.";

            var sb = new StringBuilder();
            sb.AppendLine($"Segment: {(config.SegmentType == SegmentType.Fordelingsledning ? "Fordelingsledninger" : "Stikledninger")}");
            sb.AppendLine($"Antal rørtyper: {config.Priorities.Count}");
            sb.AppendLine();

            foreach (var priority in config.Priorities.OrderBy(p => p.Priority))
            {
                sb.AppendLine($"  {priority.Priority}. {priority.PipeType}: DN {priority.MinDn}-{priority.MaxDn}");
            }

            return sb.ToString();
        }
    }
}
