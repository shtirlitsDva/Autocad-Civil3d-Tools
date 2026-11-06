using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LineTypes
{
    /// <summary>
    /// The four text linetypes the old commands made (CCL, CCLS, CCLX, CCLXS), as
    /// definitions: a line with a text that reads both ways along it, with or without an
    /// X between, the text cut into pieces or whole. The arithmetic is the old commands':
    /// at their text size, S=0.9, a 5 unit line between, 0.05 of room each side of a text,
    /// the text measured in its style at 0.9 (<c>width</c>). At another text size every
    /// length keeps its proportion to the text, so the old commands' linetypes come out
    /// unchanged and a smaller text keeps the same look.
    /// </summary>
    public sealed class LinetypePreset
    {
        /// <summary>The old commands' text size (S=), at which the lengths are the old ones.</summary>
        public const double DefaultTextSize = 0.9;

        private const string XText = "X";

        private readonly Func<string, string, Proportions, Func<string, double>, IReadOnlyList<LinetypeElement>> m_build;

        private LinetypePreset(string command, string title, string summary,
            Func<string, string, Proportions, Func<string, double>, IReadOnlyList<LinetypeElement>> build)
        {
            Command = command;
            Title = title;
            Summary = summary;
            m_build = build;
        }

        /// <summary>The command that made this linetype before the editor (CCL, CCLS, ...).</summary>
        public string Command { get; }
        public string Title { get; }
        public string Summary { get; }

        public override string ToString() => Title;

        /// <summary>CCL: the text in up to four pieces, then the same pieces turned over.</summary>
        public static readonly LinetypePreset TextBothWays = new(
            "CCL", "Text both ways, in pieces",
            "Line, the text (cut into up to 4 pieces so it bends with the line), a double line, the text turned over, line.",
            (text, style, p, width) => TextBothWaysElements(text, style, p, width));

        /// <summary>CCLS: the text whole, then turned over.</summary>
        public static readonly LinetypePreset TextBothWaysWhole = new(
            "CCLS", "Text both ways, whole",
            "Line, the text, a double line, the text turned over, line.",
            (text, style, p, width) => TextBothWaysWholeElements(text, style, p, width));

        /// <summary>CCLX: the text in up to three pieces, an X, the pieces turned over, an X.</summary>
        public static readonly LinetypePreset TextWithX = new(
            "CCLX", "Text and X, in pieces",
            "Half line, the text (cut into up to 3 pieces), half line, X, half line, the text turned over, half line, X.",
            (text, style, p, width) => TextWithXElements(text, style, p, width));

        /// <summary>CCLXS: the text whole, an X, the text turned over, an X.</summary>
        public static readonly LinetypePreset TextWithXWhole = new(
            "CCLXS", "Text and X, whole",
            "Half line, the text, half line, X, half line, the text turned over, half line, X.",
            (text, style, p, width) => TextWithXWholeElements(text, style, p, width));

        /// <summary>The four, in the order the old commands came in.</summary>
        public static readonly IReadOnlyList<LinetypePreset> All = new[] { TextBothWays, TextBothWaysWhole, TextWithX, TextWithXWhole };

        /// <summary>
        /// The linetype <paramref name="name"/> drawing <paramref name="text"/> in
        /// <paramref name="style"/> at text size <paramref name="size"/> (S=);
        /// <paramref name="width"/> is a text's width in that style at that size.
        /// </summary>
        public LinetypeDefinition Build(string name, string text, string style, double size, Func<string, double> width)
        {
            if (text.Length == 0) throw new ArgumentException("The linetype needs a text.", nameof(text));
            if (!(size > 0) || double.IsInfinity(size))
                throw new ArgumentOutOfRangeException(nameof(size), size, "The text size must be above 0.");
            return new LinetypeDefinition(name, $"{text} ---- {text} ---- {text} ----", true,
                m_build(text, style, new Proportions(size), width));
        }

        /// <summary>
        /// The old commands' lengths at a text size: at 0.9 exactly theirs, at another size
        /// each in proportion to the text.
        /// </summary>
        private sealed class Proportions
        {
            public Proportions(double size)
            {
                double k = size / DefaultTextSize;
                TextSize = size;
                LineLength = 5 * k;
                TextBuffer = 0.05 * k;
                TextPad = 0.1 * k;
                TextDrop = size / 2;
            }

            /// <summary>S=, the texts' size.</summary>
            public double TextSize { get; }

            /// <summary>The line between two texts, X and its room included (5 at 0.9).</summary>
            public double LineLength { get; }

            /// <summary>The room each side of a text (0.05 at 0.9).</summary>
            public double TextBuffer { get; }

            /// <summary>What the pieced and both-ways texts add to their width (0.1 at 0.9).</summary>
            public double TextPad { get; }

            /// <summary>How far a text sits below the line, so it is centred on it: half its size.</summary>
            public double TextDrop { get; }
        }

        // ----------------------------------------------------------------- the four

        private static TextInclusion Text(string text, string style, Proportions p, double rotation, double x, double y) =>
            new(text, style, new InclusionPlacement(p.TextSize, rotation, RotationKind.Relative, x, y));

        /// <summary>The line on each side of an X: (5 - the X's room) / 2.</summary>
        private static double HalfLine(Proportions p, Func<string, double> width) =>
            Math.Max(0.0, p.LineLength - Math.Abs(XDash(p, width))) / 2.0;

        /// <summary>The X's dash: the X and its room each side, as a gap.</summary>
        private static double XDash(Proportions p, Func<string, double> width) => -(Math.Max(0.0, width(XText)) + 2 * p.TextBuffer);

        private static LinetypeElement X(string style, Proportions p, Func<string, double> width) =>
            new(XDash(p, width), Text(XText, style, p, 0, -(width(XText) + p.TextBuffer), -p.TextDrop));

        /// <summary>Cuts the text into near-equal pieces, the longer ones first.</summary>
        private static List<string> Pieces(string text, int count)
        {
            var pieces = new List<string>();
            int baseLength = text.Length / count;
            int remainder = text.Length % count;
            int cursor = 0;
            for (int i = 0; i < count; i++)
            {
                int length = baseLength + (i < remainder ? 1 : 0);
                if (length <= 0) continue;
                pieces.Add(text.Substring(cursor, length));
                cursor += length;
            }
            if (cursor < text.Length && pieces.Count > 0) pieces[pieces.Count - 1] += text.Substring(cursor);
            return pieces;
        }

        private static IReadOnlyList<LinetypeElement> TextBothWaysElements(string text, string style, Proportions p, Func<string, double> width)
        {
            const int maxPieces = 4;
            List<string> forward = text.Length <= maxPieces
                ? text.Select(c => c.ToString()).ToList()
                : Pieces(text, maxPieces);
            int n = forward.Count;
            double inner = n > 1 ? 0.0 : p.TextBuffer;
            // Each piece's letters reversed, the pieces kept in order.
            List<string> reversed = forward.Select(piece => new string(piece.Reverse().ToArray())).ToList();
            double line = Math.Max(0.0, (p.LineLength - Math.Abs(XDash(p, width))) / 2.0);

            var elements = new List<LinetypeElement> { LinetypeElement.Plain(line) };
            for (int i = 0; i < n; i++)
            {
                double w = width(forward[i]) + p.TextPad;
                double left = i == 0 ? p.TextBuffer : 0;
                double right = i == n - 1 ? p.TextBuffer : 0;
                elements.Add(new LinetypeElement(-(w + left + right), Text(forward[i], style, p, 0, -(w - right), -p.TextDrop)));
            }
            elements.Add(LinetypeElement.Plain(2 * line));
            for (int i = 0; i < n; i++)
            {
                double w = width(reversed[i]) + p.TextPad;
                double left = i == 0 ? p.TextBuffer : inner;
                double right = i == n - 1 ? p.TextBuffer : inner;
                elements.Add(new LinetypeElement(-(w + left + right), Text(reversed[i], style, p, Math.PI, -p.TextBuffer, p.TextDrop)));
            }
            elements.Add(LinetypeElement.Plain(line));
            return elements;
        }

        private static IReadOnlyList<LinetypeElement> TextBothWaysWholeElements(string text, string style, Proportions p, Func<string, double> width)
        {
            double line = Math.Max(0.0, (p.LineLength - Math.Abs(XDash(p, width))) / 2.0);
            double w = width(text) + p.TextPad;
            return new[]
            {
                LinetypeElement.Plain(line),
                new LinetypeElement(-(w + 2 * p.TextBuffer), Text(text, style, p, 0, -(w - p.TextBuffer), -p.TextDrop)),
                LinetypeElement.Plain(2 * line),
                new LinetypeElement(-(w + 2 * p.TextBuffer), Text(text, style, p, Math.PI, -p.TextBuffer, p.TextDrop)),
                LinetypeElement.Plain(line),
            };
        }

        private static IReadOnlyList<LinetypeElement> TextWithXElements(string text, string style, Proportions p, Func<string, double> width)
        {
            // 12 dashes at most, 6 of them the lines and the X's: 3 pieces.
            int maxPieces = Math.Max(1, Math.Min(text.Length, (LinetypeDefinition.MaxElements - 6) / 2));
            List<string> forward = Pieces(text, Math.Max(1, Math.Min(maxPieces, text.Length)));
            int n = forward.Count;
            double inner = n > 1 ? 0.0 : p.TextBuffer;
            // The pieces in reverse order, each piece's letters kept.
            List<string> reversed = Enumerable.Reverse(forward).ToList();
            double half = HalfLine(p, width);

            var elements = new List<LinetypeElement> { LinetypeElement.Plain(half) };
            for (int i = 0; i < n; i++)
            {
                double w = width(forward[i]);
                double left = i == 0 ? p.TextBuffer : inner;
                double right = i == n - 1 ? p.TextBuffer : inner;
                elements.Add(new LinetypeElement(-(w + left + right), Text(forward[i], style, p, 0, -(w - right), -p.TextDrop)));
            }
            elements.Add(LinetypeElement.Plain(half));
            elements.Add(X(style, p, width));
            elements.Add(LinetypeElement.Plain(half));
            for (int i = 0; i < n; i++)
            {
                double w = width(reversed[i]);
                double left = i == 0 ? p.TextBuffer : inner;
                double right = i == n - 1 ? p.TextBuffer : inner;
                elements.Add(new LinetypeElement(-(w + left + right), Text(reversed[i], style, p, Math.PI, 0, p.TextDrop)));
            }
            elements.Add(LinetypeElement.Plain(half));
            elements.Add(X(style, p, width));
            return elements;
        }

        private static IReadOnlyList<LinetypeElement> TextWithXWholeElements(string text, string style, Proportions p, Func<string, double> width)
        {
            double half = HalfLine(p, width);
            double w = width(text);
            return new[]
            {
                LinetypeElement.Plain(half),
                new LinetypeElement(-(w + 2 * p.TextBuffer), Text(text, style, p, 0, -(w - p.TextBuffer), -p.TextDrop)),
                LinetypeElement.Plain(half),
                X(style, p, width),
                LinetypeElement.Plain(half),
                new LinetypeElement(-(w + 2 * p.TextBuffer), Text(text, style, p, Math.PI, 0, p.TextDrop)),
                LinetypeElement.Plain(half),
                X(style, p, width),
            };
        }
    }

    /// <summary>Changes made to a whole pattern.</summary>
    public static class LinetypeTransforms
    {
        /// <summary>
        /// The old UELTWS / UELTNS: the pattern twice, its words at 0° in the first copy and
        /// turned over in the second, so one of the two reads right whichever way the line
        /// runs. Symbols (X, a lightning bolt) and shapes keep their rotation. A zero-length
        /// dash that carries nothing is left out first, as the old tools did.
        /// <paramref name="width"/> is a text's width in a style at a scale; when it throws,
        /// the turned-over text keeps no X offset, as before.
        /// </summary>
        public static LinetypeDefinition ReadableBothWays(LinetypeDefinition definition, Func<string, string, double, double> width)
        {
            List<LinetypeElement> pattern = definition.Elements
                .Where(e => e.Length != 0 || e.Inclusion is not NoInclusion)
                .ToList();
            if (pattern.Count * 2 > LinetypeDefinition.MaxElements)
                throw new InvalidOperationException(
                    $"{definition.Name} has {pattern.Count} dashes: twice that is more than the {LinetypeDefinition.MaxElements} a linetype holds.");

            // Only words change; every other dash is copied as it is into both halves.
            var first = pattern.Select(e => e.Inclusion is TextInclusion t && t.IsWording
                ? e with { Inclusion = t with { Placement = t.Placement with { Rotation = 0 } } }
                : e);
            var second = pattern.Select(e => e.Inclusion is TextInclusion t && t.IsWording
                ? e with { Inclusion = TurnedOver(t, width) }
                : e);
            return definition with { Elements = first.Concat(second).ToList() };
        }

        private static TextInclusion TurnedOver(TextInclusion text, Func<string, string, double, double> width)
        {
            double x;
            try
            {
                double measured = width(text.Style, text.Text, text.Placement.Scale > 0 ? text.Placement.Scale : 0.9);
                x = measured - 0.05;
            }
            catch (Exception)
            {
                x = 0;
            }
            return text with { Placement = text.Placement with { Rotation = Math.PI, X = x, Y = -text.Placement.Y } };
        }
    }
}
