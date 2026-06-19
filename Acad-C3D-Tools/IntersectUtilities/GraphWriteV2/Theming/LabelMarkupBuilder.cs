using System;

namespace IntersectUtilities.GraphWriteV2.Theming;

/// <summary>
/// The single source of truth for a node's Graphviz HTML-like-label markup, ported from the design
/// package's <c>label()</c> / <c>idCell()</c> / <c>chipCell()</c> blueprint. Pure and free of any
/// AutoCAD dependency so the live designer preview and the real DOT export call exactly the same code —
/// what the user sees in the preview is byte-for-byte what GRAPHWRITEV2 emits.
///
/// <para>Inputs are the three label fields plus a Series number (1/2/3, or 0 for "no chip"):</para>
/// <list type="bullet">
///   <item><c>id</c>   — top-left handle; the Series plaque shares this row</item>
///   <item><c>type</c> — right column, wrapped to two lines at the last space</item>
///   <item><c>desc</c> — bottom-left, wrapped at the first comma so long descriptions don't balloon</item>
/// </list>
/// Text values are escaped here, so callers pass raw strings.
///
/// <para>The Series plaque sits on the id row in its own fixed-width cell (so I / II / III are one
/// size). When a group width is supplied the id cell is width-locked so the fixed plaque lands on a
/// single right-hand vertical edge across the whole group; with no group width (the designer preview)
/// the plaque simply follows the handle inline.</para>
///
/// <para><c>minWidth</c> stamps a minimum table width and <c>textWidth</c> a minimum text-column width
/// (both points). Graphviz sizes each label to its own content, so to make every label in a cluster the
/// same width — and every plaque land on one edge — the styler measures the group's column maxima
/// (<see cref="EstimateLeftPts"/> / <see cref="EstimateRightPts"/>), folds them via
/// <see cref="GroupWidths"/>, and passes the results here. The preview passes 0/0 (natural size).</para>
/// </summary>
public sealed class LabelMarkupBuilder
{
    private static readonly string[] Roman = { "", "I", "II", "III" };

    // Approximate average glyph width as a fraction of point size. Used only to equalize widths within
    // a group, exactly as the prior styler did — never for the chip, which is a fixed-width cell.
    private const double CharWidthFactor = 0.62;

    // Fixed inner width (points) of the Serie plaque, so I / II / III are one size (wide enough for "III"),
    // and the rendered width of its cell including its own padding — used to width-lock the id cell.
    private const int SeriePlaqueWidth = 30;
    private const int SerieBay = 38;

    private readonly LabelTheme _t;

    public LabelMarkupBuilder(LabelTheme theme) => _t = theme;

