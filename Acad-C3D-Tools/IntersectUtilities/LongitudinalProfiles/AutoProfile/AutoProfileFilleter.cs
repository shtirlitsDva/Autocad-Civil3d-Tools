using System;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    public class AutoProfileFilleter
    {        
        private readonly IFilletRadiusProvider _radiusProvider;
        private readonly IPolylineBuilder _polylineBuilder;
        private readonly ISegmentExtractor _segmentExtractor;
        private readonly FilletStrategyManager _strategyManager;
        
        public AutoProfileFilleter(
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

        public static AutoProfileFilleter CreateDefault(Func<Point2d, double> radiusCallback)
        {                     
            var radiusProvider = new RadiusProvider(radiusCallback);            
            var polylineBuilder = new PolylineBuilder();
            var segmentExtractor = new SegmentExtractor();
            return new AutoProfileFilleter(radiusProvider, polylineBuilder, segmentExtractor);
        }

        public Polyline PerformFilleting(Polyline polyline)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline));            

            try
            {
                

                

                for (int i = 0; i < segmentList.Count - 1; i++)
                {
                    var segment1 = segmentList[i];
                    var segment2 = segmentList[i + 1];

                    double radius = _radiusProvider.GetRadiusAtPoint(segment1.EndPoint);

                    var strategy = _strategyManager.GetStrategy(segment1, segment2);
                    if (strategy == null)
                    {
                        if (i == 0) resultSegments.Add(segment1);
                        resultSegments.Add(segment2);
                        continue;
                    }

                    var filletResult = strategy.CreateFillet(segment1, segment2, radius);
                    
                    if (filletResult.Success && filletResult.TrimmedSegment1 != null && 
                        filletResult.FilletSegment != null && filletResult.TrimmedSegment2 != null)
                    {
                        if (i == 0) resultSegments.Add(filletResult.TrimmedSegment1);
                        resultSegments.Add(filletResult.FilletSegment);
                        
                        if (i < segmentList.Count - 2)
                        {
                            segmentList[i + 1] = filletResult.TrimmedSegment2;
                        }
                        else
                        {
                            resultSegments.Add(filletResult.TrimmedSegment2);
                        }
                    }
                    else
                    {
                        if (i == 0) resultSegments.Add(segment1);
                        resultSegments.Add(segment2);
                    }
                }                

                return _polylineBuilder.BuildPolyline(resultSegments);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Filleting operation failed: {ex.Message}", ex);
            }
        }
    }
}
