using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Themes
{
    class ValueListProvider
    {
        public static List<T> GetValues<T>(
            MapPropertyEnum property,
            IEnumerable<AnalysisFeature> features,
            Func<AnalysisFeature, T> selector,
            T[] basicStyleValues)
        {
            if (features == null) throw new ArgumentNullException(nameof(features));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            if (basicStyleValues == null) throw new ArgumentNullException(nameof(basicStyleValues));

            return property switch
            {
                MapPropertyEnum.Pipe =>
                    (basicStyleValues as string[] is { } temp)
                        ? features
                            .DistinctBy(x => x.PipeDim.DimName)
                            .Where(x => !temp.Contains(x.PipeDim.DimName))
                            .OrderBy(x => x.PipeDim.OrderingPriority)
                            .ThenBy(x => x.PipeDim.DimName)
                            .Select(selector)
                            .ToList()
                        : throw new ArgumentException("Basic style values must be of type string[] for Pipe property."),

                _ => features
                    .Select(selector)
                    .Distinct()
                    .Where(x => !basicStyleValues.Contains(x))
                    .OrderBy(x => x)
                    .ToList()
            };
        }
    }
}