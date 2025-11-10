using System;

using IntersectUtilities.UtilsCommon.Graphs;
using IntersectUtilities.UtilsCommon.Graphs.Styling;

namespace IntersectUtilities.GraphWriteV2.DotStyling
{
    // Optional basic styler that keeps record labels; provided for completeness.
    internal sealed class RecordStyler : IDotStyler<GraphEntity>
    {
        public void BeginGraph(Graph<GraphEntity> graph) { }

        public string BuildNodeLabel(GraphEntity value)
        {
            // Keep existing label structure (record-style)
            var label = $"\"{{{value.OwnerHandle}|{value.TypeLabel}}}|{value.SystemLabel}\\n{value.DnLabel}\"";
            return $"label={label}";
        }

        public string? BuildNodeAttrs(GraphEntity value, bool isRoot) => null;
        public string? BuildEdgeAttrs(GraphEntity from, GraphEntity to) => null;
        public string? BuildClusterAttrs(string key) => null;
    }
}

