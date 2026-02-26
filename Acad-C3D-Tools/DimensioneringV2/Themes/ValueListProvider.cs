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
        public static List<object> GetValues(
            PropertyMeta meta,
            IEnumerable<AnalysisFeature> features,
            Func<AnalysisFeature, object?> selector,
            object[] basicStyleValues)
        {
            if (features == null) throw new ArgumentNullException(nameof(features));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            if (basicStyleValues == null) throw new ArgumentNullException(nameof(basicStyleValues));

            var basicSet = new HashSet<object>(basicStyleValues, ObjectKeyComparer.Instance);

            // When DisplayValuePath is set, the property is a complex object (e.g., Dim).
            // We may need custom ordering and null-checking.
            if (!string.IsNullOrEmpty(meta.DisplayValuePath))
            {
                return GetValuesWithDisplayPath(meta, features, selector, basicSet);
            }

            // Default path: simple distinct + natural ordering
            return features
                .Select(selector)
                .Where(x => x != null)
                .Distinct()
                .Where(x => !basicSet.Contains(x!))
                .OrderBy(x => x)
                .ToList()!;
        }

        private static List<object> GetValuesWithDisplayPath(
            PropertyMeta meta,
            IEnumerable<AnalysisFeature> features,
            Func<AnalysisFeature, object?> selector,
            HashSet<object> basicSet)
        {
            bool hasOrderingPath = !string.IsNullOrEmpty(meta.OrderingPropertyPath);

            var items = features
                .Select(f => (
                    resolved: selector(f),
                    ordering: hasOrderingPath ? meta.GetOrderingValue(f) : null,
                    feature: f))
                .ToList();

            // Check for null resolved values (like null Dim) and mark them for debugging
            var nullItems = items.Where(x => x.resolved == null).ToList();
            if (nullItems.Count > 0)
            {
                MarkNullEdges.Mark(nullItems.Select(x => x.feature));
            }

            var query = items
                .Where(x => x.resolved != null)
                .DistinctBy(x => x.resolved)
                .Where(x => !basicSet.Contains(x.resolved!));

            if (hasOrderingPath)
            {
                query = query
                    .OrderBy(x => x.ordering)
                    .ThenBy(x => x.resolved);
            }
            else
            {
                query = query.OrderBy(x => x.resolved);
            }

            return query
                .Select(x => x.resolved!)
                .ToList();
        }
    }
}
