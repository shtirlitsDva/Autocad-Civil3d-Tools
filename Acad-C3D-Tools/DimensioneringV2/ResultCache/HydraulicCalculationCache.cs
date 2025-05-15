using DimensioneringV2.GraphFeatures;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.ResultCache
{
    public sealed class HydraulicCalculationCache
    {
        private readonly Dictionary<IHydraulicSegment, CalculationResult> _stikCache = new();
        private readonly ConcurrentDictionary<SegmentKey, CalculationResult> _cache = new();
        private readonly Func<IHydraulicSegment, CalculationResult> _calcFunc;
        private readonly int _precisionFactor;
        private readonly bool _cacheEnabled = false;

        public HydraulicCalculationCache(
            Func<IHydraulicSegment, CalculationResult> calculationFunc,
            bool cacheEnabled,
            int demandPrecision = 4)
        {
            _calcFunc = calculationFunc ?? throw new ArgumentNullException(nameof(calculationFunc));
            _precisionFactor = (int)Math.Pow(10, demandPrecision);
            _cacheEnabled = cacheEnabled;
        }

        public void PrecalculateServicePipes(IEnumerable<IHydraulicSegment> segments)
        {
            foreach (var segment in segments)
            {
                if (!_stikCache.ContainsKey(segment))
                {
                    if (segment is not AnalysisFeature feature) continue;
                    feature.NumberOfBuildingsSupplied = 1;
                    feature.NumberOfUnitsSupplied = feature.NumberOfUnitsConnected;
                    feature.HeatingDemandSupplied = feature.HeatingDemandConnected;

                    var result = _calcFunc(segment);
                    _stikCache.Add(segment, result);
                }
            }
        }

        /// <summary>
        /// Must operate on the original AnalysisFeature!
        /// </summary>
        public CalculationResult GetServicePipeResult(IHydraulicSegment originalAnalysisFeature)
        {
            ArgumentNullException.ThrowIfNull(originalAnalysisFeature);
            if (originalAnalysisFeature.SegmentType == SegmentType.Stikledning)
            {
                if (_stikCache.TryGetValue(originalAnalysisFeature, out var result)) return result;
                else throw new Exception("Stikledning not found in cache!!!");
            }
            else
            {
                throw new Exception("Segment is not a service pipe!!!");
            }
        }
        public CalculationResult GetOrCalculateSupplyPipeResult(IHydraulicSegment segment)
        {
            if (segment is null) throw new ArgumentNullException(nameof(segment));

            if (segment.NumberOfBuildingsSupplied == 0 &&
                segment.HeatingDemandSupplied == 0)
                return new CalculationResult(
                    "Fordelingsledning", Dim.NA, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            if (!_cacheEnabled) return _calcFunc(segment);

            var key = new SegmentKey(segment, _precisionFactor);
            return _cache.GetOrAdd(key, _ => _calcFunc(segment));
        }

        public int CachedCount => _cache.Count;

        private readonly struct SegmentKey : IEquatable<SegmentKey>
        {
            public int NumberOfBuildings { get; }
            public int NumberOfUnits { get; }
            public int ScaledHeatingDemand { get; }

            public SegmentKey(IHydraulicSegment segment, int precisionFactor)
            {
                NumberOfBuildings = segment.NumberOfBuildingsSupplied;
                NumberOfUnits = segment.NumberOfUnitsSupplied;
                ScaledHeatingDemand = (int)Math.Round(
                    segment.HeatingDemandSupplied * precisionFactor, MidpointRounding.AwayFromZero);
            }

            public bool Equals(SegmentKey other) =>
                NumberOfBuildings == other.NumberOfBuildings &&
                NumberOfUnits == other.NumberOfUnits &&
                ScaledHeatingDemand == other.ScaledHeatingDemand;

            public override bool Equals(object obj) => obj is SegmentKey other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(NumberOfBuildings, NumberOfUnits, ScaledHeatingDemand);
        }
    }
}