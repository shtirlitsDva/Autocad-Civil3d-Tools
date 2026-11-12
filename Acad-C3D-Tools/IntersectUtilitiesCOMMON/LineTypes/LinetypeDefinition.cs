using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LineTypes
{
    // A complex linetype as the .lin format and the linetype record both describe it,
    // with no host type in it: the presets, the .lin text, the editor (CCL) and its
    // preview all work on this, and LinetypeRecords is the one place that turns it
    // into a LinetypeTableRecord and back.
    //
    // The record's layout, which the .lin text mirrors one to one:
    // - a pattern is a list of dashes, at most 12;
    // - a dash's length is positive for a drawn dash, negative for a gap, 0 for a dot;
    // - a dash can carry one inclusion, a text or a shape, drawn at the END of the dash
    //   and moved from there by its X and Y offsets (the .lin writes it in brackets
    //   after the dash it belongs to);
    // - the inclusion has a scale (S=), a rotation and the way the rotation is measured
    //   (R= along the line, A= absolute, U= along the line but never upside down).

    /// <summary>How an inclusion's rotation is measured: the .lin's R=, A= and U=.</summary>
    public enum RotationKind
    {
        /// <summary>R=: relative to the line's direction.</summary>
        Relative,
        /// <summary>A=: absolute, the same for every inclusion whatever the line does.</summary>
        Absolute,
        /// <summary>U=: relative to the line, turned over where it would read upside down.</summary>
        Upright,
    }

    /// <summary>
    /// Where an inclusion sits on its dash: its scale (S=), its rotation in radians and how
    /// that is measured, and its offset from the dash's end along the line (X=) and across
    /// it (Y=). The offsets are not scaled by S=.
    /// </summary>
    public sealed record InclusionPlacement(double Scale, double Rotation, RotationKind Kind, double X, double Y)
    {
        /// <summary>S=1, R=0, no offset: what the .lin means when a field is left out.</summary>
        public static readonly InclusionPlacement Default = new(1.0, 0.0, RotationKind.Relative, 0.0, 0.0);
    }

    /// <summary>
    /// What one dash of the record receives for its inclusion. LinetypeRecords writes a
    /// record through it; each kind of inclusion says itself which call it is.
    /// </summary>
    public interface IInclusionSink
    {
        void Nothing(int index);
        void Text(int index, TextInclusion text);
        void Shape(int index, ShapeInclusion shape);
    }

    /// <summary>What a dash carries at its end: nothing, a text or a shape.</summary>
    public abstract record Inclusion
    {
        /// <summary>The dash carries nothing.</summary>
        public static readonly Inclusion None = new NoInclusion();

        /// <summary>Hands this inclusion to the sink as the dash at <paramref name="index"/>.</summary>
        public abstract void WriteTo(IInclusionSink sink, int index);

        /// <summary>The .lin bracket for this inclusion, or "" when there is none.</summary>
        public abstract string LinText();

        /// <summary>Why this inclusion cannot be written, as dash <paramref name="dash"/> (from 1).</summary>
        public abstract IEnumerable<string> Problems(int dash);
    }

    /// <summary>The dash carries nothing.</summary>
    public sealed record NoInclusion : Inclusion
    {
        public override void WriteTo(IInclusionSink sink, int index) => sink.Nothing(index);
        public override string LinText() => "";
        public override IEnumerable<string> Problems(int dash) => Enumerable.Empty<string>();
    }

    /// <summary>A text drawn with a text style of the drawing (the .lin's ["text",STYLE,...]).</summary>
    public sealed record TextInclusion(string Text, string Style, InclusionPlacement Placement) : Inclusion
    {
        /// <summary>The texts the old tools drew as symbols, not as words: X and a lightning bolt.</summary>
        public static readonly IReadOnlyList<string> Symbols = new[] { "X", "⚡" };

        public override void WriteTo(IInclusionSink sink, int index) => sink.Text(index, this);
        public override string LinText() => $"[\"{Text}\",{Style}{LinFormat.PlacementFields(Placement)}]";

        public override IEnumerable<string> Problems(int dash)
        {
            if (Text.Length == 0) yield return $"Dash {dash} carries an empty text.";
            if (Text.Contains('"')) yield return $"Dash {dash}: a linetype text cannot hold a double quote.";
            if (Style.Trim().Length == 0) yield return $"Dash {dash}: the text has no text style.";
        }

        /// <summary>A word, not a symbol (X or a lightning bolt), as the old upright tools judged it.</summary>
        public bool IsWording => Text.Length > 0 && !Symbols.Contains(Text);
    }

    /// <summary>
    /// A shape from a compiled shape file (the .lin's [NAME,file.shx,...]). The record
    /// stores the shape's number and the shape file's text style; the .lin names the
    /// shape. Either may be unknown (0, ""): LinetypeRecords fills the number from the
    /// file when it writes, and the name when it reads.
    /// </summary>
    public sealed record ShapeInclusion(int Number, string Name, string ShapeFile, InclusionPlacement Placement) : Inclusion
    {
        public override void WriteTo(IInclusionSink sink, int index) => sink.Shape(index, this);

        public override string LinText() =>
            $"[{(Name.Length > 0 ? Name : "#" + Number)},{ShapeFile}{LinFormat.PlacementFields(Placement)}]";

        public override IEnumerable<string> Problems(int dash)
        {
            if (ShapeFile.Trim().Length == 0) yield return $"Dash {dash}: the shape has no shape file.";
        }
    }

    /// <summary>One dash of the pattern: its signed length and what it carries at its end.</summary>
    public sealed record LinetypeElement(double Length, Inclusion Inclusion)
    {
        public static LinetypeElement Plain(double length) => new(length, Inclusion.None);
    }

    /// <summary>A linetype: its name, description, scaled-to-fit flag and its pattern.</summary>
    public sealed record LinetypeDefinition(string Name, string Description, bool ScaledToFit, IReadOnlyList<LinetypeElement> Elements)
    {
        /// <summary>The most dashes a pattern may hold (the .lin format's limit, which the old tools kept to).</summary>
        public const int MaxElements = 12;

        /// <summary>The names the drawing reserves: never written by these tools.</summary>
        public static readonly IReadOnlyList<string> ReservedNames = new[] { "ByLayer", "ByBlock", "Continuous" };

        /// <summary>The characters a symbol name may not hold (| is an xref's, judged on its own).</summary>
        public const string ForbiddenNameCharacters = "<>/\\\":;?*,=`";

        /// <summary>One pattern length: the sum of the dashes' lengths, gaps counted positive.</summary>
        public double PatternLength => Elements.Sum(e => Math.Abs(e.Length));

        /// <summary>
        /// Why this definition cannot be written, in a drafter's words, or an empty list.
        /// Checks what the drawing needs no reading for; LinetypeRecords checks the rest
        /// (the styles exist, the record is ours to change).
        /// </summary>
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            string name = Name.Trim();
            if (name.Length == 0) problems.Add("The linetype has no name.");
            if (name.IndexOfAny(ForbiddenNameCharacters.ToCharArray()) >= 0)
                problems.Add($"The name may not hold any of {ForbiddenNameCharacters}");
            if (ReservedNames.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase)))
                problems.Add($"{name} is the drawing's own linetype and is not changed here.");
            if (name.Contains('|'))
                problems.Add("A linetype from an xref is changed in its own drawing.");
            if (Elements.Count == 0) problems.Add("The pattern has no dashes.");
            if (Elements.Count > MaxElements)
                problems.Add($"The pattern has {Elements.Count} dashes; a linetype holds at most {MaxElements}.");
            if (Elements.Count > 0 && PatternLength <= 0)
                problems.Add("The pattern's length is 0: it needs at least one dash or gap that is not a dot.");
            for (int i = 0; i < Elements.Count; i++)
                problems.AddRange(Elements[i].Inclusion.Problems(i + 1));
            return problems;
        }
    }
}
