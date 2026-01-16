using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Holds velocity and pressure gradient accept criteria for a single pipe dimension (DN),
    /// along with calculated max flow values computed during hydraulic calculation initialization.
    /// Implements INotifyPropertyChanged for WPF data binding.
    /// </summary>
    public class DnAcceptCriteria : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion

        #region Configuration Properties (Serialized)
        private int _nominalDiameter;
        private double _maxVelocity;
        private int _maxPressureGradient;
        private bool _isInitialized;

        /// <summary>
        /// Nominal diameter (e.g., 50, 63, 75, 100, etc.)
        /// </summary>
        public int NominalDiameter
        {
            get => _nominalDiameter;
            set => SetProperty(ref _nominalDiameter, value);
        }

        /// <summary>
        /// Maximum allowed velocity in m/s.
        /// </summary>
        public double MaxVelocity
        {
            get => _maxVelocity;
            set
            {
                if (SetProperty(ref _maxVelocity, value))
                {
                    // Mark as initialized when user changes the value
                    IsInitialized = true;
                }
            }
        }

        /// <summary>
        /// Maximum allowed pressure gradient in Pa/m.
        /// </summary>
        public int MaxPressureGradient
        {
            get => _maxPressureGradient;
            set
            {
                if (SetProperty(ref _maxPressureGradient, value))
                {
                    // Mark as initialized when user changes the value
                    IsInitialized = true;
                }
            }
        }

        /// <summary>
        /// Indicates whether the user has explicitly reviewed/set this criteria.
        /// Used for UI validation to warn about unreviewed defaults.
        /// </summary>
        public bool IsInitialized
        {
            get => _isInitialized;
            set => SetProperty(ref _isInitialized, value);
        }
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
            _nominalDiameter = nominalDiameter;
            _maxVelocity = maxVelocity;
            _maxPressureGradient = maxPressureGradient;
            _isInitialized = isInitialized;
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
