using DimensioneringV2.GraphFeatures;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using QuikGraph;

using System;
using System.Diagnostics.CodeAnalysis;

namespace DimensioneringV2.BruteForceOptimization
{
    /// <summary>
    /// WARNING! When adding new properties that read from OriginalEdge.PipeSegment,
    /// remember to cache the value in the constructor instead of accessing
    /// OriginalEdge.PipeSegment directly in the property getter.
    /// </summary>
    internal sealed class BFEdge : Edge<BFNode>, IHydraulicSegment
    {
        // Cached values from OriginalEdge.PipeSegment
        private readonly bool _isRootNode;
        private readonly double _length;
        private readonly int _numberOfBuildingsConnected;
        private readonly int _numberOfUnitsConnected;
        private readonly double _heatingDemandConnected;
        private readonly bool _manualDim;
        private readonly double _tempDeltaVarme;
        private readonly double _tempDeltaBV;

        public int Id { get; set; }
        public double Price { get => Dim.Price_m * Length + Dim.Price_stk(SegmentType); }
        public bool IsRootNode { get => _isRootNode; }
        public bool IsBridge { get; set; }
        public int SubGraphId { get; set; }
        public double Length { get => _length; }
        public int NumberOfBuildingsConnected { get => _numberOfBuildingsConnected; }
        public SegmentType SegmentType =>
            NumberOfBuildingsConnected == 1 ?
            SegmentType.Stikledning :
            SegmentType.Fordelingsledning;
        public int NumberOfUnitsConnected { get => _numberOfUnitsConnected; }
        public double HeatingDemandConnected { get => _heatingDemandConnected; }
        public int NumberOfBuildingsSupplied { get; set; }
        public int NumberOfUnitsSupplied { get; set; }
        public double HeatingDemandSupplied { get; set; }
        public Dim Dim { get; set; }
        public bool ManualDim { get => _manualDim; }
        public double ReynoldsSupply { get; set; }
        public double ReynoldsReturn { get; set; }
        public double KarFlowHeatSupply { get; set; }
        public double KarFlowBVSupply { get; set; }
        public double KarFlowHeatReturn { get; set; }
        public double KarFlowBVReturn { get; set; }
        public double DimFlowSupply { get; set; }
        public double DimFlowReturn { get; set; }
        public double PressureGradientSupply { get; set; }
        public double PressureGradientReturn { get; set; }
        public double PressureLossAtClientSupply { get; set; } // Pressure loss at the client
        public double PressureLossAtClientReturn { get; set; } // Pressure loss at the client
        public double DifferentialPressureAtClient { get; set; }
        public bool IsCriticalPath { get; set; } = false;
        public double VelocitySupply { get; set; }
        public double VelocityReturn { get; set; }
        public double UtilizationRate { get; set; }
        public EdgePipeSegment OriginalEdge { get; }
        public int NonBridgeChromosomeIndex { get; internal set; } = -1;
        public double TempDeltaVarme => _tempDeltaVarme;
        public double TempDeltaBV => _tempDeltaBV;
        public BFEdge([NotNull] BFNode source, [NotNull] BFNode target, EdgePipeSegment edge) : base(source, target)
        {
            OriginalEdge = edge;
            Dim = edge.PipeSegment.Dim;            
            _isRootNode = edge.PipeSegment.IsRootNode;
            _length = edge.PipeSegment.Length;
            _numberOfBuildingsConnected = edge.PipeSegment.NumberOfBuildingsConnected;
            _numberOfUnitsConnected = edge.PipeSegment.NumberOfUnitsConnected;
            _heatingDemandConnected = edge.PipeSegment.HeatingDemandConnected;
            _manualDim = edge.PipeSegment.ManualDim;
            _tempDeltaVarme = edge.PipeSegment.TempDeltaVarme;
            _tempDeltaBV = edge.PipeSegment.TempDeltaBV;
        }
        public BFEdge([NotNull] BFNode source, [NotNull] BFNode target, BFEdge edge) : base(source, target)
        {
            OriginalEdge = edge.OriginalEdge;
            Dim = edge.Dim;
            NumberOfBuildingsSupplied = edge.NumberOfBuildingsSupplied;
            NumberOfUnitsSupplied = edge.NumberOfUnitsSupplied;
            HeatingDemandSupplied = edge.HeatingDemandSupplied;
            KarFlowHeatSupply = edge.KarFlowHeatSupply;
            KarFlowBVSupply = edge.KarFlowBVSupply;
            KarFlowHeatReturn = edge.KarFlowHeatReturn;
            KarFlowBVReturn = edge.KarFlowBVReturn;       
            SubGraphId = edge.SubGraphId;
            NonBridgeChromosomeIndex = edge.NonBridgeChromosomeIndex;
            _isRootNode = edge.IsRootNode;
            _length = edge.Length;
            _numberOfBuildingsConnected = edge.NumberOfBuildingsConnected;
            _numberOfUnitsConnected = edge.NumberOfUnitsConnected;
            _heatingDemandConnected = edge.HeatingDemandConnected;
            _manualDim = edge.ManualDim;            
            _tempDeltaVarme = edge.TempDeltaVarme;
            _tempDeltaBV = edge.TempDeltaBV;
        }

