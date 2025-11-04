using System;
using System.Collections.Generic;
using System.Linq;

using IntersectUtilities.UtilsCommon.Graphs;
using IntersectUtilities.UtilsCommon.Graphs.Styling;

namespace IntersectUtilities.GraphWriteV2.DotStyling
{
    internal sealed class UniformWidthHtmlStyler : IDotStyler<GraphEntity>
    {
        private readonly int _fontSizePts;
        private readonly double _charWidthFactor;

        private int _leftChars;
        private int _rightChars;
        private int _leftWidthPts;
        private int _rightWidthPts;
        private int _tableWidthPts;

        public UniformWidthHtmlStyler(int fontSizePts = 13, double charWidthFactor = 0.6)
        {
            _fontSizePts = fontSizePts;
            _charWidthFactor = charWidthFactor;
        }

        public void BeginGraph(Graph<GraphEntity> graph)
        {
            // Compute minimal column widths (chars) across all nodes without breaking words
            int leftMax = 0;
            int rightMax = 0;

            foreach (var node in graph.Dfs())
            {
                var v = node.Value;
                // left column: handle and TypeLabel words
                int handleLen = v.OwnerHandle.ToString().Length;
                leftMax = Math.Max(leftMax, handleLen);

                var typeWords = SplitWords(v.TypeLabel);
                foreach (var w in typeWords)
                    leftMax = Math.Max(leftMax, w.Length);

                // right column: SystemLabel words and DnLabel
                var sysWords = SplitWords(v.SystemLabel);
                foreach (var w in sysWords)
                    rightMax = Math.Max(rightMax, w.Length);
                rightMax = Math.Max(rightMax, v.DnLabel.Length);
            }

            // Add a tiny margin (1 char) to each column
            _leftChars = Math.Max(1, leftMax + 1);
            _rightChars = Math.Max(1, rightMax + 1);

            // Convert chars to points using monospace approximation
            int charWidthPts = (int)Math.Round(_fontSizePts * _charWidthFactor);
            _leftWidthPts = _leftChars * charWidthPts;
            _rightWidthPts = _rightChars * charWidthPts;

            // Add minimal padding/border allowance
            int paddingPts = Math.Max(6, (int)Math.Round(_fontSizePts * 0.5));
            _leftWidthPts += paddingPts;
            _rightWidthPts += paddingPts;
            _tableWidthPts = _leftWidthPts + _rightWidthPts;
        }

        public string BuildNodeLabel(GraphEntity value)
        {
            string handle = value.OwnerHandle.ToString();
            string type = WrapOnSpaces(value.TypeLabel, _leftChars);
            string sys = value.SystemLabel;
            string dn = value.DnLabel;

            // HTML-like label with fixed column widths and table width
            string html = $"<TABLE BORDER=\"1\" CELLBORDER=\"1\" CELLSPACING=\"0\" WIDTH=\"{_tableWidthPts}\">" +
                          "<TR>" +
                          $"<TD ALIGN=\"CENTER\" WIDTH=\"{_leftWidthPts}\"><FONT FACE=\"monospace\" POINT-SIZE=\"{_fontSizePts}\"><B>{Escape(handle)}</B></FONT></TD>" +
                          $"<TD ROWSPAN=\"2\" ALIGN=\"CENTER\" WIDTH=\"{_rightWidthPts}\"><FONT FACE=\"monospace\" POINT-SIZE=\"{_fontSizePts}\"><B>{Escape(sys)}<BR/>{Escape(dn)}</B></FONT></TD>" +
                          "</TR>" +
                          "<TR>" +
                          $"<TD ALIGN=\"CENTER\"><FONT FACE=\"monospace\" POINT-SIZE=\"{_fontSizePts}\"><B>{EscapeMultiline(type)}</B></FONT></TD>" +
                          "</TR>" +
                          "</TABLE>";

            return $"label=<{html}>";
        }

        public string? BuildNodeAttrs(GraphEntity value, bool isRoot)
        {
            // Make the entire node clickable via custom protocol
            var handle = value.OwnerHandle.ToString();
            return $"URL=\"ahk://ACCOMSelectByHandle/{handle}\"";
        }

        public string? BuildEdgeAttrs(GraphEntity from, GraphEntity to)
        {
            // Neutral by default
            return null;
        }

        public string? BuildClusterAttrs(string key)
        {
            return null;
        }

        private static IEnumerable<string> SplitWords(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) yield break;
            foreach (var part in s.Split(" ", StringSplitOptions.RemoveEmptyEntries))
                yield return part;
        }

        private static string WrapOnSpaces(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text) || maxChars <= 0) return text ?? string.Empty;
            var words = SplitWords(text).ToArray();
            if (words.Length == 0) return string.Empty;

            var lines = new List<string>();
            var current = new List<string>();
            int currentLen = 0;

            foreach (var w in words)
            {
                if (current.Count == 0)
                {
                    current.Add(w);
                    currentLen = w.Length;
                }
                else if (currentLen + 1 + w.Length <= maxChars)
                {
                    current.Add(w);
                    currentLen += 1 + w.Length;
                }
                else
                {
                    lines.Add(string.Join(" ", current));
                    current.Clear();
                    current.Add(w);
                    currentLen = w.Length;
                }
            }
            if (current.Count > 0) lines.Add(string.Join(" ", current));

            return string.Join("<BR/>", lines);
        }

        private static string Escape(string s)
        {
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
        private static string EscapeMultiline(string s)
        {
            return string.Join("<BR/>", s.Split(new[] { "<BR/>" }, StringSplitOptions.None).Select(Escape));
        }
    }
}

