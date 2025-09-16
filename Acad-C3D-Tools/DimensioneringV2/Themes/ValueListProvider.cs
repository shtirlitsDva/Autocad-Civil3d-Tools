using DimensioneringV2.AutoCAD;
using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

using System;
using System.Collections.Generic;
using System.Linq;

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

            switch (property)
            {
                case MapPropertyEnum.Pipe:
                    {
                        if (basicStyleValues is not string[] temp)
                            throw new ArgumentException(
                                "Basic style values must be of type string[] for Pipe property.");
                        
                        var query = features
                            .DistinctBy(x => x.Dim.DimName)
                            .Where(x => !temp.Contains(x.Dim.DimName))                        
                            .OrderBy(x => x.Dim.OrderingPriority)
                            .ThenBy(x => x.Dim.DimName)
                            .Select(selector)
                            .ToList();

                        if (query.Count != 0 && query.Any(x => x == null))
                        {
                            var nulls = features.Where(x => selector(x) == null).ToList();
                            MarkNullEdges.Mark(nulls);
                        }

                        return query;
                    }

                default:
                    return features
                        .Select(selector)
                        .Distinct()
                        .Where(x => !basicStyleValues.Contains(x))
                        .OrderBy(x => x)
                        .ToList();
            }
        }
    }
}