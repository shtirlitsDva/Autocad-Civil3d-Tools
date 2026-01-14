using System;
using System.Text.Json.Serialization;

using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Holds velocity and pressure gradient accept criteria for a single pipe dimension (DN),
    /// along with calculated max flow values computed during hydraulic calculation initialization.
    /// Plain POCO class for serialization and use across projects.
    /// </summary>
    public class DnAcceptCriteria
    {
        #region Configuration Properties (Serialized)
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
        #endregion

        #region Calculated Properties (Runtime Only - Not Serialized)
        /// <summary>
        /// Maximum flow rate for supply pipe in m³/hr.
        /// Calculated during HydraulicCalc initialization based on criteria.
        /// </summary>
        [JsonIgnore]
        public double? MaxFlowSupply { get; set; }

        /// <summary>
        /// Maximum flow rate for return pipe in m³/hr.
        /// Calculated during HydraulicCalc initialization based on criteria.
        /// </summary>
        [JsonIgnore]
        public double? MaxFlowReturn { get; set; }

        /// <summary>
        /// Reference to the pipe dimension data (inner diameter, roughness, etc.).
        /// Set during HydraulicCalc initialization.
        /// </summary>
        [JsonIgnore]
        public Dim? Dim { get; set; }

        /// <summary>
        /// Indicates whether this criteria has been initialized with calculated values.
        /// </summary>
        [JsonIgnore]
        public bool IsCalculated => MaxFlowSupply.HasValue && MaxFlowReturn.HasValue && Dim != null;
        #endregion

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
        /// Creates a copy of this instance (configuration only, not calculated values).
        /// </summary>
        public DnAcceptCriteria Clone()
        {
            return new DnAcceptCriteria(NominalDiameter, MaxVelocity, MaxPressureGradient, IsInitialized);
        }

        /// <summary>
        /// Clears calculated runtime values. Call this when criteria is modified.
        /// </summary>
        public void ClearCalculatedValues()
        {
            MaxFlowSupply = null;
            MaxFlowReturn = null;
            Dim = null;
        }

        public override string ToString() => $"DN{NominalDiameter}: v={MaxVelocity}m/s, dP={MaxPressureGradient}Pa/m";
    }
}
