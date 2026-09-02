using System.Collections.Generic;

namespace AcadOverrules.VertexCircles
{
    /// <summary>
    /// Matches layer names against the user's filter list.
    /// </summary>
    internal static class LayerFilterMatcher
    {
        /// <summary>
        /// True when <paramref name="layerName"/> matches any of <paramref name="filters"/>,
        /// or when the filter list is empty, which means "every layer".
        /// Matching uses AutoCAD's own wildcard engine so the masks behave exactly like the
        /// ones the user is used to from the layer manager (* ? # @ ~ [] etc).
        /// </summary>
        public static bool Matches(IReadOnlyList<string> filters, string layerName)
        {
            if (filters == null || filters.Count == 0) return true;
            if (string.IsNullOrEmpty(layerName)) return false;

            for (int i = 0; i < filters.Count; i++)
            {
                string filter = filters[i];
                if (string.IsNullOrWhiteSpace(filter)) continue;

                if (Autodesk.AutoCAD.Internal.Utils.WcMatchEx(layerName, filter.Trim(), true))
                    return true;
            }

            return false;
        }
    }
}
