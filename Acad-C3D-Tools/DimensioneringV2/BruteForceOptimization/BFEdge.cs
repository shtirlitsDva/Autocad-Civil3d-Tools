using System;
using DimensioneringV2.GraphFeatures;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using QuikGraph;

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace DimensioneringV2.BruteForceOptimization
{
    /// <summary>
    /// WARNING! When adding new properties that read from OriginalEdge.PipeSegment,
    /// remember to cache the value in the constructor instead of accessing
    /// OriginalEdge.PipeSegment directly in the property getter.
    /// </summary>
    internal sealed class BFEdge : Edge<BFNode>, IHydraulicSegment
    {
#if DEBUG
        static BFEdge()
        {
            var syncProps = typeof(BFEdge).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<SyncPropertyAttribute>() != null)
                .Select(p => p.Name)
                .ToHashSet();
            var afWritableProps = typeof(AnalysisFeature)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .Select(p => p.Name)
                .ToHashSet();
            var missing = syncProps.Except(afWritableProps).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    $"BFEdge [SyncProperty] properties missing from AnalysisFeature: {string.Join(", ", missing)}");
        }
#endif

        // Cached values from OriginalEdge.PipeSegment
        private readonly bool _isRootNode;
        private readonly double _length;
        private readonly int _numberOfBuildingsConnected;
        private readonly int _numberOfUnitsConnected;
        private readonly double _heatingDemandConnected;
        private readonly bool _manualDim;
        private readonly double _tempDeltaVarme;
        private readonly double _tempDeltaBV;
        private readonly string _anvKode;

        public int Id { get; set; }
        public double Price { get => Dim.Price_m * Length + Dim.Price_stk_calc(SegmentType); }
        public bool IsRootNode { get => _isRootNode; }
        [SyncProperty] public bool IsBridge { get; set; }
        [SyncProperty] public int SubGraphId { get; set; }
        public double Length { get => _length; }
        public int NumberOfBuildingsConnected { get => _numberOfBuildingsConnected; }
        public SegmentType SegmentType =>
            NumberOfBuildingsConnected == 1 ?
            SegmentType.Stikledning :
            SegmentType.Fordelingsledning;
        public int NumberOfUnitsConnected { get => _numberOfUnitsConnected; }
        public double HeatingDemandConnected { get => _heatingDemandConnected; }
        [SyncProperty] public int NumberOfBuildingsSupplied { get; set; }
        [SyncProperty] public int NumberOfUnitsSupplied { get; set; }
        [SyncProperty] public double HeatingDemandSupplied { get; set; }
        [SyncProperty] public Dim Dim { get; set; }
        public bool ManualDim { get => _manualDim; }
        [SyncProperty] public double ReynoldsSupply { get; set; }
        [SyncProperty] public double ReynoldsReturn { get; set; }
        [SyncProperty] public double KarFlowHeatSupply { get; set; }
        [SyncProperty] public double KarFlowBVSupply { get; set; }
        [SyncProperty] public double KarFlowHeatReturn { get; set; }
        [SyncProperty] public double KarFlowBVReturn { get; set; }
        [SyncProperty] public double DimFlowSupply { get; set; }
        [SyncProperty] public double DimFlowReturn { get; set; }
        [SyncProperty] public double PressureGradientSupply { get; set; }
        [SyncProperty] public double PressureGradientReturn { get; set; }
        [SyncProperty] public double PressureLossAtClientSupply { get; set; }
        [SyncProperty] public double PressureLossAtClientReturn { get; set; }
        [SyncProperty] public double DifferentialPressureAtClient { get; set; }
        [SyncProperty] public bool IsCriticalPath { get; set; } = false;
        [SyncProperty] public double VelocitySupply { get; set; }
        [SyncProperty] public double VelocityReturn { get; set; }
        [SyncProperty] public double UtilizationRate { get; set; }
        [SyncProperty] public double Effekt { get; set; }
        public EdgePipeSegment OriginalEdge { get; }
        public int NonBridgeChromosomeIndex { get; internal set; } = -1;
        public double TempDeltaVarme => _tempDeltaVarme;
        public double TempDeltaBV => _tempDeltaBV;
        public string AnvendelseKode => _anvKode;
        public int Nyttetimer { get => OriginalEdge.PipeSegment.Nyttetimer; }
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
            _anvKode = edge.PipeSegment.BygningsAnvendelseNyKode;
        }
        // Sync checklist for new result properties:
        // 1. Add [SyncProperty] attribute to the property declaration
        // 2. Add to PushAllResults() and YankAllResults()
        // 3. Add matching property to AnalysisFeature (plain bag style)
        // 4. Add to AnalysisFeature.ResetHydraulicResults()
        // 5. Copy in BOTH copy constructors below
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
            DimFlowSupply = edge.DimFlowSupply;
            DimFlowReturn = edge.DimFlowReturn;
            ReynoldsSupply = edge.ReynoldsSupply;
            ReynoldsReturn = edge.ReynoldsReturn;
            PressureGradientSupply = edge.PressureGradientSupply;
            PressureGradientReturn = edge.PressureGradientReturn;
            VelocitySupply = edge.VelocitySupply;
            VelocityReturn = edge.VelocityReturn;
            UtilizationRate = edge.UtilizationRate;
            Effekt = edge.Effekt;
            IsBridge = edge.IsBridge;
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
            _anvKode = edge.AnvendelseKode;
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
            DimFlowSupply = edge.DimFlowSupply;
            DimFlowReturn = edge.DimFlowReturn;
            ReynoldsSupply = edge.ReynoldsSupply;
            ReynoldsReturn = edge.ReynoldsReturn;
            PressureGradientSupply = edge.PressureGradientSupply;
            PressureGradientReturn = edge.PressureGradientReturn;
            VelocitySupply = edge.VelocitySupply;
            VelocityReturn = edge.VelocityReturn;
            UtilizationRate = edge.UtilizationRate;
            Effekt = edge.Effekt;
            IsBridge = edge.IsBridge;
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
            _anvKode = edge.AnvendelseKode;
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
            OriginalEdge.PipeSegment.KarFlowHeatSupply = KarFlowHeatSupply;
            OriginalEdge.PipeSegment.KarFlowBVSupply = KarFlowBVSupply;
            OriginalEdge.PipeSegment.KarFlowHeatReturn = KarFlowHeatReturn;
            OriginalEdge.PipeSegment.KarFlowBVReturn = KarFlowBVReturn;
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
            OriginalEdge.PipeSegment.Effekt = Effekt;
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
            DimFlowSupply = OriginalEdge.PipeSegment.DimFlowSupply;
            DimFlowReturn = OriginalEdge.PipeSegment.DimFlowReturn;
            Dim = OriginalEdge.PipeSegment.Dim;
            ReynoldsSupply = OriginalEdge.PipeSegment.ReynoldsSupply;
            ReynoldsReturn = OriginalEdge.PipeSegment.ReynoldsReturn;
            KarFlowHeatSupply = OriginalEdge.PipeSegment.KarFlowHeatSupply;
            KarFlowBVSupply = OriginalEdge.PipeSegment.KarFlowBVSupply;
            KarFlowHeatReturn = OriginalEdge.PipeSegment.KarFlowHeatReturn;
            KarFlowBVReturn = OriginalEdge.PipeSegment.KarFlowBVReturn;
            PressureGradientSupply = OriginalEdge.PipeSegment.PressureGradientSupply;
            PressureGradientReturn = OriginalEdge.PipeSegment.PressureGradientReturn;
            PressureLossAtClientSupply = OriginalEdge.PipeSegment.PressureLossAtClientSupply;
            PressureLossAtClientReturn = OriginalEdge.PipeSegment.PressureLossAtClientReturn;
            DifferentialPressureAtClient = OriginalEdge.PipeSegment.DifferentialPressureAtClient;
            IsCriticalPath = OriginalEdge.PipeSegment.IsCriticalPath;
            VelocitySupply = OriginalEdge.PipeSegment.VelocitySupply;
            VelocityReturn = OriginalEdge.PipeSegment.VelocityReturn;
            UtilizationRate = OriginalEdge.PipeSegment.UtilizationRate;
            Effekt = OriginalEdge.PipeSegment.Effekt;
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
            Effekt = result.Effekt;
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