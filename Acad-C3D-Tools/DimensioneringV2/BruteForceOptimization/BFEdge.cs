﻿using DimensioneringV2.GraphFeatures;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.BruteForceOptimization
{
    internal class BFEdge : Edge<BFNode>, IHydraulicSegment
    {
        public int Id { get; set; }
        public double Price { get => PipeDim.Price_m * Length + PipeDim.Price_stk(SegmentType); }
        public bool IsRootNode { get => OriginalEdge.PipeSegment.IsRootNode; }
        public double Length { get => OriginalEdge.PipeSegment.Length; }
        public int NumberOfBuildingsConnected { get => OriginalEdge.PipeSegment.NumberOfBuildingsConnected; }
        public SegmentType SegmentType =>
            NumberOfBuildingsConnected == 1 ?
            SegmentType.Stikledning :
            SegmentType.Fordelingsledning;
        public int NumberOfUnitsConnected { get => OriginalEdge.PipeSegment.NumberOfUnitsConnected; }
        public double HeatingDemandConnected { get => OriginalEdge.PipeSegment.HeatingDemandConnected; }
        public int NumberOfBuildingsSupplied { get; set; }
        public int NumberOfUnitsSupplied { get; set; }
        public double HeatingDemandSupplied { get; set; }
        public Dim PipeDim { get; set; }
        public double ReynoldsSupply { get; set; }
        public double ReynoldsReturn { get; set; }
        public double FlowSupply { get; set; }
        public double FlowReturn { get; set; }
        public double PressureGradientSupply { get; set; }
        public double PressureGradientReturn { get; set; }
        public double PressureLossAtClient { get; set; } // Pressure loss at the client
        public bool IsCriticalPath { get; set; } = false;
        public double VelocitySupply { get; set; }
        public double VelocityReturn { get; set; }
        public double UtilizationRate { get; set; }        
        public EdgePipeSegment OriginalEdge { get; }
        public int NonBridgeChromosomeIndex { get; internal set; } = -1;
        public BFEdge([NotNull] BFNode source, [NotNull] BFNode target, EdgePipeSegment edge) : base(source, target)
        {
            OriginalEdge = edge;
        }

        public void PushBaseSums()
        {
            OriginalEdge.PipeSegment.NumberOfBuildingsSupplied = NumberOfBuildingsSupplied;
            OriginalEdge.PipeSegment.NumberOfUnitsSupplied = NumberOfUnitsSupplied;
            OriginalEdge.PipeSegment.HeatingDemandSupplied = HeatingDemandSupplied;
        }

        public void PushAllResults()
        {
            PushBaseSums();
            OriginalEdge.PipeSegment.PipeDim = PipeDim;
            OriginalEdge.PipeSegment.ReynoldsSupply = ReynoldsSupply;
            OriginalEdge.PipeSegment.ReynoldsReturn = ReynoldsReturn;
            OriginalEdge.PipeSegment.FlowSupply = FlowSupply;
            OriginalEdge.PipeSegment.FlowReturn = FlowReturn;
            OriginalEdge.PipeSegment.PressureGradientSupply = PressureGradientSupply;
            OriginalEdge.PipeSegment.PressureGradientReturn = PressureGradientReturn;
            OriginalEdge.PipeSegment.PressureLossAtClient = PressureLossAtClient;
            OriginalEdge.PipeSegment.IsCriticalPath = IsCriticalPath;
            OriginalEdge.PipeSegment.VelocitySupply = VelocitySupply;
            OriginalEdge.PipeSegment.VelocityReturn = VelocityReturn;
            OriginalEdge.PipeSegment.UtilizationRate = UtilizationRate;            
        }

        internal void SyncBaseSums(BFEdge edge)
        {
            NumberOfBuildingsSupplied = edge.NumberOfBuildingsSupplied;
            NumberOfUnitsSupplied = edge.NumberOfUnitsSupplied;
            HeatingDemandSupplied = edge.HeatingDemandSupplied;
        }

        public override string ToString() =>
            $"BFEdge(Id={Id}, {Source.Id}--{Target.Id})";

        internal void YankAllResults()
        {
            //Yank all results from the original edge
            NumberOfBuildingsSupplied = OriginalEdge.PipeSegment.NumberOfBuildingsSupplied;
            NumberOfUnitsSupplied = OriginalEdge.PipeSegment.NumberOfUnitsSupplied;
            HeatingDemandSupplied = OriginalEdge.PipeSegment.HeatingDemandSupplied;
            PipeDim = OriginalEdge.PipeSegment.PipeDim;
            ReynoldsSupply = OriginalEdge.PipeSegment.ReynoldsSupply;
            ReynoldsReturn = OriginalEdge.PipeSegment.ReynoldsReturn;
            FlowSupply = OriginalEdge.PipeSegment.FlowSupply;
            FlowReturn = OriginalEdge.PipeSegment.FlowReturn;
            PressureGradientSupply = OriginalEdge.PipeSegment.PressureGradientSupply;
            PressureGradientReturn = OriginalEdge.PipeSegment.PressureGradientReturn;
            PressureLossAtClient = OriginalEdge.PipeSegment.PressureLossAtClient;
            IsCriticalPath = OriginalEdge.PipeSegment.IsCriticalPath;
            VelocitySupply = OriginalEdge.PipeSegment.VelocitySupply;
            VelocityReturn = OriginalEdge.PipeSegment.VelocityReturn;
            UtilizationRate = OriginalEdge.PipeSegment.UtilizationRate;
        }
    }
}
