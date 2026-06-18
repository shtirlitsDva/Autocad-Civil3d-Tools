using System;

namespace IntersectUtilities.GraphWriteV2.Theming;

/// <summary>
/// The single source of truth for a node's Graphviz HTML-like-label markup, ported verbatim from the
/// design package's <c>label()</c> / <c>idCell()</c> / <c>chipCell()</c> blueprint. Pure and free of
/// any AutoCAD dependency so the live designer preview and the real DOT export call exactly the same
/// code — what the user sees in the preview is byte-for-byte what GRAPHWRITEV2 emits.
///
/// <para>Inputs are the three label fields plus a Series number (1/2/3, or 0 for "no chip"):</para>
/// <list type="bullet">
///   <item><c>id</c>   — top-left, carries the Series chip</item>
///   <item><c>type</c> — right column, wrapped to two lines at the last space</item>
///   <item><c>desc</c> — bottom-left</item>
/// </list>
/// Text values are escaped here, so callers pass raw strings.
///
/// <para><c>minWidth</c> stamps a minimum table width (points). Graphviz sizes each label to its own
/// content, so to make every label in a cluster the same width the styler measures the group's widest
/// label via <see cref="EstimateWidthPts"/> and passes that here; narrower labels pad up to it. The
/// designer preview renders a single label and passes 0 (natural size).</para>
/// </summary>
public sealed class LabelMarkupBuilder
{
    private static readonly string[] Roman = { "", "I", "II", "III" };

    // Approximate average glyph width as a fraction of point size (monospace ID ~0.6; proportional
    // body a touch less). Used only to equalize widths within a group, exactly as the prior styler did.
    private const double CharWidthFactor = 0.62;

    private readonly LabelTheme _t;

    public LabelMarkupBuilder(LabelTheme theme) => _t = theme;

    /// <summary>Returns the <c>&lt;TABLE&gt;…&lt;/TABLE&gt;</c> markup (without the <c>label=&lt;&gt;</c> wrapper).</summary>
    public string Build(string id, string type, string desc, int series, int minWidth = 0)
    {
        id = Escape(id);
        type = Escape(type);
        desc = Escape(desc);

        var c = _t.Colors;
        int p = _t.Padding;
        string f = _t.Fonts.Id, b = _t.Fonts.Body;
        string round = _t.Rounded ? " STYLE=\"ROUNDED\"" : "";
        string w = minWidth > 0 ? $" WIDTH=\"{minWidth}\"" : "";
        string typeTwo = TwoLine(type);

        string IdF(string color, string txt) =>
            $"<FONT FACE=\"{f}\" POINT-SIZE=\"13\" COLOR=\"{color}\"><B>{txt}</B></FONT>";

        switch (_t.Style)
        {
            case LabelStyle.Glass:
                return $@"<TABLE{w} BORDER=""1"" COLOR=""{c.Frame}"" CELLBORDER=""0"" CELLSPACING=""0"" CELLPADDING=""0""{round} BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""125"">
<TR><TD ALIGN=""LEFT"" CELLPADDING=""{p}"" SIDES=""B"" BORDER=""1"" COLOR=""{c.Divider}"">{IdCell(IdF(c.Id, id), series)}</TD>
<TD ROWSPAN=""2"" ALIGN=""LEFT"" CELLPADDING=""{p + 2}"" SIDES=""L"" BORDER=""1"" COLOR=""{c.Divider}""><FONT FACE=""{b}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD ALIGN=""LEFT"" CELLPADDING=""{p}""><FONT FACE=""{b}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{desc}</FONT></TD></TR>
</TABLE>";

            case LabelStyle.Blueprint:
                return $@"<TABLE{w} BORDER=""2"" COLOR=""{c.Frame}"" CELLBORDER=""1"" CELLSPACING=""0"" CELLPADDING=""{p}"" BGCOLOR=""{c.Background}"">
<TR><TD ALIGN=""LEFT"" COLOR=""{c.Divider}"">{IdCell(IdF(c.Id, id), series)}</TD>
<TD ROWSPAN=""2"" ALIGN=""LEFT"" COLOR=""{c.Divider}""><FONT FACE=""{f}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD ALIGN=""LEFT"" COLOR=""{c.Divider}""><FONT FACE=""{f}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{desc}</FONT></TD></TR>
</TABLE>";

            case LabelStyle.Segmented:
                return Segmented(id, typeTwo, desc, series, w);

            case LabelStyle.Fluent:
            {
                string idInner =
                    $"<FONT FACE=\"{b}\" POINT-SIZE=\"10\" COLOR=\"{c.Id}\"><B>&#9642; </B></FONT>" +
                    $"<FONT FACE=\"{b}\" POINT-SIZE=\"13\" COLOR=\"#f3f6fa\"><B>{id}</B></FONT>";
                return $@"<TABLE{w} BORDER=""1"" COLOR=""{c.Frame}"" CELLBORDER=""0"" CELLSPACING=""0"" CELLPADDING=""{p + 2}""{round} BGCOLOR=""{c.FillLite1}:{c.FillLite2}"" GRADIENTANGLE=""135"">
<TR><TD ALIGN=""LEFT"" SIDES=""B"" BORDER=""1"" COLOR=""{c.Divider}"">{IdCell(idInner, series)}</TD>
<TD ROWSPAN=""2"" ALIGN=""LEFT"" SIDES=""L"" BORDER=""1"" COLOR=""{c.Divider}""><FONT FACE=""{b}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD ALIGN=""LEFT""><FONT FACE=""{b}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{desc}</FONT></TD></TR>
</TABLE>";
            }

            case LabelStyle.Terminal:
            default:
                return $@"<TABLE{w} BORDER=""2"" COLOR=""{c.Frame}"" CELLBORDER=""0"" CELLSPACING=""0"" CELLPADDING=""{p}"" BGCOLOR=""{c.Background}"">
<TR><TD ALIGN=""LEFT"" SIDES=""B"" BORDER=""1"" COLOR=""{c.Divider}"">{IdCell(IdF(c.Id, "[" + id + "]"), series)}</TD>
<TD ROWSPAN=""2"" ALIGN=""LEFT"" SIDES=""L"" BORDER=""1"" COLOR=""{c.Divider}""><FONT FACE=""{f}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD ALIGN=""LEFT""><FONT FACE=""{f}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{desc}</FONT></TD></TR>
</TABLE>";
        }
    }

