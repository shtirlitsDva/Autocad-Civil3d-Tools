using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal class AutoProfileFilleter
    {
        private readonly IFilletRadiusProvider _radiusProvider;
        private readonly IPolylineBuilder _polylineBuilder;
        private readonly ISegmentExtractor _segmentExtractor;
        private readonly FilletStrategyManager _strategyManager;

        internal AutoProfileFilleter(
            IFilletRadiusProvider radiusProvider,
            IPolylineBuilder polylineBuilder,
            ISegmentExtractor segmentExtractor,
            FilletStrategyManager? strategyManager = null)
        {
            _radiusProvider = radiusProvider ?? throw new ArgumentNullException(nameof(radiusProvider));
            _polylineBuilder = polylineBuilder ?? throw new ArgumentNullException(nameof(polylineBuilder));
            _segmentExtractor = segmentExtractor ?? throw new ArgumentNullException(nameof(segmentExtractor));
            _strategyManager = strategyManager ?? new FilletStrategyManager();
        }

        internal static AutoProfileFilleter CreateDefault(Func<Point2d, double> radiusCallback)
        {
            var radiusProvider = new RadiusProvider(radiusCallback);
            var polylineBuilder = new PolylineBuilder();
            var segmentExtractor = new SegmentExtractor();
            return new AutoProfileFilleter(radiusProvider, polylineBuilder, segmentExtractor);
        }

        internal Polyline PerformFilleting(Polyline polyline)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline));

            try
            {
                var segments = _segmentExtractor.ExtractSegments(polyline);

                //Remove very short segments from the segments
                //As these cause autocad internal stuff to fail
                double threshold = 0.01;
                var lquery = () => segments.Any(x => x.Length < threshold);
                while (lquery.Invoke())
                    PolylineSanitizer.PruneShortSegments(segments, threshold);

                //Begin the filleting procedure
                var skipped = new HashSet<VertexKey>();
                int safetyCounter = 0;
                while (segments.TryGetFilletCandidate(
                        skipped, _strategyManager, out var nodes))
                {
                    if (safetyCounter++ > 300) break;
                        //throw new InvalidOperationException(
                        //    "Safety limit exceeded while filleting segments. " +
                        //    "Possible infinite loop detected.");

                    var seg1 = nodes.firstNode.Value;
                    var seg2 = nodes.secondNode.Value;
                    var strategy = _strategyManager.GetStrategy(seg1, seg2);
                    if (strategy == null)
                    {
                        skipped.Add(VertexKey.From(seg1.EndPoint));
                        continue;
                    }

#if DEBUG
                    //prdDbg($"Strategy: {strategy}");
#endif

                    IFilletResult result = strategy.CreateFillet(
                        seg1, seg2, _radiusProvider.GetRadius(seg1.EndPoint));

                    if (result.Success)
                    {
                        result.UpdateWithResults(segments, nodes);                        
                    }
                    else
                    {
                        skipped.Add(VertexKey.From(seg1.EndPoint));
                    }
                }

                return _polylineBuilder.BuildPolyline(segments);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Filleting operation failed: {ex.Message}", ex);
            }
        }
    }
}