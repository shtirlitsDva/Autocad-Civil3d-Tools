﻿using Mapsui.Nts;

using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;

using static IntersectUtilities.UtilsCommon.Utils;

namespace DimensioneringV2.GraphFeatures
{
    internal class AnalysisFeature : GeometryFeature, IFeature
    {
        #region Constructors
        public AnalysisFeature() : base() { }

        public AnalysisFeature(AnalysisFeature analysisFeature) : base(analysisFeature)
        {
            Geometry = analysisFeature.Geometry?.Copy();
        }
        public AnalysisFeature(NetTopologySuite.Geometries.Geometry? geometry)
        {
            Geometry = geometry;
            
        }
        public AnalysisFeature(
            NetTopologySuite.Geometries.Geometry geometry,
            Dictionary<string, object> attributes) : base(geometry)
        {
            foreach (var attribute in attributes)
            {
                this[attribute.Key] = attribute.Value;
            }
            
            Length = geometry.Length;
            NumberOfBuildingsConnected = 
                (this["IsBuildingConnection"] as bool? ?? false) == true ? 1 : 0;
            NumberOfUnitsConnected = this["AntalEnheder"] as int? ?? 0;
            BuildingHeatingDemand = this["EstimeretVarmeForbrug"] as double? ?? 0;
        }
        #endregion

        #region Calculation properties
        //Cached from attributes
        /// <summary>
        /// Length of the segment
        /// </summary>
        public double Length { get; private set; }
        /// <summary>
        /// Is the segment a service line, then returns 1, else 0
        /// </summary>
        public int NumberOfBuildingsConnected { get; private set; }
        /// <summary>
        /// Lists the number of units connected to the segment
        /// </summary>
        public int NumberOfUnitsConnected { get; private set; }
        /// <summary>
        /// Lists the heating demand of the building connected to the segment
        /// </summary>
        public double BuildingHeatingDemand { get; private set; }

        //Summation from analyses
        /// <summary>
        /// Is used to sum all the buildins supplied by the segment
        /// </summary>
        public int NumberOfBuildingsSupplied { get; set; }
        /// <summary>
        /// Is used to sum all the units (enheder for HWS (hot water supply)) supplied by the segment
        /// </summary>
        public int NumberOfUnitsSupplied { get; set; }
        /// <summary>
        /// Is used to sum all the heating demand of the buildings supplied by the segment
        /// </summary>
        public double HeatingDemandSummarized { get; set; }


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