    private string Segmented(string id, string typeTwo, string desc, int series, string w)
    {
        var c = _t.Colors;
        int p = _t.Padding;
        string idFace = _t.Fonts.Id;
        string body = _t.Fonts.Body;

        string IdF(string color, string txt) =>
            $"<FONT FACE=\"{idFace}\" POINT-SIZE=\"13\" COLOR=\"{color}\"><B>{txt}</B></FONT>";

        switch (_t.TileMode)
        {
            case SegmentMode.Pop:
                return $@"<TABLE{w} BORDER=""0"" CELLBORDER=""0"" CELLSPACING=""6"" CELLPADDING=""{p}"" BGCOLOR=""#050608"">
<TR><TD ALIGN=""LEFT"" BGCOLOR=""{c.TileA1}:{c.TileA2}"" GRADIENTANGLE=""120"">{IdCell(IdF(c.IdInk, id), series)}</TD>
<TD ROWSPAN=""2"" ALIGN=""LEFT"" BGCOLOR=""{c.TileA2}:{c.TileA1}"" GRADIENTANGLE=""120""><FONT FACE=""{body}"" POINT-SIZE=""11"" COLOR=""{c.Ink}"">{typeTwo}</FONT></TD></TR>
<TR><TD ALIGN=""LEFT"" BGCOLOR=""{c.TileA1}:{c.TileA2}"" GRADIENTANGLE=""120""><FONT FACE=""{body}"" POINT-SIZE=""12"" COLOR=""{c.Ink}""><B>{desc}</B></FONT></TD></TR>
</TABLE>";

            case SegmentMode.Bordered:
                return $@"<TABLE{w} BORDER=""0"" CELLBORDER=""0"" CELLSPACING=""5"" CELLPADDING=""{p}"" BGCOLOR=""{c.Background}"">
<TR><TD ALIGN=""LEFT"" BORDER=""1"" COLOR=""{c.Frame}"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""120"">{IdCell(IdF(c.Id, id), series)}</TD>
<TD ROWSPAN=""2"" ALIGN=""LEFT"" BORDER=""1"" COLOR=""{c.Frame}"" BGCOLOR=""{c.Fill2}:{c.Background}"" GRADIENTANGLE=""120""><FONT FACE=""{body}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD ALIGN=""LEFT"" BORDER=""1"" COLOR=""{c.Frame}"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""120""><FONT FACE=""{body}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{desc}</FONT></TD></TR>
</TABLE>";

            case SegmentMode.Flush:
            default:
                return $@"<TABLE{w} BORDER=""0"" CELLBORDER=""0"" CELLSPACING=""5"" CELLPADDING=""{p}"" BGCOLOR=""{c.Background}"">
<TR><TD ALIGN=""LEFT"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""120"">{IdCell(IdF(c.Id, id), series)}</TD>
<TD ROWSPAN=""2"" ALIGN=""LEFT"" BGCOLOR=""{c.Fill2}:{c.Background}"" GRADIENTANGLE=""120""><FONT FACE=""{body}"" POINT-SIZE=""11"" COLOR=""{c.Type}"">{typeTwo}</FONT></TD></TR>
<TR><TD ALIGN=""LEFT"" BGCOLOR=""{c.Fill1}:{c.Fill2}"" GRADIENTANGLE=""120""><FONT FACE=""{body}"" POINT-SIZE=""12"" COLOR=""{c.Body}"">{desc}</FONT></TD></TR>
</TABLE>";
        }
    }

