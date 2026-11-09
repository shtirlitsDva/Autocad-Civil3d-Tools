using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace IntersectUtilities.LineTypes
{
    /// <summary>
    /// The .lin text of a linetype, both ways: what AutoCAD's and BricsCAD's LINETYPE
    /// command loads, and what PRINTLT and the editor show.
    /// <code>
    /// *NAME,description
    /// A,dash,dash,["TEXT",STYLE,S=0.9,R=0,X=-0.1,Y=-0.45],dash,[SHAPE,file.shx,S=1,U=0,X=0,Y=0]
    /// </code>
    /// A bracket belongs to the dash before it. Rotations are written in degrees and read
    /// with an optional d (degrees), r (radians) or g (grads) suffix. A shape bracket also
    /// reads the old positional form [SHAPE,file.shx,scale,rotation,x,y].
    /// </summary>
    public static class LinFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The rotation letter of each RotationKind, by its value: R=, A=, U=.
        private static readonly string[] RotationLetters = { "R", "A", "U" };

        /// <summary>A number as the .lin writes it: invariant, up to six decimals, no -0.</summary>
        public static string Number(double value)
        {
            string text = Math.Round(value, 6).ToString("0.######", Inv);
            return text == "-0" ? "0" : text;
        }

        /// <summary>The S=, R=/A=/U=, X= and Y= fields of a bracket, each after a comma.</summary>
        public static string PlacementFields(InclusionPlacement placement) =>
            $",S={Number(placement.Scale)}" +
            $",{RotationLetters[(int)placement.Kind]}={Number(placement.Rotation * 180.0 / Math.PI)}" +
            $",X={Number(placement.X)},Y={Number(placement.Y)}";

        /// <summary>One definition: its *NAME line and its pattern line.</summary>
        public static string Format(LinetypeDefinition definition)
        {
            var text = new StringBuilder();
            text.Append('*').Append(definition.Name);
            text.Append(',').Append(definition.Description).Append("\r\n");
            text.Append('A');
            foreach (LinetypeElement element in definition.Elements)
            {
                text.Append(',').Append(Number(element.Length));
                string bracket = element.Inclusion.LinText();
                if (bracket.Length > 0) text.Append(',').Append(bracket);
            }
            return text.ToString();
        }

        /// <summary>Every definition of a .lin text, in its order. Throws FormatException naming the line.</summary>
        public static IReadOnlyList<LinetypeDefinition> Parse(string text)
        {
            var definitions = new List<LinetypeDefinition>();
            string header = "";
            int headerLine = 0;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNumber = i + 1;
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith(";")) continue;
                if (line.StartsWith("*"))
                {
                    if (headerLine > 0)
                        throw new FormatException($"Line {headerLine}: {header} has no pattern line (A,...) after it.");
                    header = line;
                    headerLine = lineNumber;
                    continue;
                }
                if (headerLine == 0)
                    throw new FormatException($"Line {lineNumber}: a pattern line needs a *NAME line before it.");
                definitions.Add(ParseDefinition(header, line, lineNumber));
                header = "";
                headerLine = 0;
            }
            if (headerLine > 0)
                throw new FormatException($"Line {headerLine}: {header} has no pattern line (A,...) after it.");
            return definitions;
        }

        private static LinetypeDefinition ParseDefinition(string header, string pattern, int line)
        {
            string body = header.Substring(1);
            int comma = body.IndexOf(',');
            string name = (comma < 0 ? body : body.Substring(0, comma)).Trim();
            string description = comma < 0 ? "" : body.Substring(comma + 1).Trim();
            if (name.Length == 0) throw new FormatException($"Line {line - 1}: the *NAME line has no name.");

            List<string> fields = SplitTopLevel(pattern, line);
            if (fields.Count == 0 || !string.Equals(fields[0], "A", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"Line {line}: a pattern line starts with A, (the only alignment there is).");

            var elements = new List<LinetypeElement>();
            for (int f = 1; f < fields.Count; f++)
            {
                string field = fields[f];
                if (field.StartsWith("["))
                {
                    if (!field.EndsWith("]"))
                        throw new FormatException($"Line {line}: {field} has no closing ].");
                    if (elements.Count == 0)
                        throw new FormatException($"Line {line}: {field} comes before any dash; it belongs to the dash before it.");
                    LinetypeElement last = elements[elements.Count - 1];
                    if (last.Inclusion is not NoInclusion)
                        throw new FormatException($"Line {line}: dash {elements.Count} carries two brackets.");
                    elements[elements.Count - 1] = last with { Inclusion = ParseInclusion(field, line) };
                    continue;
                }
                elements.Add(LinetypeElement.Plain(ParseNumber(field, line)));
            }
            return new LinetypeDefinition(name, description, false, elements);
        }

        private static Inclusion ParseInclusion(string bracket, int line)
        {
            List<string> parts = SplitTopLevel(bracket.Substring(1, bracket.Length - 2), line);
            if (parts.Count < 2)
                throw new FormatException($"Line {line}: {bracket} needs a text or shape and a style or shape file.");

            double scale = 1.0, rotation = 0.0, x = 0.0, y = 0.0;
            RotationKind kind = RotationKind.Relative;
            int positional = 0;
            for (int p = 2; p < parts.Count; p++)
            {
                string part = parts[p];
                int equals = part.IndexOf('=');
                if (equals < 0)
                {
                    // The old positional form: scale, rotation, x, y.
                    double value = positional == 1 ? ParseAngle(part, line) : ParseNumber(part, line);
                    if (positional == 0) scale = value;
                    else if (positional == 1) rotation = value;
                    else if (positional == 2) x = value;
                    else if (positional == 3) y = value;
                    else throw new FormatException($"Line {line}: {bracket} has more than four plain values.");
                    positional++;
                    continue;
                }
                string key = part.Substring(0, equals).Trim().ToUpperInvariant();
                string text = part.Substring(equals + 1).Trim();
                int letter = Array.IndexOf(RotationLetters, key);
                if (letter >= 0)
                {
                    kind = (RotationKind)letter;
                    rotation = ParseAngle(text, line);
                }
                else if (key == "S") scale = ParseNumber(text, line);
                else if (key == "X") x = ParseNumber(text, line);
                else if (key == "Y") y = ParseNumber(text, line);
                else throw new FormatException($"Line {line}: {part} in {bracket} is not S=, R=, A=, U=, X= or Y=.");
            }
            var placement = new InclusionPlacement(scale, rotation, kind, x, y);

            string what = parts[0];
            string where = parts[1].Trim();
            if (what.StartsWith("\""))
            {
                if (what.Length < 2 || !what.EndsWith("\""))
                    throw new FormatException($"Line {line}: the text {what} has no closing quote.");
                return new TextInclusion(what.Substring(1, what.Length - 2), where, placement);
            }
            // Our own export of a shape whose name was unknown: #number.
            if (what.StartsWith("#") && int.TryParse(what.Substring(1), NumberStyles.Integer, Inv, out int number))
                return new ShapeInclusion(number, "", where, placement);
            return new ShapeInclusion(0, what, where, placement);
        }

        private static double ParseNumber(string text, int line)
        {
            if (double.TryParse(text.Trim(), NumberStyles.Float, Inv, out double value)) return value;
            throw new FormatException($"Line {line}: {text} is not a number.");
        }

        /// <summary>An angle in radians from degrees, or from radians (r) or grads (g).</summary>
        private static double ParseAngle(string text, int line)
        {
            string t = text.Trim();
            double factor = Math.PI / 180.0;
            char last = t.Length > 0 ? char.ToLowerInvariant(t[t.Length - 1]) : ' ';
            if (last == 'r') { factor = 1.0; t = t.Substring(0, t.Length - 1); }
            else if (last == 'g') { factor = Math.PI / 200.0; t = t.Substring(0, t.Length - 1); }
            else if (last == 'd') t = t.Substring(0, t.Length - 1);
            return ParseNumber(t, line) * factor;
        }

        /// <summary>Splits on the commas that are neither inside quotes nor inside brackets.</summary>
        private static List<string> SplitTopLevel(string text, int line)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            int depth = 0;
            foreach (char c in text)
            {
                if (c == '"') quoted = !quoted;
                else if (!quoted && c == '[') depth++;
                else if (!quoted && c == ']') depth--;
                if (c == ',' && !quoted && depth == 0)
                {
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            if (quoted) throw new FormatException($"Line {line}: a quote is not closed.");
            if (depth != 0) throw new FormatException($"Line {line}: a bracket is not closed.");
            parts.Add(current.ToString().Trim());
            return parts.Where(p => p.Length > 0).ToList();
        }
    }
}
