using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LineTypes
{
    /// <summary>
    /// The four text linetypes the old commands made (CCL, CCLS, CCLX, CCLXS), as
    /// definitions: a line with a text that reads both ways along it, with or without an
    /// X between, the text cut into pieces or whole. The arithmetic is the old commands',
    /// unchanged: text at S=0.9, a 5 unit line between, 0.05 of room each side of a text,
    /// the text measured in its style at 0.9 (<c>width</c>).
    /// </summary>
    public sealed class LinetypePreset
    {
        private const double LineLength = 5;
        private const double TextScale = 0.9;
        private const double TextBuffer = 0.05;
        private const double TextDrop = 0.45;
        private const string XText = "X";

        private readonly Func<string, string, string, Func<string, double>, IReadOnlyList<LinetypeElement>> m_build;

        private LinetypePreset(string command, string title, string summary,
            Func<string, string, string, Func<string, double>, IReadOnlyList<LinetypeElement>> build)
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
            (name, text, style, width) => TextBothWaysElements(text, style, width));

        /// <summary>CCLS: the text whole, then turned over.</summary>
        public static readonly LinetypePreset TextBothWaysWhole = new(
            "CCLS", "Text both ways, whole",
            "Line, the text, a double line, the text turned over, line.",
            (name, text, style, width) => TextBothWaysWholeElements(text, style, width));

        /// <summary>CCLX: the text in up to three pieces, an X, the pieces turned over, an X.</summary>
        public static readonly LinetypePreset TextWithX = new(
            "CCLX", "Text and X, in pieces",
            "Half line, the text (cut into up to 3 pieces), half line, X, half line, the text turned over, half line, X.",
            (name, text, style, width) => TextWithXElements(text, style, width));

        /// <summary>CCLXS: the text whole, an X, the text turned over, an X.</summary>
        public static readonly LinetypePreset TextWithXWhole = new(
            "CCLXS", "Text and X, whole",
            "Half line, the text, half line, X, half line, the text turned over, half line, X.",
            (name, text, style, width) => TextWithXWholeElements(text, style, width));

        /// <summary>The four, in the order the old commands came in.</summary>
        public static readonly IReadOnlyList<LinetypePreset> All = new[] { TextBothWays, TextBothWaysWhole, TextWithX, TextWithXWhole };

        /// <summary>
        /// The linetype <paramref name="name"/> drawing <paramref name="text"/> in
        /// <paramref name="style"/>; <paramref name="width"/> is a text's width in that style at S=0.9.
        /// </summary>
        public LinetypeDefinition Build(string name, string text, string style, Func<string, double> width)
        {
            if (text.Length == 0) throw new ArgumentException("The linetype needs a text.", nameof(text));
            return new LinetypeDefinition(name, $"{text} ---- {text} ---- {text} ----", true, m_build(name, text, style, width));
        }

        // ----------------------------------------------------------------- the four

        private static TextInclusion Text(string text, string style, double rotation, double x, double y) =>
            new(text, style, new InclusionPlacement(TextScale, rotation, RotationKind.Relative, x, y));

        /// <summary>The line on each side of an X: (5 - the X's room) / 2.</summary>
        private static double HalfLine(Func<string, double> width) =>
            Math.Max(0.0, LineLength - Math.Abs(XDash(width))) / 2.0;

        /// <summary>The X's dash: the X and its room each side, as a gap.</summary>
        private static double XDash(Func<string, double> width) => -(Math.Max(0.0, width(XText)) + 2 * TextBuffer);

        private static LinetypeElement X(string style, Func<string, double> width) =>
            new(XDash(width), Text(XText, style, 0, -(width(XText) + TextBuffer), -TextDrop));

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

        private static IReadOnlyList<LinetypeElement> TextBothWaysElements(string text, string style, Func<string, double> width)
        {
            const int maxPieces = 4;
            List<string> forward = text.Length <= maxPieces
                ? text.Select(c => c.ToString()).ToList()
                : Pieces(text, maxPieces);
            int n = forward.Count;
            double inner = n > 1 ? 0.0 : TextBuffer;
            // Each piece's letters reversed, the pieces kept in order.
            List<string> reversed = forward.Select(piece => new string(piece.Reverse().ToArray())).ToList();
            double line = Math.Max(0.0, (LineLength - Math.Abs(XDash(width))) / 2.0);

            var elements = new List<LinetypeElement> { LinetypeElement.Plain(line) };
            for (int i = 0; i < n; i++)
            {
                double w = width(forward[i]) + 0.1;
                double left = i == 0 ? TextBuffer : 0;
                double right = i == n - 1 ? TextBuffer : 0;
                elements.Add(new LinetypeElement(-(w + left + right), Text(forward[i], style, 0, -(w - right), -TextDrop)));
            }
            elements.Add(LinetypeElement.Plain(2 * line));
            for (int i = 0; i < n; i++)
            {
                double w = width(reversed[i]) + 0.1;
                double left = i == 0 ? TextBuffer : inner;
                double right = i == n - 1 ? TextBuffer : inner;
                elements.Add(new LinetypeElement(-(w + left + right), Text(reversed[i], style, Math.PI, -TextBuffer, TextDrop)));
            }
            elements.Add(LinetypeElement.Plain(line));
            return elements;
        }

        private static IReadOnlyList<LinetypeElement> TextBothWaysWholeElements(string text, string style, Func<string, double> width)
        {
            double line = Math.Max(0.0, (LineLength - Math.Abs(XDash(width))) / 2.0);
            double w = width(text) + 0.1;
            return new[]
            {
                LinetypeElement.Plain(line),
                new LinetypeElement(-(w + 2 * TextBuffer), Text(text, style, 0, -(w - TextBuffer), -TextDrop)),
                LinetypeElement.Plain(2 * line),
                new LinetypeElement(-(w + 2 * TextBuffer), Text(text, style, Math.PI, -TextBuffer, TextDrop)),
                LinetypeElement.Plain(line),
            };
        }

        private static IReadOnlyList<LinetypeElement> TextWithXElements(string text, string style, Func<string, double> width)
        {
            // 12 dashes at most, 6 of them the lines and the X's: 3 pieces.
            int maxPieces = Math.Max(1, Math.Min(text.Length, (LinetypeDefinition.MaxElements - 6) / 2));
            List<string> forward = Pieces(text, Math.Max(1, Math.Min(maxPieces, text.Length)));
            int n = forward.Count;
            double inner = n > 1 ? 0.0 : TextBuffer;
            // The pieces in reverse order, each piece's letters kept.
            List<string> reversed = Enumerable.Reverse(forward).ToList();
            double half = HalfLine(width);

            var elements = new List<LinetypeElement> { LinetypeElement.Plain(half) };
            for (int i = 0; i < n; i++)
            {
                double w = width(forward[i]);
                double left = i == 0 ? TextBuffer : inner;
                double right = i == n - 1 ? TextBuffer : inner;
                elements.Add(new LinetypeElement(-(w + left + right), Text(forward[i], style, 0, -(w - right), -TextDrop)));
            }
            elements.Add(LinetypeElement.Plain(half));
            elements.Add(X(style, width));
            elements.Add(LinetypeElement.Plain(half));
            for (int i = 0; i < n; i++)
            {
                double w = width(reversed[i]);
                double left = i == 0 ? TextBuffer : inner;
                double right = i == n - 1 ? TextBuffer : inner;
                elements.Add(new LinetypeElement(-(w + left + right), Text(reversed[i], style, Math.PI, 0, TextDrop)));
            }
            elements.Add(LinetypeElement.Plain(half));
            elements.Add(X(style, width));
            return elements;
        }

        private static IReadOnlyList<LinetypeElement> TextWithXWholeElements(string text, string style, Func<string, double> width)
        {
            double half = HalfLine(width);
            double w = width(text);
            return new[]
            {
                LinetypeElement.Plain(half),
                new LinetypeElement(-(w + 2 * TextBuffer), Text(text, style, 0, -(w - TextBuffer), -TextDrop)),
                LinetypeElement.Plain(half),
                X(style, width),
                LinetypeElement.Plain(half),
                new LinetypeElement(-(w + 2 * TextBuffer), Text(text, style, Math.PI, 0, TextDrop)),
                LinetypeElement.Plain(half),
                X(style, width),
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
