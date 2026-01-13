using System;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Holds velocity and pressure gradient accept criteria for a single pipe dimension (DN).
    /// Plain POCO class for serialization and use across projects.
    /// </summary>
    public class DnAcceptCriteria
    {
        /// <summary>
        /// Nominal diameter (e.g., 50, 63, 75, 100, etc.)
        /// </summary>
        public int NominalDiameter { get; set; }

        /// <summary>
        /// Maximum allowed velocity in m/s.
        /// </summary>
        public double MaxVelocity { get; set; }

        /// <summary>
        /// Maximum allowed pressure gradient in Pa/m.
        /// </summary>
        public int MaxPressureGradient { get; set; }

        /// <summary>
        /// Indicates whether the user has explicitly reviewed/set this criteria.
        /// Used for UI validation to warn about unreviewed defaults.
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// Parameterless constructor for serialization.
        /// </summary>
        public DnAcceptCriteria() { }

        /// <summary>
        /// Creates a new accept criteria instance.
        /// </summary>
        public DnAcceptCriteria(int nominalDiameter, double maxVelocity, int maxPressureGradient, bool isInitialized = true)
        {
            NominalDiameter = nominalDiameter;
            MaxVelocity = maxVelocity;
            MaxPressureGradient = maxPressureGradient;
            IsInitialized = isInitialized;
        }

        /// <summary>
        /// Creates a copy of this instance.
        /// </summary>
        public DnAcceptCriteria Clone()
        {
            return new DnAcceptCriteria(NominalDiameter, MaxVelocity, MaxPressureGradient, IsInitialized);
        }

        public override string ToString() => $"DN{NominalDiameter}: v={MaxVelocity}m/s, dP={MaxPressureGradient}Pa/m";
    }
}
