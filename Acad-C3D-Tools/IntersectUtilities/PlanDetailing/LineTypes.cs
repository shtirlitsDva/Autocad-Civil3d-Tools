using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.DataShortcuts;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Dreambuild.AutoCAD;
using IntersectUtilities.PlanDetailing.Components;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.ComponentSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>CCL</command>
        /// <summary>
        /// Creates complex linetype with text. User must type the name of the linetype and the text to be displayed.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CCL")]
        public void createcomplexlinetype()
        {
            string? lineTypeName = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Line type name", "Enter LineType name: (ex: BIPS_TEXT_N2X) \n");
            if (string.IsNullOrEmpty(lineTypeName)) return;
            string? text = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Text", "Enter text to be displayed by line: (ex: 10kV)\n");
            if (string.IsNullOrEmpty(text)) return;
            string textStyleName = "Standard";
            PlanDetailing.LineTypes.LineTypes.createltmethod(lineTypeName, text, textStyleName);
        }

        /// <command>CCLS</command>
        /// <summary>
        /// Creates complex linetype with text. User must type the name of the linetype and the text to be displayed.
        /// Uses single linetype dash for the text.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CCLS")]
        public void createcomplexlinetypesingle()
        {
            string? lineTypeName = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Line type name", "Enter LineType name: (ex: BIPS_TEXT_N2X) \n");
            if (string.IsNullOrEmpty(lineTypeName)) return;
            string? text = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Text", "Enter text to be displayed by line: (ex: 10kV)\n");
            if (string.IsNullOrEmpty(text)) return;
            string textStyleName = "Standard";
            PlanDetailing.LineTypes.LineTypes.createltmethodsingle(lineTypeName, text, textStyleName);
        }

        /// <command>CCLX</command>
        /// <summary>
        /// Creates complex linetype with text and an X. User must type the name of the linetype and the text to be displayed.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CCLX")]
        public void createcomplexlinetypex()
        {
            string? lineTypeName = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Line type name", "Enter LineType name: (ex: BIPS_TEXT_N2X) \n");
            if (string.IsNullOrEmpty(lineTypeName)) return;
            string? text = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Text", "Enter text to be displayed by line: (ex: 10kV)\n");
            if (string.IsNullOrEmpty(text)) return;
            string textStyleName = "Standard";
            PlanDetailing.LineTypes.LineTypes.linetypeX(lineTypeName, text, textStyleName);
        }

        /// <command>CCLXS</command>
        /// <summary>
        /// Creates complex linetype with text and an X. User must type the name of the linetype and the text to be displayed.
        /// Uses single linetype dash for the text.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CCLXS")]
        public void createcomplexlinetypexsingle()
        {
            string? lineTypeName = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Line type name", "Enter LineType name: (ex: BIPS_TEXT_N2X) \n");
            if (string.IsNullOrEmpty(lineTypeName)) return;
            string? text = PlanDetailing.LineTypes.LineTypes.PromptForString(
                "Text", "Enter text to be displayed by line: (ex: 10kV)\n");
            if (string.IsNullOrEmpty(text)) return;
            string textStyleName = "Standard";
            PlanDetailing.LineTypes.LineTypes.linetypeXsingle(lineTypeName, text, textStyleName);
        }

        /// <command>UELT</command>
        /// <summary>
        /// Update existing linetype. User must select object with linetype to be updated and type the text to be displayed.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("UELT")]
        public void updateexistinglinetype()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            prdDbg("Text style is presumed \"Standard\"!");
            Oid id = Interaction.GetEntity("Select object to update linetype: ");
            if (id.IsNull) return;
            string text = Interaction.GetString("Enter text: ", true);
            if (text.IsNoE()) return;
            Transaction tx = db.TransactionManager.StartTransaction();
            using (tx)
            {
                try
                {
                    PlanDetailing.LineTypes.LineTypes.createltmethod(
                        id.Layer().Replace("00LT-", ""), text, "Standard");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
                tx.Commit();
            }
        }
        /// <command>PRINTLT</command>
        /// <summary>
        /// Prints the definition of a linetype for debugging.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("PRINTLT")]
        public void printlinetypedefinition()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\nSelect object with linetype to print: ");
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Entity ent = per.ObjectId.Go<Entity>(tx);
                    Oid linetypeId = ent.LinetypeId;

                    if (linetypeId == db.ByLayerLinetype)
                    {
                        LayerTableRecord ltr = ent.LayerId.Go<LayerTableRecord>(tx);
                        linetypeId = ltr.LinetypeObjectId;
                    }

                    LinetypeTableRecord lttr = linetypeId.Go<LinetypeTableRecord>(tx);
                    PlanDetailing.LineTypes.LineTypes.PrintLinetypeDefinition(lttr);
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                }
                tx.Commit();
            }
        }

        /// <command>UELTWS</command>
        /// <summary>
        /// Update Existing LineType With Symbol. Selects a polyline, extracts its linetype,
        /// and recreates it with upright rotation (U=0 and U=π) for better readability.
        /// Duplicates the original pattern - first copy with text at 0°, second copy with text at 180°.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("UELTWS")]
        public void updateexistinglinetypewithsymbol()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Select polyline
            PromptEntityOptions peo = new PromptEntityOptions("\nSelect polyline with linetype to update: ");
            peo.SetRejectMessage("\nMust be a polyline.");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline pline = per.ObjectId.Go<Polyline>(tx);
                    Oid linetypeId = pline.LinetypeId;

                    // If linetype is ByLayer, get it from the layer
                    if (linetypeId == db.ByLayerLinetype)
                    {
                        LayerTableRecord ltr = pline.LayerId.Go<LayerTableRecord>(tx);
                        linetypeId = ltr.LinetypeObjectId;
                    }

                    LinetypeTableRecord lttr = linetypeId.Go<LinetypeTableRecord>(tx);

                    // Read all linetype info BEFORE committing transaction
                    var linetypeInfo = PlanDetailing.LineTypes.LineTypes.ReadLinetypeInfo(lttr);

                    prdDbg($"Updating linetype '{linetypeInfo.Name}' - duplicating pattern with U=0 and U=π rotations");
                    prdDbg($"Original pattern has {linetypeInfo.Dashes.Count} dashes, new will have {linetypeInfo.Dashes.Count * 2}");

                    tx.Commit();

                    // Call the update method (it manages its own transaction)
                    PlanDetailing.LineTypes.LineTypes.UpdateLinetypeWithSymbolUprightRotation(linetypeInfo);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
            }
        }

        /// <command>UELTNS</command>
        /// <summary>
        /// Update Existing LineType No Symbol. Selects a polyline, extracts its linetype,
        /// and recreates it with upright rotation (U=0 and U=π) for better readability.
        /// Duplicates the original pattern - first copy with text at 0°, second copy with text at 180°.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("UELTNS")]
        public void updateexistinglinetypenosymbol()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Select polyline
            PromptEntityOptions peo = new PromptEntityOptions("\nSelect polyline with linetype to update: ");
            peo.SetRejectMessage("\nMust be a polyline.");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline pline = per.ObjectId.Go<Polyline>(tx);
                    Oid linetypeId = pline.LinetypeId;

                    // If linetype is ByLayer, get it from the layer
                    if (linetypeId == db.ByLayerLinetype)
                    {
                        LayerTableRecord ltr = pline.LayerId.Go<LayerTableRecord>(tx);
                        linetypeId = ltr.LinetypeObjectId;
                    }

                    LinetypeTableRecord lttr = linetypeId.Go<LinetypeTableRecord>(tx);

                    // Read all linetype info BEFORE committing transaction
                    var linetypeInfo = PlanDetailing.LineTypes.LineTypes.ReadLinetypeInfo(lttr);

                    prdDbg($"Updating linetype '{linetypeInfo.Name}' - duplicating pattern with U=0 and U=π rotations");
                    prdDbg($"Original pattern has {linetypeInfo.Dashes.Count} dashes, new will have {linetypeInfo.Dashes.Count * 2}");

                    tx.Commit();

                    // Call the update method (it manages its own transaction)
                    PlanDetailing.LineTypes.LineTypes.UpdateLinetypeNoSymbolUprightRotation(linetypeInfo);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
            }
        }

        /// <command>CREATEALLLINETYPESLAYERS</command>
        /// <summary>
        /// Creates a layer and a polyline for each linetype in drawing with the linetype assigned.
        /// Arranges the polylines in a table-like fashion.
        /// Useful for visualizing linetypes.
        /// </summary>
        /// <category>LineTypes</category>
        [CommandMethod("CREATEALLLINETYPESLAYERS")]
        public void createcalllinetypeslayers()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    TextStyleTable tt = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    // Get the linetype table from the drawing
                    LinetypeTable ltt = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
                    // Get layer table
                    LayerTable lt = tx.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    double startX = 0; double Y = 0; double delta = 5;
                    double endX = 100;
                    var dict = new Dictionary<string, Oid>();
                    foreach (Oid lttrOid in ltt)
                    {
                        LinetypeTableRecord lttr = lttrOid.Go<LinetypeTableRecord>(tx);
                        dict.Add(lttr.Name, lttrOid);
                    }
                    foreach (var kvp in dict.OrderBy(x => x.Key))
                    {
                        LinetypeTableRecord lttr = kvp.Value.Go<LinetypeTableRecord>(tx);
                        string layerName = "00LT-" + lttr.Name;
                        if (!lt.Has(layerName))
                        {
                            db.CheckOrCreateLayer(layerName);
                        }
                        Oid ltrId = lt[layerName];
                        LayerTableRecord ltr = ltrId.Go<LayerTableRecord>(tx, OpenMode.ForWrite);
                        ltr.LinetypeObjectId = kvp.Value;
                        Polyline pline = new Polyline(2);
                        pline.AddVertexAt(pline.NumberOfVertices, new Point2d(startX, Y), 0, 0, 0);
                        pline.AddVertexAt(pline.NumberOfVertices, new Point2d(endX, Y), 0, 0, 0);
                        pline.AddEntityToDbModelSpace(db);
                        pline.Layer = layerName;
                        DBText text = new DBText();
                        text.Position = new Point3d(-60, Y, 0);
                        text.TextString = lttr.Name;
                        text.AddEntityToDbModelSpace(db);
                        Y -= delta;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                }
                tx.Commit();
            }
        }
    }
}
