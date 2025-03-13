using DimensioneringV2.UI;

using Mapsui.Nts;

using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using static IntersectUtilities.UtilsCommon.Utils;

namespace DimensioneringV2.GraphFeatures
{
    internal class AnalysisFeature : GeometryFeature, IFeature, ICloneable, IHydraulicSegment, IInfoForFeature
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
            // Copy all AnalysisFeature-specific properties
            IsRootNode = analysisFeature.IsRootNode;
            Length = analysisFeature.Length;
            NumberOfBuildingsConnected = analysisFeature.NumberOfBuildingsConnected;
            NumberOfUnitsConnected = analysisFeature.NumberOfUnitsConnected;
            HeatingDemandConnected = analysisFeature.HeatingDemandConnected;

            NumberOfBuildingsSupplied = analysisFeature.NumberOfBuildingsSupplied;
            NumberOfUnitsSupplied = analysisFeature.NumberOfUnitsSupplied;
            HeatingDemandSupplied = analysisFeature.HeatingDemandSupplied;

            PipeDim = analysisFeature.PipeDim;
            ReynoldsSupply = analysisFeature.ReynoldsSupply;
            ReynoldsReturn = analysisFeature.ReynoldsReturn;
            FlowSupply = analysisFeature.FlowSupply;
            FlowReturn = analysisFeature.FlowReturn;
            PressureGradientSupply = analysisFeature.PressureGradientSupply;
            PressureGradientReturn = analysisFeature.PressureGradientReturn;
            VelocitySupply = analysisFeature.VelocitySupply;
            VelocityReturn = analysisFeature.VelocityReturn;
            UtilizationRate = analysisFeature.UtilizationRate;

            IsBridge = analysisFeature.IsBridge;
            SubGraphId = analysisFeature.SubGraphId;
            PressureLossAtClient = analysisFeature.PressureLossAtClient;
            IsCriticalPath = analysisFeature.IsCriticalPath;

