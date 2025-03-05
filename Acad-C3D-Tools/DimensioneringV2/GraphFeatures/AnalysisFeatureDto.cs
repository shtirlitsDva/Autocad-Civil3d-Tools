using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NetTopologySuite.Geometries;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

namespace DimensioneringV2.GraphFeatures
{
    internal class AnalysisFeatureDto
    {
        public double [][] Coordinates { get; set; }
        public Dictionary<string, object> Attributes { get; set; }

        // Cached properties
        public bool IsRootNode { get; set; }
        public double Length { get; set; }
        public int NumberOfBuildingsConnected { get; set; }
        public SegmentType SegmentType { get; set; }
        public int NumberOfUnitsConnected { get; set; }
        public double HeatingDemandConnected { get; set; }

        // Calculated properties
        public int NumberOfBuildingsSupplied { get; set; }
        public int NumberOfUnitsSupplied { get; set; }
        public double HeatingDemandSupplied { get; set; }

        // Hydraulic results
        public Dim PipeDim { get; set; }
        public double ReynoldsSupply { get; set; }
        public double ReynoldsReturn { get; set; }
        public double FlowSupply { get; set; }
        public double FlowReturn { get; set; }
        public double PressureGradientSupply { get; set; }
        public double PressureGradientReturn { get; set; }
        public double VelocitySupply { get; set; }
        public double VelocityReturn { get; set; }
        public double UtilizationRate { get; set; }
        public bool IsBridge { get; set; }
        public int SubGraphId { get; set; }
        public double PressureLossAtClient { get; set; }
        public bool IsCriticalPath { get; set; }

        public AnalysisFeatureDto() { }

        public AnalysisFeatureDto(AnalysisFeature analysisFeature)
        {
            if (analysisFeature.Geometry is not LineString lineString)
            {
                throw new ArgumentException("Geometry must be a LineString!" +
                    "\n"+string.Join(", ", 
                    analysisFeature.Geometry.Coordinates.Select(x => x)));
            }

            Coordinates = lineString.Coordinates
                .Select(c => new double[] { c.X, c.Y })
                .ToArray();
            Attributes = new Dictionary<string, object>();
            foreach (var field in analysisFeature.Fields)
            {
                Attributes[field] = analysisFeature[field];
            }

            // Cached properties
            IsRootNode = analysisFeature.IsRootNode;
            Length = analysisFeature.Length;
            NumberOfBuildingsConnected = analysisFeature.NumberOfBuildingsConnected;
            SegmentType = analysisFeature.SegmentType;
            NumberOfUnitsConnected = analysisFeature.NumberOfUnitsConnected;
            HeatingDemandConnected = analysisFeature.HeatingDemandConnected;

            // Calculated properties
            NumberOfBuildingsSupplied = analysisFeature.NumberOfBuildingsSupplied;
            NumberOfUnitsSupplied = analysisFeature.NumberOfUnitsSupplied;
            HeatingDemandSupplied = analysisFeature.HeatingDemandSupplied;

            // Hydraulic results
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
        }

        public AnalysisFeature ToAnalysisFeature()
        {
            var coordinates = Coordinates
                .Select(c => new Coordinate(c[0], c[1]))
                .ToArray();

            var geometry = new LineString(coordinates);

            var analysisFeature = new AnalysisFeature(geometry, Attributes)
            {
                // Cached properties
                IsRootNode = IsRootNode,
                Length = Length,
                NumberOfBuildingsConnected = NumberOfBuildingsConnected,
                NumberOfUnitsConnected = NumberOfUnitsConnected,
                HeatingDemandConnected = HeatingDemandConnected,

                // Calculated properties
                NumberOfBuildingsSupplied = NumberOfBuildingsSupplied,
                NumberOfUnitsSupplied = NumberOfUnitsSupplied,
                HeatingDemandSupplied = HeatingDemandSupplied,

                // Hydraulic results
                PipeDim = PipeDim,
                ReynoldsSupply = ReynoldsSupply,
                ReynoldsReturn = ReynoldsReturn,
                FlowSupply = FlowSupply,
                FlowReturn = FlowReturn,
                PressureGradientSupply = PressureGradientSupply,
                PressureGradientReturn = PressureGradientReturn,
                VelocitySupply = VelocitySupply,
                VelocityReturn = VelocityReturn,
                UtilizationRate = UtilizationRate,
                IsBridge = IsBridge,
                SubGraphId = SubGraphId,
                PressureLossAtClient = PressureLossAtClient,
                IsCriticalPath = IsCriticalPath
            };

            return analysisFeature;
        }
    }
}