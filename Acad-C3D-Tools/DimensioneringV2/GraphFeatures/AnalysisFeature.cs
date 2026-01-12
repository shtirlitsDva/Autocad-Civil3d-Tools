using DimensioneringV2.Services;
using DimensioneringV2.Services.GDALClient;
using DimensioneringV2.UI;

using Mapsui.Nts;

using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DimensioneringV2.GraphFeatures
{
    public sealed class AnalysisFeature : GeometryFeature, IFeature, ICloneable, IInfoForFeature
    {
        #region Constructors
        public AnalysisFeature() : base() { }

        /// <summary>
        /// This constructor is used to create a new AnalysisFeature from an existing one.
        /// This is used in the ReProjectFeatures method in the ProjectionService class.
        /// Mapsui internally copyies the original feature by activating a new instance
        /// of the feature and using this constructor.
        /// So this constructor needs to copy all the values from the original feature.
        /// </summary>
        public AnalysisFeature(AnalysisFeature analysisFeature) : base(analysisFeature)
        {
            foreach (var field in analysisFeature.Fields) this[field] = analysisFeature[field];
            this.OriginalGeometry = analysisFeature.OriginalGeometry;
            this.Elevations = new ElevationProfileCache(this, OriginalGeometry);
        }
        public AnalysisFeature(
            NetTopologySuite.Geometries.Geometry? geometry,
            OriginalGeometry originalGeometry) : base(geometry)
        {
            OriginalGeometry = originalGeometry;
            Elevations = new ElevationProfileCache(this, originalGeometry);
        }
        public AnalysisFeature(
            NetTopologySuite.Geometries.Geometry geometry,
            OriginalGeometry originalGeometry,
            Dictionary<string, object> attributes) : base(geometry)
        {
            foreach (var attribute in attributes)
            {
                this[attribute.Key] = attribute.Value;
            }
            this.OriginalGeometry = originalGeometry;
            this.Elevations = new ElevationProfileCache(this, originalGeometry);
        }
        #endregion

        #region Spatial data storage
        public OriginalGeometry OriginalGeometry { get; }
        internal ElevationProfileCache Elevations { get; }
        #endregion

        #region Properties to attributes mapper, indexer
        private static Dictionary<MapPropertyEnum, string>? _propertyKeyLookup;
        private static Dictionary<MapPropertyEnum, string> PropertyKeyLookup =>
            _propertyKeyLookup ??= typeof(AnalysisFeature)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(prop => (prop, attr: prop.GetCustomAttribute<MapPropertyAttribute>()))
                .Where(x => x.attr != null)
                .ToDictionary(
                    x => x.attr!.Property,
                    x => x.prop.Name
                );

        private static Dictionary<MapPropertyEnum, PropertyInfo>? _propertyInfoLookup;
        private static Dictionary<MapPropertyEnum, PropertyInfo> PropertyInfoLookup =>
            _propertyInfoLookup ??= typeof(AnalysisFeature)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(prop => (prop, attr: prop.GetCustomAttribute<MapPropertyAttribute>()))
                .Where(x => x.attr != null)
                .ToDictionary(
                    x => x.attr!.Property,
                    x => x.prop
                );
        public object? this[MapPropertyEnum property]
        {
            get
            {
                if (!PropertyKeyLookup.TryGetValue(property, out var key))
                    throw new KeyNotFoundException($"Property enum '{property}' not found in lookup.");
                return this[key];
            }
            set
            {
                if (!PropertyKeyLookup.TryGetValue(property, out var key))
                    throw new KeyNotFoundException($"Property enum '{property}' not found in lookup.");

                if (value == null)
                    throw new ArgumentNullException(nameof(value), $"Cannot assign null to property '{property}' via indexer.");

                this[key] = value!;
            }
        }
        public T GetAttributeValue<T>(MapPropertyEnum property)
        {
            var val = this[property];
            if (val is T tVal) return tVal;
            if (val == null) return default!;
            return (T)Convert.ChangeType(val, typeof(T));
        }
        public void SetAttributeValue<T>(MapPropertyEnum property, T value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"Property {property} cannot be set to null!");
            this[property] = value!;
        }

        public static string GetAttributeName(MapPropertyEnum property)
        {
            if (!PropertyKeyLookup.TryGetValue(property, out var key))
                throw new KeyNotFoundException($"Property enum '{property}' not found in lookup.");
            return key;
        }

        /// <summary>
        /// Gets the display value for a property by invoking the property getter.
        /// This ensures that any custom logic in the property getter is executed,
        /// unlike direct attribute table access which bypasses property logic.
        /// </summary>
        public object? GetDisplayValue(MapPropertyEnum property)
        {
            if (!PropertyInfoLookup.TryGetValue(property, out var propInfo))
                throw new KeyNotFoundException($"Property enum '{property}' not found in lookup.");
            return propInfo.GetValue(this);
        }
        #endregion

        #region Calculation properties
        /// <summary>
        /// Is the segment a root node, then returns true, else false
        /// </summary>
        public bool IsRootNode
        {
            get => this["IsRootNode"] is bool value ? value : false;
            //set => this["IsRootNode"] = value;
        }

        /// <summary>
        /// Length of the segment
        /// </summary>
        public double Length => OriginalGeometry.Length;

        /// <summary>
        /// Is the segment a service line, then returns 1, else 0
        /// </summary>
        public int NumberOfBuildingsConnected
        {
            get => (this["IsBuildingConnection"] as bool? ?? false) ? 1 : 0;
        }

        /// <summary>
        /// Lists the number of units connected to the segment
        /// </summary>
        public int NumberOfUnitsConnected
        {
            get => this["AntalEnheder"] as int? ?? 0;
            //set => this["NumberOfUnitsConnected"] = value;
        }

        /// <summary>
        /// Lists the heating demand of the building connected to the segment
        /// </summary>
        public double HeatingDemandConnected
        {
            get => this["EstimeretVarmeForbrug"] as double? ?? 0;
            //set => this["HeatingDemandConnected"] = value;
        }

        /// <summary>
        /// Signifies what type of segment the feature represents.
        /// </summary>
        public SegmentType SegmentType =>
            NumberOfBuildingsConnected == 1 ?
            SegmentType.Stikledning :
            SegmentType.Fordelingsledning;

        /// <summary>
        /// Is used to sum all the buildins supplied by the segment
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.Bygninger)]
        public int NumberOfBuildingsSupplied
        {
            get => GetAttributeValue<int>(MapPropertyEnum.Bygninger);
            set => SetAttributeValue(MapPropertyEnum.Bygninger, value);
        }

        /// <summary>
        /// Is used to sum all the units (enheder for HWS (hot water supply)) supplied by the segment
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.Units)]
        public int NumberOfUnitsSupplied
        {
            get => GetAttributeValue<int>(MapPropertyEnum.Units);
            set => SetAttributeValue(MapPropertyEnum.Units, value);
        }

        /// <summary>
        /// Is used to sum all the heating demand of the buildings supplied by the segment
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.HeatingDemand)]
        public double HeatingDemandSupplied
        {
            get => GetAttributeValue<double>(MapPropertyEnum.HeatingDemand);
            set => SetAttributeValue(MapPropertyEnum.HeatingDemand, value);
        }

        /// <summary>
        /// Dimension
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.Pipe)]
        public Dim Dim
        {
            get => GetAttributeValue<Dim>(MapPropertyEnum.Pipe);
            set => SetAttributeValue(MapPropertyEnum.Pipe, value);
        }

        /// <summary>
        /// Used for storing the previous dimension before a change
        /// In use specifically for set/reset dim manual setting
        /// </summary>
        internal Dim PreviousDim { get; set; }

        /// <summary>
        /// Reynolds number for supply
        /// </summary>
        public double ReynoldsSupply
        {
            get => this["ReynoldsSupply"] as double? ?? 0;
            set => this["ReynoldsSupply"] = value;
        }

        /// <summary>
        /// Reynolds number for return
        /// </summary>
        public double ReynoldsReturn
        {
            get => this["ReynoldsReturn"] as double? ?? 0;
            set => this["ReynoldsReturn"] = value;
        }

        /// <summary>
        /// Flow rate for supply
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.DimFlowSupply)]
        public double DimFlowSupply
        {
            get => GetAttributeValue<double>(MapPropertyEnum.DimFlowSupply);
            set => SetAttributeValue(MapPropertyEnum.DimFlowSupply, value);
        }

        /// <summary>
        /// Flow rate for return
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.DimFlowReturn)]
        public double DimFlowReturn
        {
            get => GetAttributeValue<double>(MapPropertyEnum.DimFlowReturn);
            set => SetAttributeValue(MapPropertyEnum.DimFlowReturn, value);
        }

        /// <summary>
        /// Pressure gradient for supply
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.PressureGradientSupply)]
        public double PressureGradientSupply
        {
            get => GetAttributeValue<double>(MapPropertyEnum.PressureGradientSupply) * pct;
            set => SetAttributeValue(MapPropertyEnum.PressureGradientSupply, value);
        }
        private double pct => 1.0 + (double)HydraulicSettingsService.Instance.Settings.ProcentTillægTilTryktab / 100;

        /// <summary>
        /// Pressure gradient for return
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.PressureGradientReturn)]
        public double PressureGradientReturn
        {
            get => GetAttributeValue<double>(MapPropertyEnum.PressureGradientReturn) * pct;
            set => SetAttributeValue(MapPropertyEnum.PressureGradientReturn, value);
        }

        /// <summary>
        /// Velocity for supply
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.VelocitySupply)]
        public double VelocitySupply
        {
            get => GetAttributeValue<double>(MapPropertyEnum.VelocitySupply);
            set => SetAttributeValue(MapPropertyEnum.VelocitySupply, value);
        }

        /// <summary>
        /// Velocity for return
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.VelocityReturn)]
        public double VelocityReturn
        {
            get => GetAttributeValue<double>(MapPropertyEnum.VelocityReturn);
            set => SetAttributeValue(MapPropertyEnum.VelocityReturn, value);
        }

        /// <summary>
        /// Utilization rate
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.UtilizationRate)]
        public double UtilizationRate
        {
            get => GetAttributeValue<double>(MapPropertyEnum.UtilizationRate);
            set => SetAttributeValue(MapPropertyEnum.UtilizationRate, value);
        }

        /// <summary>
        /// Marks a segment as a bridge (ie. cannot be removed without disconnecting the network)
        /// </summary>
        [MapProperty(MapPropertyEnum.Bridge)]
        public bool IsBridge
        {
            get => GetAttributeValue<bool>(MapPropertyEnum.Bridge);
            set => SetAttributeValue(MapPropertyEnum.Bridge, value);
        }

        /// <summary>
        /// Stores the current subgraph id
        /// </summary>
        [MapProperty(MapPropertyEnum.SubGraphId)]
        public int SubGraphId
        {
            get => GetAttributeValue<int>(MapPropertyEnum.SubGraphId);
            set => SetAttributeValue(MapPropertyEnum.SubGraphId, value);
        }

        /// <summary>
        /// Total pressure loss for the client in the supply pipe.
        /// This is intented only for clients' connections
        /// where the pressure loss is calculated for the whole path.
        /// It should NOT include the allowed client loss from HydraulicCalculationSettings
        /// </summary>
        public double PressureLossAtClientSupply
        {
            get => this["PressureLossAtClientSupply"] as double? ?? 0;
            set => this["PressureLossAtClientSupply"] = value;
        }

        /// <summary>
        /// Total pressure loss for the client in the return pipe.
        /// This is intented only for clients' connections
        /// where the pressure loss is calculated for the whole path.
        /// It should NOT include the allowed client loss from HydraulicCalculationSettings
        /// </summary>
        public double PressureLossAtClientReturn
        {
            get => this["PressureLossAtClientReturn"] as double? ?? 0;
            set => this["PressureLossAtClientReturn"] = value;
        }

        /// <summary>
        /// Differential pressure at the client.
        /// This is intented only for clients' connections
        /// where the differential pressure is calculated
        /// using the max pressure for network minus
        /// pressure loss for the whole path in both dirs.
        /// </summary>
        public double DifferentialPressureAtClient
        {
            get => this["DifferentialPressureAtClient"] as double? ?? 0;
            set => this["DifferentialPressureAtClient"] = value;
        }

        /// <summary>
        /// Marks the segment as a part of a critical path.
        /// </summary>
        [MapProperty(MapPropertyEnum.CriticalPath)]
        public bool IsCriticalPath
        {
            get => GetAttributeValue<bool>(MapPropertyEnum.CriticalPath);
            set => SetAttributeValue(MapPropertyEnum.CriticalPath, value);
        }

        /// <summary>
        /// Marks the segment as manually assigned dimension.
        /// It may not be overriden by automatic dimensioning.
        /// </summary>
        [MapProperty(MapPropertyEnum.ManualDim)]
        public bool ManualDim
        {
            get => GetAttributeValue<bool>(MapPropertyEnum.ManualDim);
            set => SetAttributeValue(MapPropertyEnum.ManualDim, value);
        }

        /// <summary>
        /// The temperature delta at the client connection for heating.
        /// </summary>
        [MapProperty(MapPropertyEnum.TempDeltaVarme)]
        public double TempDeltaVarme
        {
            get => GetAttributeValue<double>(MapPropertyEnum.TempDeltaVarme);
            //set => SetAttributeValue(MapPropertyEnum.TempDelta, value);
        }

        /// <summary>
        /// The temperature delta at the client connection for water.
        /// </summary>
        [MapProperty(MapPropertyEnum.TempDeltaBV)]
        public double TempDeltaBV
        {
            get => GetAttributeValue<double>(MapPropertyEnum.TempDeltaBV);
            //set => SetAttributeValue(MapPropertyEnum.TempDelta, value);
        }

        /// <summary>
        /// Anvendelseskode for the feature.
        /// </summary>
        [MapProperty(MapPropertyEnum.BygningsAnvendelseNyKode)]
        public string BygningsAnvendelseNyKode
        {
            get => GetAttributeValue<string>(MapPropertyEnum.BygningsAnvendelseNyKode);
        }

        /// <summary>
        /// Nyttetimer value from the current configuration based on AnvendelsesKode.
        /// Only valid for Stikledning segments. Returns 0 for Fordelingsledning.
        /// This property dynamically reads from NyttetimerService so changes in settings
        /// are reflected when the map is refreshed.
        /// </summary>
        [MapProperty(MapPropertyEnum.Nyttetimer)]
        public int Nyttetimer
        {
            get
            {
                if (SegmentType != SegmentType.Stikledning) return 0;

                var kode = BygningsAnvendelseNyKode;
                var defaultValue = HydraulicSettingsService.Instance.Settings.BygningsnyttetimerDefault;
                return NyttetimerService.Instance.GetNyttetimer(kode, defaultValue);
            }
        }

        public void ResetHydraulicResults()
        {
            NumberOfBuildingsSupplied = 0;
            NumberOfUnitsSupplied = 0;
            HeatingDemandSupplied = 0;
            Dim = Dim.NA;
            ReynoldsSupply = 0;
            ReynoldsReturn = 0;
            DimFlowSupply = 0;
            DimFlowReturn = 0;
            PressureGradientSupply = 0;
            PressureGradientReturn = 0;
            VelocitySupply = 0;
            VelocityReturn = 0;
            UtilizationRate = 0;
            IsCriticalPath = false;
            PressureLossAtClientSupply = 0;
            PressureLossAtClientReturn = 0;
            DifferentialPressureAtClient = 0;
            //SubGraphId = 0;
        }
        #region ICloneable Implementation
        public object Clone()
        {
            AnalysisFeature clonedObject = (AnalysisFeature)this.MemberwiseClone();
            foreach (PropertyInfo property in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanWrite && property.GetSetMethod() != null)
                {
                    property.SetValue(clonedObject, property.GetValue(this));
                }
            }
            return clonedObject;
        }
        private HashSet<string> ignoreProps = new HashSet<string>()
        {
            "BoundingBox",
            "Attributes",
            "OriginalGeometry",
            "Elevations"
        };
        IEnumerable<PropertyItem> IInfoForFeature.PropertiesToDataGrid()
        {
            var props = GetType()
            .GetProperties(System.Reflection.BindingFlags.Public |
                           System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.DeclaredOnly)
            .Where(p => p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.MetadataToken)
            .Where(p => !ignoreProps.Contains(p.Name))
            .Select(pi => new PropertyItem(pi.Name, pi.GetValue(this)?.ToString() ?? "null"));

            return props;
        }
        #endregion

        #endregion

        #region Display properties
        /// <summary>
        /// The address of the feature.
        /// </summary>
        [MapProperty(MapPropertyEnum.Address)]
        public string Adresse
        {
            get
            {
                if (this.SegmentType == SegmentType.Fordelingsledning) return string.Empty;
                var duplicat = this["AdresseDuplikatNr"] as int? ?? 0;
                var addresse = this["Adresse"] as string ?? string.Empty;
                if (duplicat != 0) addresse += $" ({duplicat})";
                return addresse;
            }
        }

        /// <summary>
        /// The anvendelse text of the feature.
        /// </summary>
        [MapProperty(MapPropertyEnum.BygningsAnvendelseNyTekst)]
        public string BygningsAnvendelseNyTekst
        {
            get => GetAttributeValue<string>(MapPropertyEnum.BygningsAnvendelseNyTekst);
        }

        /// <summary>
        /// The pressure loss in this pipe segment.
        /// </summary>
        public double PressureLossBAR
        {
            get
            {
                var len = Length; // In meters
                var pg1 = PressureGradientSupply; // In Pa/m
                var pg2 = PressureGradientReturn; // In Pa/m
                return (pg1 + pg2).PaToBar() * len;
            }
        }
        #endregion

        #region NTS IFeature for serialization of FeatureCollection
        //NTS IFeature implementation
        //We shouldn't mix these Mapsui and NTS stuff
        //But right now it is to support creation of FeatureCollection
        //to be able to serialize it to GeoJSON directly without translating
        public Envelope BoundingBox { get => this.Geometry!.EnvelopeInternal; set => throw new NotImplementedException(); }
        private AttributesTable _attributesTable;
        public IAttributesTable Attributes
        {
            get => _attributesTable ??= new AttributesTable(this.Fields.ToDictionary(f => f, f => this[f]));
            set => throw new NotImplementedException();
        }
        #endregion
    }
}