    private string IdCell(string idInner, int series)
    {
        if (!_t.Series.Show || series < 1 || series > 3) return idInner;
        string chip = ChipCell(series);
        return _t.Series.Pos == ChipPos.Before
            ? $"<TABLE BORDER=\"0\" CELLBORDER=\"0\" CELLSPACING=\"0\" CELLPADDING=\"0\"><TR>{chip}<TD WIDTH=\"9\"> </TD><TD>{idInner}</TD></TR></TABLE>"
            : $"<TABLE BORDER=\"0\" CELLBORDER=\"0\" CELLSPACING=\"0\" CELLPADDING=\"0\"><TR><TD>{idInner}</TD><TD WIDTH=\"9\"> </TD>{chip}</TR></TABLE>";
    }

    private string ChipCell(int series)
    {
        var c = _t.Colors;
        string roman = Roman[series];
        return _t.Series.Fill == ChipFill.Filled
            ? $"<TD BGCOLOR=\"{c.Chip(series)}\" CELLPADDING=\"3\"><FONT FACE=\"{_t.Fonts.Id}\" POINT-SIZE=\"10\" COLOR=\"{c.ChipText}\"><B>{roman}</B></FONT></TD>"
            : $"<TD BORDER=\"1\" COLOR=\"{c.Chip(series)}\" CELLPADDING=\"3\"><FONT FACE=\"{_t.Fonts.Id}\" POINT-SIZE=\"10\" COLOR=\"{c.Chip(series)}\"><B>{roman}</B></FONT></TD>";
    }

    /// <summary>
    /// Approximates the natural width (points) of the label for the given fields, so the styler can
    /// take the per-group maximum and feed it back as <c>minWidth</c>. Mirrors the prior styler's
    /// character-count heuristic: left column = max(id row, desc row), right column = the longer
    /// wrapped type line, plus chrome for cell padding, dividers, and the optional Series chip.
    /// </summary>
    public int EstimateWidthPts(string id, string type, string desc, int series)
    {
        id ??= ""; type ??= ""; desc ??= "";

        bool chip = _t.Series.Show && series >= 1 && series <= 3;
        int chipChars = chip ? Roman[series].Length + 3 : 0; // chip cell + the 9px gap, in char units

        int leftChars = Math.Max(id.Length + chipChars, desc.Length);
        int rightChars = LongestLine(type);

        int idPts = (int)Math.Round(13 * CharWidthFactor);
        int bodyPts = (int)Math.Round(11 * CharWidthFactor);

        int leftPts = leftChars * idPts;
        int rightPts = rightChars * bodyPts;

        int chrome = _t.Padding * 4 + 28; // both column paddings + borders/dividers + gaps
        return leftPts + rightPts + chrome;
    }

    private static int LongestLine(string type)
    {
        int i = type.LastIndexOf(' ');
        if (i < 0) return type.Length;
        int first = i;                       // chars before the split
        int second = type.Length - i - 1;    // chars after the split
        return Math.Max(first, second);
    }

    // Split at the last space into two lines. Operates on already-escaped text; escaping never
    // introduces spaces, so the split point is unaffected.
    private static string TwoLine(string t)
    {
        int i = t.LastIndexOf(' ');
        return i < 0 ? t : t.Substring(0, i) + "<BR/>" + t.Substring(i + 1);
    }

    private static string Escape(string s) =>
        string.IsNullOrEmpty(s) ? string.Empty
        : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
