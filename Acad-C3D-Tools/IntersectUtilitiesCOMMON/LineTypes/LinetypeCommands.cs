#if BRICSCAD
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
#endif

using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.LineTypes
{
    /// <summary>
    /// The bodies of the linetype commands (CCLS, CCLX, CCLXS, UELT, PRINTLT, UELTWS,
    /// UELTNS, CREATEALLLINETYPESLAYERS), shared by every head that registers one. CCL
    /// itself is the editor window (NorsynApplicationsSHARED Linetypes\Editor\), which
    /// does all of these too. No command attribute here: this file compiles into every
    /// project that imports IntersectUtilitiesCOMMON.
    /// </summary>
    public static class LinetypeCommands
    {
        /// <summary>What UELT takes off a layer name to get the linetype's.</summary>
        public const string SampleLayerPrefix = "00LT-";

        /// <summary>
        /// Writes the preset's linetype <paramref name="name"/> drawing <paramref name="text"/>
        /// in <paramref name="style"/>: changes the linetype of that name in place (its layers
        /// and entities keep it), or adds it. A refusal is printed, nothing changes.
        /// </summary>
        public static void Create(Database db, LinetypePreset preset, string name, string text, string style, ILinetypeFlags flags)
        {
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Func<string, double> width = LinetypeRecords.TextWidths(db, tx, style, LinetypeRecords.PresetTextScale);
                    LinetypeDefinition definition = preset.Build(name, text, style, width);
                    LinetypeRecords.Write(db, tx, definition, flags);
                    tx.Commit();
                }
                catch (LinetypeRefusal refusal)
                {
                    tx.Abort();
                    prdDbg(refusal.Message);
                }
                catch (Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
            }
        }

        /// <summary>
        /// UELT: asks for an object on a sample layer (00LT-name) and a text, and writes
        /// linetype name as CCL's text both ways in Standard.
        /// </summary>
        public static void UpdateText(Document doc, ILinetypeFlags flags)
        {
            Editor ed = doc.Editor;
            prdDbg("Text style is presumed \"Standard\"!");
            PromptEntityResult picked = ed.GetEntity("\nSelect object to update linetype: ");
            if (picked.Status != PromptStatus.OK) return;
            PromptStringOptions ask = new PromptStringOptions("\nEnter text: ") { AllowSpaces = true };
            PromptResult typed = ed.GetString(ask);
            if (typed.Status != PromptStatus.OK || string.IsNullOrEmpty(typed.StringResult)) return;

            string layer;
            using (Transaction tx = doc.Database.TransactionManager.StartTransaction())
            {
                layer = ((Entity)tx.GetObject(picked.ObjectId, OpenMode.ForRead)).Layer;
                tx.Commit();
            }
            Create(doc.Database, LinetypePreset.TextBothWays, layer.Replace(SampleLayerPrefix, ""), typed.StringResult, "Standard", flags);
        }

        /// <summary>PRINTLT: asks for an object and prints its linetype as .lin text and dash by dash.</summary>
        public static void Print(Document doc, ILinetypeFlags flags)
        {
            PromptEntityResult picked = doc.Editor.GetEntity("\nSelect object with linetype to print: ");
            if (picked.Status != PromptStatus.OK) return;
            using (Transaction tx = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var entity = (Entity)tx.GetObject(picked.ObjectId, OpenMode.ForRead);
                    var record = (LinetypeTableRecord)tx.GetObject(LinetypeRecords.LinetypeOf(entity, tx), OpenMode.ForRead);
                    LinetypeDefinition definition = LinetypeRecords.Read(record, tx, flags);
                    prdDbg($"=== Linetype: {record.Name} ===");
                    prdDbg(LinFormat.Format(definition));
                    prdDbg($"PatternLength: {record.PatternLength} (the dashes sum to {definition.PatternLength})");
                    prdDbg($"NumDashes: {record.NumDashes}");
                    prdDbg($"IsScaledToFit: {record.IsScaledToFit}");
                    for (int i = 0; i < definition.Elements.Count; i++)
                    {
                        LinetypeElement element = definition.Elements[i];
                        string carries = element.Inclusion.LinText();
                        prdDbg($"--- Dash [{i}] {element.Length}{(carries.Length > 0 ? " " + carries : "")}");
                    }
                    prdDbg("=== End Linetype ===");
                }
                catch (Exception ex)
                {
                    prdDbg(ex);
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// UELTWS and UELTNS: asks for a polyline and rewrites its linetype with the pattern
        /// twice, the words upright in one copy and turned over in the other
        /// (<see cref="LinetypeTransforms.ReadableBothWays"/>).
        /// </summary>
        public static void MakeReadableBothWays(Document doc, ILinetypeFlags flags)
        {
            var options = new PromptEntityOptions("\nSelect polyline with linetype to update: ");
            options.SetRejectMessage("\nMust be a polyline.");
            options.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult picked = doc.Editor.GetEntity(options);
            if (picked.Status != PromptStatus.OK) return;

            Database db = doc.Database;
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var polyline = (Polyline)tx.GetObject(picked.ObjectId, OpenMode.ForRead);
                    var record = (LinetypeTableRecord)tx.GetObject(LinetypeRecords.LinetypeOf(polyline, tx), OpenMode.ForRead);
                    LinetypeDefinition original = LinetypeRecords.Read(record, tx, flags);
                    LinetypeDefinition both = LinetypeTransforms.ReadableBothWays(
                        original, (style, text, size) => LinetypeRecords.TextWidths(db, tx, style, size)(text));
                    prdDbg($"Updating linetype '{original.Name}' - duplicating pattern with U=0 and U=π rotations");
                    prdDbg($"Original pattern has {both.Elements.Count / 2} dashes, new will have {both.Elements.Count}");
                    LinetypeRecords.Write(db, tx, both, flags);
                    tx.Commit();
                }
                catch (LinetypeRefusal refusal)
                {
                    tx.Abort();
                    prdDbg($"ERROR: {refusal.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    tx.Abort();
                    prdDbg($"ERROR: {ex.Message}");
                }
                catch (Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
            }
        }

        /// <summary>
        /// CREATEALLLINETYPESLAYERS: a layer 00LT-name per linetype, set to it, with a
        /// 100 unit line on it and the name beside, one row per linetype down from the origin.
        /// Returns how many rows it drew.
        /// </summary>
        public static int DrawSampleSheet(Database db)
        {
            int rows = 0;
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var linetypes = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                    var layers = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var byName = new Dictionary<string, ObjectId>();
                    foreach (ObjectId id in linetypes)
                        byName.Add(((LinetypeTableRecord)tx.GetObject(id, OpenMode.ForRead)).Name, id);

                    const double startX = 0, endX = 100, step = 5;
                    double y = 0;
                    foreach (KeyValuePair<string, ObjectId> linetype in byName.OrderBy(x => x.Key))
                    {
                        string layerName = SampleLayerPrefix + linetype.Key;
                        if (!layers.Has(layerName)) db.CheckOrCreateLayer(layerName);
                        var layer = (LayerTableRecord)tx.GetObject(layers[layerName], OpenMode.ForWrite);
                        layer.LinetypeObjectId = linetype.Value;

                        var line = new Polyline(2);
                        line.AddVertexAt(line.NumberOfVertices, new Point2d(startX, y), 0, 0, 0);
                        line.AddVertexAt(line.NumberOfVertices, new Point2d(endX, y), 0, 0, 0);
                        line.AddEntityToDbModelSpace(db);
                        line.Layer = layerName;

                        var label = new DBText { Position = new Point3d(-60, y, 0), TextString = linetype.Key };
                        label.AddEntityToDbModelSpace(db);
                        y -= step;
                        rows++;
                    }
                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return 0;
                }
            }
            return rows;
        }
    }
}