    /// <summary>Returns the <c>&lt;TABLE&gt;…&lt;/TABLE&gt;</c> markup (without the <c>label=&lt;&gt;</c> wrapper).</summary>
    public string Build(string id, string type, string desc, int series, int textWidth = 0, int typeWidth = 0)
    {
        id = Escape(id);
        type = Escape(type);
        string descTwo = DescWrap(Escape(desc));

        var c = _t.Colors;
        int p = _t.Padding;
        string f = _t.Fonts.Id; // single monospace face used for every text in the label
        // Lock both columns to the group's per-column maxima: tw on the desc cell (the text column) and
        // rw on the type cell. With both columns fixed, every box in the group is the same total width
        // by construction — no outer table WIDTH, so Graphviz never pads the surplus with a (in bordered
        // styles, visible) filler column.
        string tw = textWidth > 0 ? $" WIDTH=\"{textWidth}\"" : "";
        string rw = typeWidth > 0 ? $" WIDTH=\"{typeWidth}\"" : "";
        string typeTwo = TwoLine(type);

        string IdF(string color, string txt) =>
            $"<FONT FACE=\"{f}\" POINT-SIZE=\"13\" COLOR=\"{color}\"><B>{txt}</B></FONT>";

        switch (_t.Style)
        {
            case LabelStyle.Glass:
                return $@"<TABLE BORDER=""1"" COLOR=""{c.Frame}"" CELLBORDER=""0"" CELLSPACING=""0"" CELLPADDING=""0"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""125"">
<TR><TD ALIGN=""LEFT"" CELLPADDING=""{p}"" SIDES=""B"" BORDER=""1"" COLOR=""{c.Divider}"">{IdRow(IdF(c.Id, id), series, textWidth)}</TD>
<TD ROWSPAN=""2""{rw} ALIGN=""LEFT"" CELLPADDING=""{p + 2}"" SIDES=""L"" BORDER=""1"" COLOR=""{c.Divider}""><FONT FACE=""{f}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD{tw} ALIGN=""LEFT"" CELLPADDING=""{p}""><FONT FACE=""{f}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{descTwo}</FONT></TD></TR>
</TABLE>";

            case LabelStyle.Blueprint:
                return $@"<TABLE BORDER=""2"" COLOR=""{c.Frame}"" CELLBORDER=""1"" CELLSPACING=""0"" CELLPADDING=""{p}"" BGCOLOR=""{c.Background}"">
<TR><TD ALIGN=""LEFT"" COLOR=""{c.Divider}"">{IdRow(IdF(c.Id, id), series, textWidth)}</TD>
<TD ROWSPAN=""2""{rw} ALIGN=""LEFT"" COLOR=""{c.Divider}""><FONT FACE=""{f}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD{tw} ALIGN=""LEFT"" COLOR=""{c.Divider}""><FONT FACE=""{f}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{descTwo}</FONT></TD></TR>
</TABLE>";

            case LabelStyle.Segmented:
                return Segmented(id, typeTwo, descTwo, series, rw, tw, textWidth);

            case LabelStyle.Fluent:
            {
                string idInner =
                    $"<FONT FACE=\"{f}\" POINT-SIZE=\"10\" COLOR=\"{c.Id}\"><B>&#9642; </B></FONT>" +
                    $"<FONT FACE=\"{f}\" POINT-SIZE=\"13\" COLOR=\"#f3f6fa\"><B>{id}</B></FONT>";
                return $@"<TABLE BORDER=""1"" COLOR=""{c.Frame}"" CELLBORDER=""0"" CELLSPACING=""0"" CELLPADDING=""{p + 2}"" BGCOLOR=""{c.FillLite1}:{c.FillLite2}"" GRADIENTANGLE=""135"">
<TR><TD ALIGN=""LEFT"" SIDES=""B"" BORDER=""1"" COLOR=""{c.Divider}"">{IdRow(idInner, series, textWidth)}</TD>
<TD ROWSPAN=""2""{rw} ALIGN=""LEFT"" SIDES=""L"" BORDER=""1"" COLOR=""{c.Divider}""><FONT FACE=""{f}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD{tw} ALIGN=""LEFT""><FONT FACE=""{f}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{descTwo}</FONT></TD></TR>
</TABLE>";
            }

            case LabelStyle.Terminal:
            default:
                return $@"<TABLE BORDER=""2"" COLOR=""{c.Frame}"" CELLBORDER=""0"" CELLSPACING=""0"" CELLPADDING=""{p}"" BGCOLOR=""{c.Background}"">
<TR><TD ALIGN=""LEFT"" SIDES=""B"" BORDER=""1"" COLOR=""{c.Divider}"">{IdRow(IdF(c.Id, "[" + id + "]"), series, textWidth)}</TD>
<TD ROWSPAN=""2""{rw} ALIGN=""LEFT"" SIDES=""L"" BORDER=""1"" COLOR=""{c.Divider}""><FONT FACE=""{f}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD{tw} ALIGN=""LEFT""><FONT FACE=""{f}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{descTwo}</FONT></TD></TR>
</TABLE>";
        }
    }

