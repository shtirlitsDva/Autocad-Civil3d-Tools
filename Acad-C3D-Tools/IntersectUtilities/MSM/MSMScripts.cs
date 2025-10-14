using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors; // For color
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry; // For Point3d
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput; // Added for Editor & Prompt* classes
using Autodesk.Civil.DatabaseServices; // Added for ProfileView access
using IntersectUtilities.UtilsCommon;
using System.Linq;
using static IntersectUtilities.UtilsCommon.Utils;
using System; // For Math
using System.IO; // For path checks

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>LABELPROFILEVIEWS</command>
        /// <summary>
        /// This command is numbering each Profile View with a label on the left side of each Profile View. 
        /// The color of the label should be yellow when the Profile View is not yet drawn with profiles or when the profiles needs to be edited. 
        /// The drawer should manually change the color to green when a Profile View is ready with updated profiles.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("LABELPROFILEVIEWS")]
        public void labelprofileviews()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                //Code starts here
                var profileViews = localDb.HashSetOfType<ProfileView>(tx);
                prdDbg($"Antal ProfileViews fundet: {profileViews.Count}");

                // Prepare current space for adding text
                var btr = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);

                // Ensure Arial text style exists
                TextStyleTable tst = (TextStyleTable)tx.GetObject(localDb.TextStyleTableId, OpenMode.ForRead);
                ObjectId arialStyleId = ObjectId.Null;
                const string styleName = "Arial";
                if (tst.Has(styleName))
                {
                    arialStyleId = tst[styleName];
                }
                else
                {
                    tst.UpgradeOpen();
                    TextStyleTableRecord tsRec = new TextStyleTableRecord
                    {
                        Name = styleName,
                        FileName = "arial.ttf"
                    };
                    arialStyleId = tst.Add(tsRec);
                    tx.AddNewlyCreatedDBObject(tsRec, true);
                }

                foreach (var pv in profileViews.OrderBy(x => x.Name))
                {
                    var loc = pv.Location; // Point3d
                    prdDbg($"ProfileView: {pv.Name} | Location: ({loc.X:0.###}, {loc.Y:0.###}, {loc.Z:0.###})");

                    // Insertion point 100 units to the left
                    Point3d insPt = new Point3d(loc.X - 100.0, loc.Y, loc.Z);

                    // Create text with pv name
                    DBText txt = new DBText
                    {
                        Position = insPt,
                        TextString = pv.Name.Replace("_PV", ""),
                        Height = 20,
                        Layer = "0"
                    };

                    if (!arialStyleId.IsNull)
                        txt.TextStyleId = arialStyleId;

                    txt.Color = Color.FromColorIndex(ColorMethod.ByAci, 2); // Yellow

                    btr.AppendEntity(txt);
                    tx.AddNewlyCreatedDBObject(txt, true);
                }
                //Code ends here
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }

            tx.Commit();
        }

        /// <command>PLACEAFGRENING</command>
        /// <summary>
        /// Places and aligns a predefined block from Symboler.dwg on a selected polyline.
        /// Blocks available: T-TWIN-S2-ISOPLUS, T-TWIN-S3-ISOPLUS, T-TWIN-S2-LOGSTOR, T-TWIN-S3-LOGSTOR.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("PAF")]
        public void placeafgrening()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            string symbolLibPath = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";
            if (!File.Exists(symbolLibPath))
            {
                prdDbg($"Block library not found: {symbolLibPath}");
                return;
            }

            string[] blockNames =
            {
                "T-TWIN-S2-ISOPLUS",
                "T-TWIN-S3-ISOPLUS",
                "T-TWIN-S2-LOGSTOR",
                "T-TWIN-S3-LOGSTOR"
            };

            // Prompt for block choice
            PromptKeywordOptions pko = new PromptKeywordOptions("\nChoose block type:");
            foreach (var bn in blockNames) pko.Keywords.Add(bn);
            pko.AllowNone = false;
            var pkRes = ed.GetKeywords(pko);
            if (pkRes.Status != PromptStatus.OK) return;
            string chosenBlockName = pkRes.StringResult;

            using var tx = db.TransactionManager.StartTransaction();
            try
            {
                // Ensure block definition exists locally; import if missing
                BlockTable bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (!bt.Has(chosenBlockName))
                {
                    using Database srcDb = new Database(false, true);
                    srcDb.ReadDwgFile(symbolLibPath, FileOpenMode.OpenForReadAndAllShare, false, null);
                    using Transaction srcTx = srcDb.TransactionManager.StartTransaction();
                    BlockTable srcBt = (BlockTable)srcTx.GetObject(srcDb.BlockTableId, OpenMode.ForRead);
                    if (!srcBt.Has(chosenBlockName))
                    {
                        prdDbg($"Block '{chosenBlockName}' not found in library.");
                        srcTx.Commit();
                        tx.Abort();
                        return;
                    }
                    ObjectIdCollection idsToClone = new ObjectIdCollection { srcBt[chosenBlockName] };
                    IdMapping mapping = new IdMapping();
                    srcDb.WblockCloneObjects(idsToClone, db.BlockTableId, mapping, DuplicateRecordCloning.Ignore, false);
                    srcTx.Commit();
                    // Refresh local block table handle
                    bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                }

                // Select polyline
                PromptEntityOptions peoPl = new PromptEntityOptions("\nSelect polyline to align on: ");
                peoPl.SetRejectMessage("\nNot a polyline.");
                peoPl.AddAllowedClass(typeof(Polyline), true);
                var perPl = ed.GetEntity(peoPl);
                if (perPl.Status != PromptStatus.OK) { tx.Abort(); return; }
                var pline = (Polyline)tx.GetObject(perPl.ObjectId, OpenMode.ForRead);

                // Pick point roughly where to place
                PromptPointOptions ppo = new PromptPointOptions("\nPick point near desired placement along polyline: ");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) { tx.Abort(); return; }
                Point3d picked = ppr.Value;

                // Find closest point and tangent
                Point3d closest = pline.GetClosestPointTo(picked, false);
                double param = pline.GetParameterAtPoint(closest);
                Vector3d deriv = pline.GetFirstDerivative(param);
                if (deriv.Length < 1e-9) deriv = Vector3d.XAxis; // fallback
                double angle = Math.Atan2(deriv.Y, deriv.X);

                // Insert block reference
                bt.UpgradeOpen();
                ObjectId btrId = bt[chosenBlockName];
                BlockTableRecord btrDef = (BlockTableRecord)tx.GetObject(btrId, OpenMode.ForRead);
                BlockTableRecord curSpace = (BlockTableRecord)tx.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                BlockReference newBr = new BlockReference(closest, btrId)
                {
                    Layer = "0",
                    Rotation = angle,
                    ScaleFactors = new Scale3d(1.0)
                };
                curSpace.AppendEntity(newBr);
                tx.AddNewlyCreatedDBObject(newBr, true);

                // Add attribute references (if any)
                foreach (ObjectId id in btrDef)
                {
                    if (!id.IsValid) continue;
                    if (!id.ObjectClass.IsDerivedFrom(RXObject.GetClass(typeof(AttributeDefinition)))) continue;
                    var attDef = (AttributeDefinition)tx.GetObject(id, OpenMode.ForRead);
                    if (attDef == null || attDef.Constant) continue;
                    AttributeReference ar = new AttributeReference();
                    ar.SetAttributeFromBlock(attDef, newBr.BlockTransform);
                    ar.Position = attDef.Position.TransformBy(newBr.BlockTransform);
                    ar.Layer = newBr.Layer;
                    ar.Rotation = angle;
                    curSpace.AppendEntity(ar);
                    tx.AddNewlyCreatedDBObject(ar, true);
                }

                prdDbg($"Inserted '{chosenBlockName}' at ({closest.X:0.###},{closest.Y:0.###}) with rotation {angle * 180.0 / Math.PI:0.##}°");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }
    }
}
