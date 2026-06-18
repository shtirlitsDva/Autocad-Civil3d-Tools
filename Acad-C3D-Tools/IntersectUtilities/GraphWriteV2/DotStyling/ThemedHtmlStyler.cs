using System;

using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.GraphWriteV2.Theming;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;
using IntersectUtilities.UtilsCommon.Graphs.Styling;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities.GraphWriteV2.DotStyling
{
    /// <summary>
    /// Drives each node's Graphviz label from a persisted <see cref="LabelTheme"/>. Resolves the three
    /// label fields and the Series number from a <see cref="GraphEntity"/> and hands them to the shared
    /// <see cref="LabelMarkupBuilder"/> (which the designer preview also uses). Two export-only steps the
    /// preview does not need:
    ///   • Uniform width — <see cref="BeginGraph"/> measures the widest label in the graph and every
    ///     label is stamped with that minimum width, so a cluster renders as equal-width boxes.
    ///   • Viewer color pre-compensation — the GRAPHWRITEV2 HTML viewer inverts the whole SVG; the label
    ///     colors are pre-inverted (<see cref="ViewerColorInvert"/>) so they show as the user picked them.
    ///
    /// Field mapping:
    ///   id   = OwnerHandle           (top-left, carries the Series chip)
    ///   type = SystemLabel + DnLabel (right column, wrapped to two lines)
    ///   desc = TypeLabel             (bottom-left)
    /// </summary>
    internal sealed class ThemedHtmlStyler : IDotStyler<GraphEntity>
    {
        private readonly LabelMarkupBuilder _markup;
        private int _groupWidth;

        public ThemedHtmlStyler(LabelTheme theme) => _markup = new LabelMarkupBuilder(theme);

        public void BeginGraph(Graph<GraphEntity> graph)
        {
            // Per-group uniform width: the widest label's natural width becomes the minimum for all.
            int max = 0;
            foreach (var node in graph.Dfs())
            {
                var (id, type, desc, series) = Fields(node.Value);
                max = Math.Max(max, _markup.EstimateWidthPts(id, type, desc, series));
            }
            _groupWidth = max;
        }

        public string BuildNodeLabel(GraphEntity value)
        {
            var (id, type, desc, series) = Fields(value);
            string markup = _markup.Build(id, type, desc, series, _groupWidth);
            markup = ViewerColorInvert.InvertMarkupColors(markup);
            return $"label=<{markup}>";
        }

        public string? BuildNodeAttrs(GraphEntity value, bool isRoot)
            => $"URL=\"ahk://ACCOMSelectByHandle/{value.OwnerHandle}\", margin=\"0,0\"";

        public string? BuildEdgeAttrs(GraphEntity from, GraphEntity to)
            => "dir=none, penwidth=2.5, headclip=true, tailclip=true";

        public string? BuildClusterAttrs(string key) => null;

        private static (string id, string type, string desc, int series) Fields(GraphEntity value)
        {
            string id = value.OwnerHandle.ToString();
            string type = $"{value.SystemLabel} {value.DnLabel}".Trim();
            string desc = value.TypeLabel;
            return (id, type, desc, ResolveSeries(value));
        }

        // Series is a real district-heating property (insulation series S1/S2/S3). Pipes carry it on
        // their KOd (GetPipeSeriesV2); components carry it as the dynamic-block "Serie" CSV property.
        // Undefined -> 0 -> no chip (a missing series is the absence of a chip, not a fallback value).
        private static int ResolveSeries(GraphEntity value)
        {
            PipeSeriesEnum s = value.Owner switch
            {
                Polyline pl => GetPipeSeriesV2(pl),
                BlockReference br => br.GetPipeSeriesEnum(),
                _ => PipeSeriesEnum.Undefined,
            };
            return s switch
            {
                PipeSeriesEnum.S1 => 1,
                PipeSeriesEnum.S2 => 2,
                PipeSeriesEnum.S3 => 3,
                _ => 0,
            };
        }
    }
}