        public BFEdge(BFEdge edge) : base(edge.Source, edge.Target)
        {
            OriginalEdge = edge.OriginalEdge;
            Dim = edge.Dim;
            NumberOfBuildingsSupplied = edge.NumberOfBuildingsSupplied;
            NumberOfUnitsSupplied = edge.NumberOfUnitsSupplied;
            HeatingDemandSupplied = edge.HeatingDemandSupplied;
            KarFlowHeatSupply = edge.KarFlowHeatSupply;
            KarFlowBVSupply = edge.KarFlowBVSupply;
            KarFlowHeatReturn = edge.KarFlowHeatReturn;
            KarFlowBVReturn = edge.KarFlowBVReturn;
            SubGraphId = edge.SubGraphId;
            NonBridgeChromosomeIndex = edge.NonBridgeChromosomeIndex;
            _isRootNode = edge.IsRootNode;
            _length = edge.Length;
            _numberOfBuildingsConnected = edge.NumberOfBuildingsConnected;
            _numberOfUnitsConnected = edge.NumberOfUnitsConnected;
            _heatingDemandConnected = edge.HeatingDemandConnected;
            _manualDim = edge.ManualDim;
            _tempDeltaVarme = edge.TempDeltaVarme;
            _tempDeltaBV = edge.TempDeltaBV;
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
            OriginalEdge.PipeSegment.Dim = Dim;
            OriginalEdge.PipeSegment.ReynoldsSupply = ReynoldsSupply;
            OriginalEdge.PipeSegment.ReynoldsReturn = ReynoldsReturn;
            OriginalEdge.PipeSegment.DimFlowSupply = DimFlowSupply;
            OriginalEdge.PipeSegment.DimFlowReturn = DimFlowReturn;
            OriginalEdge.PipeSegment.PressureGradientSupply = PressureGradientSupply;
            OriginalEdge.PipeSegment.PressureGradientReturn = PressureGradientReturn;
            OriginalEdge.PipeSegment.PressureLossAtClientSupply = PressureLossAtClientSupply;
            OriginalEdge.PipeSegment.PressureLossAtClientReturn = PressureLossAtClientReturn;
            OriginalEdge.PipeSegment.DifferentialPressureAtClient = DifferentialPressureAtClient;
            OriginalEdge.PipeSegment.IsCriticalPath = IsCriticalPath;
            OriginalEdge.PipeSegment.VelocitySupply = VelocitySupply;
            OriginalEdge.PipeSegment.VelocityReturn = VelocityReturn;
            OriginalEdge.PipeSegment.UtilizationRate = UtilizationRate;
            OriginalEdge.PipeSegment.IsBridge = IsBridge;
            OriginalEdge.PipeSegment.SubGraphId = SubGraphId;
        }

        /// <summary>
        /// Syncs base sums from another edge (used in specific scenarios).
        /// </summary>        
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
            Dim = OriginalEdge.PipeSegment.Dim;
            ReynoldsSupply = OriginalEdge.PipeSegment.ReynoldsSupply;
            ReynoldsReturn = OriginalEdge.PipeSegment.ReynoldsReturn;
            PressureGradientSupply = OriginalEdge.PipeSegment.PressureGradientSupply;
            PressureGradientReturn = OriginalEdge.PipeSegment.PressureGradientReturn;
            PressureLossAtClientSupply = OriginalEdge.PipeSegment.PressureLossAtClientSupply;
            PressureLossAtClientReturn = OriginalEdge.PipeSegment.PressureLossAtClientReturn;
            DifferentialPressureAtClient = OriginalEdge.PipeSegment.DifferentialPressureAtClient;
            IsCriticalPath = OriginalEdge.PipeSegment.IsCriticalPath;
            VelocitySupply = OriginalEdge.PipeSegment.VelocitySupply;
            VelocityReturn = OriginalEdge.PipeSegment.VelocityReturn;
            UtilizationRate = OriginalEdge.PipeSegment.UtilizationRate;
            IsBridge = OriginalEdge.PipeSegment.IsBridge;
            SubGraphId = OriginalEdge.PipeSegment.SubGraphId;
        }

        internal void ApplyResult(CalculationResultClient result)
        {
            //Write data from result to properties
            Dim = result.Dim;
            ReynoldsSupply = result.ReynoldsSupply;
            ReynoldsReturn = result.ReynoldsReturn;
            KarFlowHeatSupply = result.KarFlowHeatSupply;
            KarFlowBVSupply = result.KarFlowBVSupply;
            KarFlowHeatReturn = result.KarFlowHeatReturn;
            KarFlowBVReturn = result.KarFlowBVReturn;
            DimFlowSupply = result.DimFlowSupply;
            DimFlowReturn = result.DimFlowReturn;
            PressureGradientSupply = result.PressureGradientSupply;
            PressureGradientReturn = result.PressureGradientReturn;
            VelocitySupply = result.VelocitySupply;
            VelocityReturn = result.VelocityReturn;
            UtilizationRate = result.UtilizationRate;
        }
        internal void ApplyResult(CalculationResultFordeling result)
        {
            //Write data from result to properties
            Dim = result.Dim;
            ReynoldsSupply = result.ReynoldsSupply;
            ReynoldsReturn = result.ReynoldsReturn;
            DimFlowSupply = result.DimFlowSupply;
            DimFlowReturn = result.DimFlowReturn;
            PressureGradientSupply = result.PressureGradientSupply;
            PressureGradientReturn = result.PressureGradientReturn;
            VelocitySupply = result.VelocitySupply;
            VelocityReturn = result.VelocityReturn;
            UtilizationRate = result.UtilizationRate;
        }
    }
}