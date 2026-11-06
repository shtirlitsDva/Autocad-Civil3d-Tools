#if BRICSCAD
using Teigha.DatabaseServices;
using Teigha.Geometry;
using GiTextStyle = Teigha.GraphicsInterface.TextStyle;
#else
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GiTextStyle = Autodesk.AutoCAD.GraphicsInterface.TextStyle;
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IntersectUtilities.LineTypes
{
    /// <summary>
    /// The one thing a host differs in for linetypes: the U= (upright) flag of a dash.
    /// AutoCAD's .NET API reads and sets it (ShapeIsUprightAt); BricsCAD's TD_Mgd V27 has
    /// no such member, though its native BRX does. Each host's application layer binds
    /// its own; <see cref="NoUprightFlags"/> is the answer of a host without it, and of
    /// any caller that never writes U=.
    /// </summary>
    public interface ILinetypeFlags
    {
        /// <summary>Whether this host reads and sets U=.</summary>
        bool CanUpright { get; }

        bool IsUprightAt(LinetypeTableRecord record, int index);

        /// <summary>Sets or clears U= at a dash. Refuses (LinetypeRefusal) to set it where the host cannot.</summary>
        void SetUprightAt(LinetypeTableRecord record, int index, bool upright);
    }

    /// <summary>No U=: reads every dash as not upright and refuses to set one.</summary>
    public sealed class NoUprightFlags : ILinetypeFlags
    {
        public static readonly ILinetypeFlags Instance = new NoUprightFlags();

        private NoUprightFlags() { }

        public bool CanUpright => false;

        public bool IsUprightAt(LinetypeTableRecord record, int index) => false;

        public void SetUprightAt(LinetypeTableRecord record, int index, bool upright)
        {
            if (upright)
                throw new LinetypeRefusal(
                    $"Dash {index + 1} is upright (U=), which this program's API cannot write. Use R= or A=.");
        }
    }

    /// <summary>A linetype that is not written, and why, in a drafter's words.</summary>
    public sealed class LinetypeRefusal : Exception
    {
        public LinetypeRefusal(string message) : base(message) { }
    }

    /// <summary>
    /// Linetype records in a drawing, read into a <see cref="LinetypeDefinition"/> and
    /// written from one. The one writer: a linetype of that name is changed in place, so
    /// every layer and entity that uses it keeps it; otherwise a new one is added.
    /// Everything runs in the caller's transaction.
    /// </summary>
    public static class LinetypeRecords
    {
        /// <summary>The text scale the old commands measured at.</summary>
        public const double PresetTextScale = 0.9;

        // ------------------------------------------------------------------ reading

        /// <summary>The linetype records' names, the drawing's own (ByLayer, ByBlock, Continuous) left out.</summary>
        public static IReadOnlyList<string> Names(Database db, Transaction tx)
        {
            var table = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            var names = new List<string>();
            foreach (ObjectId id in table)
            {
                var record = (LinetypeTableRecord)tx.GetObject(id, OpenMode.ForRead);
                if (record.IsErased) continue;
                if (LinetypeDefinition.ReservedNames.Any(r => string.Equals(r, record.Name, StringComparison.OrdinalIgnoreCase))) continue;
                names.Add(record.Name);
            }
            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The record named <paramref name="name"/>, or ObjectId.Null.</summary>
        public static ObjectId Find(Database db, Transaction tx, string name)
        {
            var table = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            return table.Has(name) ? table[name] : ObjectId.Null;
        }

        /// <summary>The linetype an entity draws with: its own, or its layer's when it is ByLayer.</summary>
        public static ObjectId LinetypeOf(Entity entity, Transaction tx)
        {
            ObjectId id = entity.LinetypeId;
            if (id == entity.Database.ByLayerLinetype)
                id = ((LayerTableRecord)tx.GetObject(entity.LayerId, OpenMode.ForRead)).LinetypeObjectId;
            return id;
        }

        public static LinetypeDefinition Read(LinetypeTableRecord record, Transaction tx, ILinetypeFlags flags)
        {
            var elements = new List<LinetypeElement>();
            for (int i = 0; i < record.NumDashes; i++)
                elements.Add(new LinetypeElement(record.DashLengthAt(i), ReadInclusion(record, i, tx, flags)));
            return new LinetypeDefinition(record.Name, record.AsciiDescription ?? "", record.IsScaledToFit, elements);
        }

        private static Inclusion ReadInclusion(LinetypeTableRecord record, int index, Transaction tx, ILinetypeFlags flags)
        {
            ObjectId styleId = record.ShapeStyleAt(index);
            if (styleId.IsNull) return Inclusion.None;

            RotationKind kind = flags.IsUprightAt(record, index) ? RotationKind.Upright
                : record.ShapeIsUcsOrientedAt(index) ? RotationKind.Absolute
                : RotationKind.Relative;
            Vector2d offset = record.ShapeOffsetAt(index);
            var placement = new InclusionPlacement(
                record.ShapeScaleAt(index), record.ShapeRotationAt(index), kind, offset.X, offset.Y);

            var style = (TextStyleTableRecord)tx.GetObject(styleId, OpenMode.ForRead);
            string text = TextAt(record, index);
            if (text.Length > 0) return new TextInclusion(text, style.Name, placement);

            int number = record.ShapeNumberAt(index);
            string name = ShapeNames(record.Database, style.FileName).TryGetValue(number, out string found) ? found : "";
            return new ShapeInclusion(number, name, Path.GetFileName(style.FileName), placement);
        }

        /// <summary>A dash's text; a dash with a shape has none (and AutoCAD may throw asking).</summary>
        private static string TextAt(LinetypeTableRecord record, int index)
        {
            try
            {
                return record.TextAt(index) ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        // ------------------------------------------------------------------ writing

        /// <summary>
        /// Writes the definition: changes the linetype of that name in place, or adds one.
        /// Throws LinetypeRefusal, before it changes anything, when the definition cannot be
        /// written as it stands (a style that is missing, an xref's linetype, U= on a host
        /// without it). Returns the record's id and whether it was added.
        /// </summary>
        public static (ObjectId Id, bool Added) Write(Database db, Transaction tx, LinetypeDefinition definition, ILinetypeFlags flags)
        {
            IReadOnlyList<string> problems = definition.Problems();
            if (problems.Count > 0) throw new LinetypeRefusal(string.Join(" ", problems));

            var resolver = new Resolver(db, tx, flags);
            // Resolve every style, shape and flag before the record is touched.
            var checking = new CheckingSink(resolver);
            for (int i = 0; i < definition.Elements.Count; i++) definition.Elements[i].Inclusion.WriteTo(checking, i);

            string name = definition.Name.Trim();
            var table = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            if (table.Has(name))
            {
                var existing = (LinetypeTableRecord)tx.GetObject(table[name], OpenMode.ForRead);
                if (existing.IsDependent)
                    throw new LinetypeRefusal($"{name} comes from an xref: change it in its own drawing.");
                existing.UpgradeOpen();
                Fill(existing, definition, resolver);
                return (existing.ObjectId, false);
            }

            var record = new LinetypeTableRecord { Name = name };
            try
            {
                Fill(record, definition, resolver);
                table.UpgradeOpen();
                ObjectId id = table.Add(record);
                tx.AddNewlyCreatedDBObject(record, true);
                return (id, true);
            }
            catch (Exception)
            {
                if (record.ObjectId.IsNull) record.Dispose();
                throw;
            }
        }

        private static void Fill(LinetypeTableRecord record, LinetypeDefinition definition, Resolver resolver)
        {
            record.AsciiDescription = definition.Description;
            record.IsScaledToFit = definition.ScaledToFit;
            // Fresh dashes: nothing of the old pattern (a style, a text, an upright flag) stays behind.
            record.NumDashes = 0;
            record.NumDashes = definition.Elements.Count;
            var sink = new RecordSink(record, resolver);
            for (int i = 0; i < definition.Elements.Count; i++)
            {
                record.SetDashLengthAt(i, definition.Elements[i].Length);
                definition.Elements[i].Inclusion.WriteTo(sink, i);
            }
            record.PatternLength = definition.PatternLength;
        }

        /// <summary>Writes each dash's inclusion into one record.</summary>
        private sealed class RecordSink : IInclusionSink
        {
            private readonly LinetypeTableRecord m_record;
            private readonly Resolver m_resolver;

            public RecordSink(LinetypeTableRecord record, Resolver resolver)
            {
                m_record = record;
                m_resolver = resolver;
            }

            public void Nothing(int index) { }

            public void Text(int index, TextInclusion text)
            {
                Place(index, m_resolver.TextStyle(text.Style, index), 0, text.Placement);
                m_record.SetTextAt(index, text.Text);
                m_record.SetShapeOffsetAt(index, new Vector2d(text.Placement.X, text.Placement.Y));
            }

            public void Shape(int index, ShapeInclusion shape)
            {
                ObjectId style = m_resolver.ShapeFile(shape.ShapeFile, index);
                Place(index, style, m_resolver.ShapeNumber(shape, style, index), shape.Placement);
                m_record.SetShapeOffsetAt(index, new Vector2d(shape.Placement.X, shape.Placement.Y));
            }

            // The order the old commands found to work: style, number, scale, orientation,
            // rotation, then the text, then the offset.
            private void Place(int index, ObjectId style, int number, InclusionPlacement placement)
            {
                m_record.SetShapeStyleAt(index, style);
                m_record.SetShapeNumberAt(index, number);
                m_record.SetShapeScaleAt(index, placement.Scale);
                m_record.SetShapeIsUcsOrientedAt(index, placement.Kind == RotationKind.Absolute);
                m_resolver.Flags.SetUprightAt(m_record, index, placement.Kind == RotationKind.Upright);
                m_record.SetShapeRotationAt(index, placement.Rotation);
            }
        }

        /// <summary>Finds the drawing's styles and shapes for a definition, once each.</summary>
        private sealed class Resolver
        {
            private readonly Database m_db;
            private readonly Transaction m_tx;
            private readonly Dictionary<string, ObjectId> m_styles = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, ObjectId> m_shapeFiles = new(StringComparer.OrdinalIgnoreCase);

            public Resolver(Database db, Transaction tx, ILinetypeFlags flags)
            {
                m_db = db;
                m_tx = tx;
                Flags = flags;
            }

            public ILinetypeFlags Flags { get; }

            /// <summary>Refuses U= where the host cannot write it.</summary>
            public void Check(InclusionPlacement placement, int index)
            {
                if (placement.Kind == RotationKind.Upright && !Flags.CanUpright)
                    throw new LinetypeRefusal(
                        $"Dash {index + 1} is upright (U=), which this program's API cannot write. Use R= or A=.");
            }

            public ObjectId TextStyle(string name, int index)
            {
                if (m_styles.TryGetValue(name, out ObjectId known)) return known;
                var table = (TextStyleTable)m_tx.GetObject(m_db.TextStyleTableId, OpenMode.ForRead);
                if (!table.Has(name))
                    throw new LinetypeRefusal($"Dash {index + 1}: the text style {name} is not in the drawing.");
                ObjectId id = table[name];
                var style = (TextStyleTableRecord)m_tx.GetObject(id, OpenMode.ForRead);
                if (style.IsShapeFile)
                    throw new LinetypeRefusal($"Dash {index + 1}: {name} is a shape file, not a text style.");
                m_styles[name] = id;
                return id;
            }

            public ObjectId ShapeFile(string file, int index)
            {
                if (m_shapeFiles.TryGetValue(file, out ObjectId known)) return known;
                var table = (TextStyleTable)m_tx.GetObject(m_db.TextStyleTableId, OpenMode.ForRead);
                foreach (ObjectId id in table)
                {
                    var style = (TextStyleTableRecord)m_tx.GetObject(id, OpenMode.ForRead);
                    if (style.IsShapeFile && SameFile(style.FileName, file))
                    {
                        m_shapeFiles[file] = id;
                        return id;
                    }
                }
                throw new LinetypeRefusal(
                    $"Dash {index + 1}: the shape file {file} is not loaded in the drawing. Load it first (LOAD), then write again.");
            }

            public int ShapeNumber(ShapeInclusion shape, ObjectId style, int index)
            {
                if (shape.Number > 0) return shape.Number;
                var record = (TextStyleTableRecord)m_tx.GetObject(style, OpenMode.ForRead);
                foreach (KeyValuePair<int, string> pair in ShapeNames(m_db, record.FileName))
                    if (string.Equals(pair.Value, shape.Name, StringComparison.OrdinalIgnoreCase)) return pair.Key;
                throw new LinetypeRefusal($"Dash {index + 1}: {shape.ShapeFile} has no shape named {shape.Name}.");
            }
        }

        /// <summary>Resolves the styles, shapes and flags of each dash without writing anything.</summary>
        private sealed class CheckingSink : IInclusionSink
        {
            private readonly Resolver m_resolver;

            public CheckingSink(Resolver resolver) => m_resolver = resolver;

            public void Nothing(int index) { }

            public void Text(int index, TextInclusion text)
            {
                m_resolver.TextStyle(text.Style, index);
                m_resolver.Check(text.Placement, index);
            }

            public void Shape(int index, ShapeInclusion shape)
            {
                m_resolver.ShapeNumber(shape, m_resolver.ShapeFile(shape.ShapeFile, index), index);
                m_resolver.Check(shape.Placement, index);
            }
        }

        private static bool SameFile(string styleFile, string file) =>
            string.Equals(Path.GetFileNameWithoutExtension(styleFile), Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase);

        // ------------------------------------------------------------ the drawing's styles

        /// <summary>The drawing's text styles (shape files left out), by name.</summary>
        public static IReadOnlyList<string> TextStyles(Database db, Transaction tx) =>
            Styles(db, tx).Where(s => !s.IsShapeFile).Select(s => s.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>The shape files the drawing has loaded, by file name.</summary>
        public static IReadOnlyList<string> ShapeFiles(Database db, Transaction tx) =>
            Styles(db, tx).Where(s => s.IsShapeFile && s.FileName.Length > 0)
                .Select(s => Path.GetFileName(s.FileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        private static IEnumerable<TextStyleTableRecord> Styles(Database db, Transaction tx)
        {
            var table = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            foreach (ObjectId id in table)
                yield return (TextStyleTableRecord)tx.GetObject(id, OpenMode.ForRead);
        }

        /// <summary>A text style's fixed height, 0 when it has none (then S= is the text's height).</summary>
        public static double TextStyleHeight(Database db, Transaction tx, string style)
        {
            var table = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!table.Has(style)) throw new LinetypeRefusal($"The text style {style} is not in the drawing.");
            return ((TextStyleTableRecord)tx.GetObject(table[style], OpenMode.ForRead)).TextSize;
        }

        /// <summary>
        /// A measure of texts in <paramref name="style"/> at height <paramref name="size"/>: the
        /// width of the text's extents, as the old commands measured it. The style is looked up
        /// now (LinetypeRefusal when missing); the measure itself needs no transaction.
        /// </summary>
        public static Func<string, double> TextWidths(Database db, Transaction tx, string style, double size)
        {
            var table = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!table.Has(style)) throw new LinetypeRefusal($"The text style {style} is not in the drawing.");
            ObjectId id = table[style];
            return text => TextWidth(id, text, size);
        }

        private static double TextWidth(ObjectId style, string text, double size)
        {
            using var measure = new GiTextStyle();
            measure.FromTextStyleTableRecord(style);
            measure.TextSize = size;
            Extents2d box = measure.ExtentsBox(text, true, false, null);
            return box.MaxPoint.X - box.MinPoint.X;
        }

        /// <summary>The shape names of a shape file the host can find; empty when it cannot.</summary>
        public static IReadOnlyDictionary<int, string> ShapeNames(Database db, string file)
        {
            try
            {
                string name = Path.HasExtension(file) ? file : file + ".shx";
                string path = HostApplicationServices.Current.FindFile(name, db, FindFileHint.CompiledShapeFile);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                return ShxShapes.Names(bytes.ToArray());
            }
            catch (Exception)
            {
                return new Dictionary<int, string>();
            }
        }
    }
}