    private string Segmented(string id, string typeTwo, string descTwo, int series, string rw, string tw, int textWidth)
    {
        var c = _t.Colors;
        int p = _t.Padding;
        string idFace = _t.Fonts.Id;

        string IdF(string color, string txt) =>
            $"<FONT FACE=\"{idFace}\" POINT-SIZE=\"13\" COLOR=\"{color}\"><B>{txt}</B></FONT>";

        switch (_t.TileMode)
        {
            case SegmentMode.Pop:
                return $@"<TABLE BORDER=""0"" CELLBORDER=""0"" CELLSPACING=""6"" CELLPADDING=""{p}"" BGCOLOR=""#050608"">
<TR><TD ALIGN=""LEFT"" BGCOLOR=""{c.TileA1}:{c.TileA2}"" GRADIENTANGLE=""120"">{IdRow(IdF(c.IdInk, id), series, textWidth)}</TD>
<TD ROWSPAN=""2""{rw} ALIGN=""LEFT"" BGCOLOR=""{c.TileA2}:{c.TileA1}"" GRADIENTANGLE=""120""><FONT FACE=""{idFace}"" POINT-SIZE=""11"" COLOR=""{c.Ink}"">{typeTwo}</FONT></TD></TR>
<TR><TD{tw} ALIGN=""LEFT"" BGCOLOR=""{c.TileA1}:{c.TileA2}"" GRADIENTANGLE=""120""><FONT FACE=""{idFace}"" POINT-SIZE=""12"" COLOR=""{c.Ink}""><B>{descTwo}</B></FONT></TD></TR>
</TABLE>";

            case SegmentMode.Bordered:
                return $@"<TABLE BORDER=""0"" CELLBORDER=""0"" CELLSPACING=""5"" CELLPADDING=""{p}"" BGCOLOR=""{c.Background}"">
<TR><TD ALIGN=""LEFT"" BORDER=""1"" COLOR=""{c.Frame}"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""120"">{IdRow(IdF(c.Id, id), series, textWidth)}</TD>
<TD ROWSPAN=""2""{rw} ALIGN=""LEFT"" BORDER=""1"" COLOR=""{c.Frame}"" BGCOLOR=""{c.Fill2}:{c.Background}"" GRADIENTANGLE=""120""><FONT FACE=""{idFace}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD{tw} ALIGN=""LEFT"" BORDER=""1"" COLOR=""{c.Frame}"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""120""><FONT FACE=""{idFace}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{descTwo}</FONT></TD></TR>
</TABLE>";

            case SegmentMode.Flush:
            default:
                return $@"<TABLE BORDER=""0"" CELLBORDER=""0"" CELLSPACING=""5"" CELLPADDING=""{p}"" BGCOLOR=""{c.Background}"">
<TR><TD ALIGN=""LEFT"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""120"">{IdRow(IdF(c.Id, id), series, textWidth)}</TD>
<TD ROWSPAN=""2""{rw} ALIGN=""LEFT"" BGCOLOR=""{c.Fill2}:{c.Background}"" GRADIENTANGLE=""120""><FONT FACE=""{idFace}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD{tw} ALIGN=""LEFT"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""120""><FONT FACE=""{idFace}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{descTwo}</FONT></TD></TR>
</TABLE>";
        }
    }

    /// <summary>
    /// The id row: the handle and (when shown) its fixed-width Series plaque. With a group
    /// <paramref name="textWidth"/> the id cell is width-locked (= text column minus the plaque bay) so
    /// the plaque sits on one vertical edge across the group; without it the plaque follows the handle
    /// inline (designer preview). <see cref="SeriePos.Before"/> puts the plaque ahead of the handle.
    /// </summary>
    private string IdRow(string idContent, int series, int textWidth)
    {
        if (!HasSerie(series)) return idContent;

        string plaque = SeriePlaqueCell(series);
        if (_t.Series.Pos == SeriePos.Before)
            return $"<TABLE BORDER=\"0\" CELLBORDER=\"0\" CELLSPACING=\"0\" CELLPADDING=\"0\"><TR>{plaque}<TD WIDTH=\"8\"></TD><TD ALIGN=\"LEFT\">{idContent}</TD></TR></TABLE>";

        string idw = textWidth > 0
            ? $" WIDTH=\"{Math.Max(1, textWidth - _t.Padding * 2 - SerieBay)}\""
            : "";
        return $"<TABLE BORDER=\"0\" CELLBORDER=\"0\" CELLSPACING=\"0\" CELLPADDING=\"0\"><TR><TD{idw} ALIGN=\"LEFT\">{idContent}</TD>{plaque}</TR></TABLE>";
    }