            foreach (var field in analysisFeature.Fields) this[field] = analysisFeature[field];
        }
        public AnalysisFeature(NetTopologySuite.Geometries.Geometry? geometry) : base(geometry)
        {
            //Geometry = geometry;
        }
        public AnalysisFeature(
            NetTopologySuite.Geometries.Geometry geometry,
            Dictionary<string, object> attributes) : base(geometry)
        {
            foreach (var attribute in attributes)
            {
                this[attribute.Key] = attribute.Value;
            }

            InitCachedProperties();
        }

        private void InitCachedProperties()
        {
            IsRootNode = this["IsRootNode"] as bool? ?? false;
            Length = Geometry!.Length;
            NumberOfBuildingsConnected =
                (this["IsBuildingConnection"] as bool? ?? false) == true ? 1 : 0;
            NumberOfUnitsConnected = this["AntalEnheder"] as int? ?? 0;
            HeatingDemandConnected = this["EstimeretVarmeForbrug"] as double? ?? 0;
        }
        #endregion

        #region Calculation properties
        //Cached from attributes
        /// <summary>
        /// Is the segment a root node, then returns true, else false
        /// </summary>
        public bool IsRootNode { get; set; }
        /// <summary>
        /// Length of the segment
        /// </summary>
        public double Length { get; set; }
        /// <summary>
        /// Is the segment a service line, then returns 1, else 0
        /// </summary>
        public int NumberOfBuildingsConnected { get; set; }
        /// <summary>
        /// Signifies what type of segment the feature represents.
        /// </summary>
        public SegmentType SegmentType =>
            NumberOfBuildingsConnected == 1 ?
            SegmentType.Stikledning :
            SegmentType.Fordelingsledning;
        /// <summary>
        /// Lists the number of units connected to the segment
        /// </summary>
        public int NumberOfUnitsConnected { get; set; }
        /// <summary>
        /// Lists the heating demand of the building connected to the segment
        /// </summary>
        public double HeatingDemandConnected { get; set; }

        //Calculated properties
        /// <summary>
        /// Is used to sum all the buildins supplied by the segment
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.Bygninger)]
        public int NumberOfBuildingsSupplied { get; set; }
        /// <summary>
        /// Is used to sum all the units (enheder for HWS (hot water supply)) supplied by the segment
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.Units)]
        public int NumberOfUnitsSupplied { get; set; }
        /// <summary>
        /// Is used to sum all the heating demand of the buildings supplied by the segment
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.HeatingDemand)]
        public double HeatingDemandSupplied { get; set; }

        //Storing of hydraulic results
        /// <summary>
        /// Dimension
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.Pipe)]
        public Dim PipeDim { get; set; }

        /// <summary>
        /// Reynolds number for supply
        /// </summary>
        public double ReynoldsSupply { get; set; }

        /// <summary>
        /// Reynolds number for return
        /// </summary>
        public double ReynoldsReturn { get; set; }

        /// <summary>
        /// Flow rate for supply
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.FlowSupply)]
        public double FlowSupply { get; set; }

        /// <summary>
        /// Flow rate for return
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.FlowReturn)]
        public double FlowReturn { get; set; }

        /// <summary>
        /// Pressure gradient for supply
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.PressureGradientSupply)]
        public double PressureGradientSupply { get; set; }

        /// <summary>
        /// Pressure gradient for return
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.PressureGradientReturn)]
        public double PressureGradientReturn { get; set; }

        /// <summary>
        /// Velocity for supply
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.VelocitySupply)]
        public double VelocitySupply { get; set; }

        /// <summary>
        /// Velocity for return
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.VelocityReturn)]
        public double VelocityReturn { get; set; }

        /// <summary>
        /// Utilization rate
        /// </summary>
        [MapPropertyAttribute(MapPropertyEnum.UtilizationRate)]
        public double UtilizationRate { get; set; }

        /// <summary>
        /// Marks a segment as a bridge (ie. cannot be removed without disconnecting the network)
        /// </summary>
        [MapProperty(MapPropertyEnum.Bridge)]
        public bool IsBridge { get; set; } = false;

        /// <summary>
        /// Stores the current subgraph id
        /// </summary>
        [MapProperty(MapPropertyEnum.SubGraphId)]
        public int SubGraphId { get; set; } = -1;

        /// <summary>
        /// Total pressure loss for the client.
        /// This is intented only for clients' connections
        /// where the pressure loss is calculated for the whole path.
        /// It should include the allowed client loss from HydraulicCalculationSettings
        /// </summary>
        public double PressureLossAtClient { get; set; } = 0;

        /// <summary>
        /// Marks the segment as a part of a critical path.
        /// </summary>
        [MapProperty(MapPropertyEnum.CriticalPath)]
        public bool IsCriticalPath { get; set; } = false;

        public void ResetHydraulicResults()
        {
            NumberOfBuildingsSupplied = 0;
            NumberOfUnitsSupplied = 0;
            HeatingDemandSupplied = 0;
            PipeDim = Dim.NA;
            ReynoldsSupply = 0;
            ReynoldsReturn = 0;
            FlowSupply = 0;
            FlowReturn = 0;
            PressureGradientSupply = 0;
            PressureGradientReturn = 0;
            VelocitySupply = 0;
            VelocityReturn = 0;
            UtilizationRate = 0;
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

        IEnumerable<PropertyItem> IInfoForFeature.PropertiesToDataGrid()
        {
            var props = GetType()
            .GetProperties(System.Reflection.BindingFlags.Public |
                           System.Reflection.BindingFlags.Instance |
                           System.Reflection.BindingFlags.DeclaredOnly)
            .OrderBy(p => p.MetadataToken)
            .Where(p => p.Name != "BoundingBox")
            .Where(p => p.Name != "Attributes")
            .Select(pi => new PropertyItem(pi.Name, pi.GetValue(this)?.ToString() ?? "null"));

            return props;
        }
        #endregion

        #endregion

        #region NTS IFeature for serialization of FeatureCollection
        //NTS IFeature implementation
        //We shouldn't mix these Mapsui and NTS stuff
        //But right now it is to support creation of FeatureCollection
        //to be able to serialize it to GeoJSON directly without translating
        public Envelope BoundingBox { get => this.Geometry!.EnvelopeInternal; set => throw new NotImplementedException(); }
        public IAttributesTable Attributes { get => _attributes; set => throw new NotImplementedException(); }
        private AttributesTable _attributes
        {
            get
            {
                AttributesTable table = new();
                foreach (string field in this.Fields)
                {
                    table.Add(field, this[field]);
                }
                return table;
            }
        }
        #endregion
    }
}