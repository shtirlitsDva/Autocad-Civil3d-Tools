using System;
using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon.Graphs.Styling
{
    public interface IDotStyler<T>
    {
        void BeginGraph(Graph<T> graph);
        string BuildNodeLabel(T value);
        string? BuildNodeAttrs(T value, bool isRoot);
        string? BuildEdgeAttrs(T from, T to);
        string? BuildClusterAttrs(string key);
    }
}