    // The fixed-size plaque cell: WIDTH-locked and centered, so the roman numeral (I / II / III) is the
    // same size regardless of how many strokes it has.
    private string SeriePlaqueCell(int series)
    {
        var c = _t.Colors;
        string roman = Roman[series];
        string face = _t.Fonts.Id;
        return _t.Series.Fill == SerieFill.Filled
            ? $"<TD BGCOLOR=\"{c.Serie(series)}\" WIDTH=\"{SeriePlaqueWidth}\" ALIGN=\"CENTER\" CELLPADDING=\"3\"><FONT FACE=\"{face}\" POINT-SIZE=\"10\" COLOR=\"{c.SerieText}\"><B>{roman}</B></FONT></TD>"
            : $"<TD BORDER=\"1\" COLOR=\"{c.Serie(series)}\" WIDTH=\"{SeriePlaqueWidth}\" ALIGN=\"CENTER\" CELLPADDING=\"3\"><FONT FACE=\"{face}\" POINT-SIZE=\"10\" COLOR=\"{c.Serie(series)}\"><B>{roman}</B></FONT></TD>";
    }

    // Estimates use 13pt glyphs (slightly generous vs the 11/12pt body), so the estimate is an upper
    // bound on real text width — Graphviz treats stamped WIDTH as a minimum, so over-estimating keeps
    // columns clamped (uniform) while under-estimating would let a label overflow and break alignment.

    /// <summary>Estimated width (points) of the text column. The id row reserves the plaque bay so the
    /// width-locked id cell never has to shrink below the handle; the desc row uses its longest
    /// comma-wrapped line.</summary>
    public int EstimateLeftPts(string id, string desc, bool hasSerie)
    {
        id ??= ""; desc ??= "";
        int glyph = (int)Math.Round(13 * CharWidthFactor);
        int idRow = id.Length * glyph + (hasSerie ? SerieBay + 10 : 0);
        int descRow = DescLongestLineChars(desc) * glyph;
        return Math.Max(idRow, descRow) + _t.Padding * 2 + 8;
    }

    /// <summary>Estimated width (points) of the type column (longer of the two wrapped lines).</summary>
    public int EstimateRightPts(string type)
    {
        type ??= "";
        int bodyPts = (int)Math.Round(11 * CharWidthFactor);
        return LongestLine(type) * bodyPts + _t.Padding * 2 + 8;
    }

    /// <summary>True when this label shows a Serie plaque.</summary>
    public bool HasSerie(int series) => _t.Series.Show && series >= 1 && series <= 3;

    /// <summary>
    /// The per-column widths fed back into <see cref="Build"/>: <c>textWidth</c> (the text/id+desc column)
    /// and <c>typeWidth</c> (the type column) — each the group's max for that column. Locking both makes
    /// every box the same total width without an outer table WIDTH (which would leave Graphviz a surplus
    /// to pad with a filler column). Both are minima Graphviz clamps to; estimates are biased high so
    /// nothing overflows them.
    /// </summary>
    public (int textWidth, int typeWidth) GroupWidths(int maxLeftPts, int maxRightPts) => (maxLeftPts, maxRightPts);

    private static int LongestLine(string type)
    {
        int i = type.LastIndexOf(' ');
        if (i < 0) return type.Length;
        return Math.Max(i, type.Length - i - 1);
    }

    // Split type at the last space into two lines.
    private static string TwoLine(string t)
    {
        int i = t.LastIndexOf(' ');
        return i < 0 ? t : t.Substring(0, i) + "<BR/>" + t.Substring(i + 1);
    }

    // Wrap desc at the FIRST comma (comma stays on the first line) so long descriptions don't balloon
    // the column. No comma -> unchanged. Operates on already-escaped text (escaping adds no commas).
    private static string DescWrap(string desc)
    {
        int i = desc.IndexOf(',');
        if (i < 0) return desc;
        string rest = desc.Substring(i + 1).TrimStart();
        return rest.Length == 0 ? desc : desc.Substring(0, i + 1) + "<BR/>" + rest;
    }

    private static int DescLongestLineChars(string desc)
    {
        int i = desc.IndexOf(',');
        if (i < 0) return desc.Length;
        int first = i + 1;
        int second = desc.Substring(i + 1).TrimStart().Length;
        return Math.Max(first, second);
    }

    private static string Escape(string s) =>
        string.IsNullOrEmpty(s) ? string.Empty
        : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
