using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LongitudinalProfiles.Quality;

internal readonly record struct QaColumn(string Header, bool Numeric = false, int MaxWidth = 24);

internal sealed class QaTable
{
    private readonly QaColumn[] columns;
    private readonly List<string[]> rows = new();

    internal QaTable(params QaColumn[] columns) => this.columns = columns;

    internal void Add(params string[] cells)
    {
        if (cells.Length != columns.Length) throw new ArgumentException("Table row does not match its columns.", nameof(cells));
        rows.Add(cells.Select(c => c.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')).ToArray());
    }

    private int[] Widths() => columns.Select((c, i) => Math.Max(c.Header.Length,
        Math.Min(c.MaxWidth, rows.Select(r => r[i].Length).DefaultIfEmpty(0).Max()))).ToArray();

    internal static int[] SharedWidths(IEnumerable<QaTable> tables)
    {
        int[][] widths = tables.Select(t => t.Widths()).ToArray();
        return widths.Length == 0 ? Array.Empty<int>() : Enumerable.Range(0, widths[0].Length).Select(i => widths.Max(w => w[i])).ToArray();
    }

    internal string Render() => Render(Widths());

    internal string Render(IReadOnlyList<int> widths)
    {
        string Format(string[] cells) => string.Join(" | ", cells.Select((cell, i) =>
            columns[i].Numeric ? cell.PadLeft(widths[i]) : cell.PadRight(widths[i])));
        var lines = new List<string> { Format(columns.Select(c => c.Header).ToArray()),
            string.Join("-+-", widths.Select(w => new string('-', w))) };
        foreach (string[] row in rows)
        {
            string[][] wrapped = row.Select((cell, i) => Wrap(cell, widths[i])).ToArray();
            for (int line = 0; line < wrapped.Max(c => c.Length); line++)
                lines.Add(Format(wrapped.Select(c => line < c.Length ? c[line] : "").ToArray()));
        }
        return string.Join("\n", lines);
    }

    private static string[] Wrap(string text, int width)
    {
        var lines = new List<string>();
        while (text.Length > width)
        {
            int at = text.LastIndexOf(' ', width - 1, width);
            if (at <= 0) at = width;
            lines.Add(text[..at]);
            text = text[at..].TrimStart();
        }
        lines.Add(text);
        return lines.ToArray();
    }
}

internal static class QaPipeText
{
    internal static string Description(QaPipeSpan pipe) => $"{pipe.Label}; casing {pipe.Diameter * 1000:0.#} mm";
}
