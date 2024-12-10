using DimensioneringV2.UI;

using Mapsui.Nts;

using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Reflection;

using static IntersectUtilities.UtilsCommon.Utils;

namespace DimensioneringV2.GraphFeatures
{
    internal class AnalysisFeature : GeometryFeature, IFeature, ICloneable
    {
        #region Constructors
        public AnalysisFeature() : base() { }

        public AnalysisFeature(AnalysisFeature analysisFeature) : base(analysisFeature)
        {
            //Geometry = analysisFeature.Geometry?.Copy();
            InitCachedProperties();
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
        public bool IsRootNode { get; private set; }
        /// <summary>
        /// Length of the segment
        /// </summary>
        public double Length { get; private set; }
        /// <summary>
        /// Is the segment a service line, then returns 1, else 0
        /// </summary>
        public int NumberOfBuildingsConnected { get; private set; }
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
        public int NumberOfUnitsConnected { get; private set; }
        /// <summary>
        /// Lists the heating demand of the building connected to the segment
        /// </summary>
        public double HeatingDemandConnected { get; private set; }

        //Summation of data for analyses
        /// <summary>
        /// Helping flag for graph traversal.
        /// </summary>
        public bool Visited { get; set; } = false;

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
        public Dim Dim { get; internal set; }

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