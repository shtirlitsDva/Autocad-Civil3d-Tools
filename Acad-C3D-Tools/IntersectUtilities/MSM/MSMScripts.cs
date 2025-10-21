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
using System.Collections.Generic; // Added for list/dict helpers
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

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
            DocumentCollection docCol = AcadApp.DocumentManager;
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

        /// <command>PLACEAFGRENING, PAF</command>
        /// <summary>
        /// Indsætter valgte T-blok(ke) orienteret efter tangent på en polyline.
        /// Sekvens: 1) Vælg bloknavn 2) Vælg polyline 3) Vælg punkt.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("PLACEAFGRENING")]
        [CommandMethod("PAF")]
        public void placeAfgrening()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            string[] blockNames = new string[]
            {
                "TEE KDLR","T-TWIN-S2-ISOPLUS","T-TWIN-S3-ISOPLUS","T-TWIN-S2-LOGSTOR","T-TWIN-S3-LOGSTOR",
                "T ENKELT S2","T ENKELT S3","T PARALLEL S3 E","T PARALLEL S3 E VARIABEL"
            };

            // 1) Vælg blok (NUMMERERING i stedet for keywords – undgår mellemrum problem)
            ed.WriteMessage("\nTilgængelige blokke:");
            for (int i = 0; i < blockNames.Length; i++)
                ed.WriteMessage($"\n  {i + 1}. {blockNames[i]}");
            int index = -1;
            while (true)
            {
                var intOpts = new PromptIntegerOptions($"\nIndtast tal (1-{blockNames.Length}) for blok: ")
                { AllowNegative = false, AllowZero = false, LowerLimit = 1, UpperLimit = blockNames.Length };
                var intRes = ed.GetInteger(intOpts);
                if (intRes.Status != PromptStatus.OK) return;
                index = intRes.Value - 1;
                if (index >= 0 && index < blockNames.Length) break;
            }
            string selectedBlock = blockNames[index];

            string libPath = @"X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg";
            if (!File.Exists(libPath)) { prdDbg("Bibliotek ikke fundet: " + libPath); return; }

            // Import block definition upfront to read dynamic properties
            using (var preTx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    db.CheckOrImportBlockRecord(libPath, selectedBlock);
                    preTx.Commit();
                }
                catch (System.Exception ex)
                {
                    prdDbg("Kunne ikke importere blokdefinition: " + ex.Message);
                    preTx.Abort();
                    return;
                }
            }

            // 2) Vælg Type parameter (hvis dynamisk property eller attribut findes)
            string chosenType = string.Empty;
            List<string> allowedTypes = new List<string>();
            using (var txTypes = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = (BlockTable)txTypes.GetObject(db.BlockTableId, OpenMode.ForRead);
                    if (bt.Has(selectedBlock))
                    {
                        var btr = (BlockTableRecord)txTypes.GetObject(bt[selectedBlock], OpenMode.ForRead);
                        using (var tempBr = new BlockReference(Point3d.Origin, btr.ObjectId))
                        {
                            var dynProps = tempBr.DynamicBlockReferencePropertyCollection;
                            foreach (DynamicBlockReferenceProperty prop in dynProps)
                            {
                                if (prop.PropertyName.Equals("Type", StringComparison.OrdinalIgnoreCase))
                                {
                                    var vals = prop.GetAllowedValues();
                                    if (vals != null && vals.Length > 0)
                                        allowedTypes.AddRange(vals.Select(v => v.ToString()));
                                }
                            }
                        }
                        if (allowedTypes.Count == 0)
                        {
                            foreach (ObjectId id in btr)
                            {
                                if (id.ObjectClass.DxfName == "ATTDEF")
                                {
                                    var attDef = (AttributeDefinition)txTypes.GetObject(id, OpenMode.ForRead);
                                    if (attDef.Tag.Equals("TYPE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(attDef.TextString))
                                        allowedTypes.Add(attDef.TextString);
                                }
                            }
                        }
                    }
                    txTypes.Commit();
                }
                catch { txTypes.Abort(); }
            }

            if (allowedTypes.Count > 0)
            {
                string defaultType = allowedTypes.First();
                ed.WriteMessage("\nTilgængelige Type værdier:");
                for (int i = 0; i < allowedTypes.Count; i++) ed.WriteMessage($"\n  {i + 1}. {allowedTypes[i]}");
                int typeIdx = -1;
                while (true)
                {
                    var typeInt = new PromptIntegerOptions($"\nIndtast tal (1-{allowedTypes.Count}) for Type (Enter={defaultType}): ")
                    { AllowNone = true, AllowNegative = false, AllowZero = false, LowerLimit = 1, UpperLimit = allowedTypes.Count };
                    var tRes = ed.GetInteger(typeInt);
                    if (tRes.Status == PromptStatus.None) { chosenType = defaultType; break; }
                    if (tRes.Status != PromptStatus.OK) { chosenType = defaultType; break; }
                    typeIdx = tRes.Value - 1;
                    if (typeIdx >= 0 && typeIdx < allowedTypes.Count) { chosenType = allowedTypes[typeIdx]; break; }
                }
            }
            else
            {
                var strOpt = new PromptStringOptions("\nIndtast Type (Enter springer over): ") { AllowSpaces = true };
                var strRes = ed.GetString(strOpt);
                if (strRes.Status == PromptStatus.OK && strRes.StringResult.IsNotNoE()) chosenType = strRes.StringResult;
            }

            // 3) Vælg polyline
            var peo = new PromptEntityOptions("\nVælg polyline: ");
            peo.SetRejectMessage("\nObjekt er ikke en polyline.");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;
            ObjectId plId = per.ObjectId;

            // 4) Vælg punkt(er) og placer blok(ke)
            while (true)
            {
                var ppo = new PromptPointOptions("\nVælg punkt (ESC stopper): ");
                var ppr = ed.GetPoint(ppo);
                if (ppr.Status != PromptStatus.OK) break;
                Point3d picked = ppr.Value;
                using (var tx = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        db.CheckOrImportBlockRecord(libPath, selectedBlock);
                        Polyline pline = plId.Go<Polyline>(tx);
                        Point3d onPl = pline.GetClosestPointTo(picked, false);
                        double param = pline.GetParameterAtPoint(onPl);
                        Vector3d deriv = pline.GetFirstDerivative(param);
                        if (deriv.Length < 1e-9)
                        {
                            double adjParam = Math.Min(param + 1e-3, pline.EndParam);
                            deriv = pline.GetFirstDerivative(adjParam);
                        }
                        if (deriv.Length < 1e-9) { prdDbg("Kan ikke bestemme tangent – springer."); tx.Abort(); continue; }
                        double rotation = Vector3d.XAxis.GetAngleTo(deriv, Vector3d.ZAxis);
                        var brOid = db.CreateBlockWithAttributes(selectedBlock, onPl, rotation);
                        BlockReference br = brOid;
                        try
                        {
                            var dynProps = br.DynamicBlockReferencePropertyCollection;
                            foreach (DynamicBlockReferenceProperty prop in dynProps)
                            {
                                if (prop.PropertyName.Equals("Type", StringComparison.OrdinalIgnoreCase) && chosenType.IsNotNoE())
                                {
                                    var allowed = prop.GetAllowedValues();
                                    if (allowed == null || allowed.Length == 0 || allowed.Any(v => v.ToString().Equals(chosenType, StringComparison.OrdinalIgnoreCase)))
                                        prop.Value = chosenType;
                                }
                            }
                        }
                        catch { }
                        try { SafeSetAttr(br, "TYPE", chosenType); } catch { }
                        prdDbg($"Indsat {selectedBlock} (Type={chosenType}) @ {onPl.X:0.###},{onPl.Y:0.###} rot {rotation.ToDeg():0.##}°");
                        tx.Commit();
                    }
                    catch (System.Exception ex) { prdDbg(ex.Message); tx.Abort(); }
                }
            }
        }

        // Normaliserer navn (fjerner mellemrum, bindestreger og underscores, gør uppercase)
        private string NormalizeName(string name) => new string(name.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray()).ToUpperInvariant();
        private string ResolveLibraryBlockName(string userName, List<string> libraryNames)
        {
            // Eksakt
            if (libraryNames.Contains(userName)) return userName;
            // Case-insensitive
            var ci = libraryNames.FirstOrDefault(x => x.Equals(userName, StringComparison.OrdinalIgnoreCase));
            if (ci != null) return ci;
            // Normaliseret
            string norm = NormalizeName(userName);
            var normMatch = libraryNames.FirstOrDefault(x => NormalizeName(x) == norm);
            if (normMatch != null) return normMatch;
            // Token baseret (alle tokens skal forekomme)
            var tokens = userName.Split(' ', '-', '_');
            var tokenMatch = libraryNames.FirstOrDefault(x => tokens.All(t => x.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0));
            return tokenMatch;
        }

        // Loader alle bloknavne fra bibliotek (engang pr. kommando-kørsel)
        private List<string> GetLibraryBlockNames(string libPath)
        {
            List<string> names = new();
            using (Database libDb = new Database(false, true))
            {
                libDb.ReadDwgFile(libPath, FileOpenMode.OpenForReadAndAllShare, false, null);
                using (var tx = libDb.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tx.GetObject(libDb.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId id in bt)
                    {
                        var btr = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
                        if (btr.IsLayout) continue; // spring model/paper space
                        if (btr.Name.StartsWith("*")) continue; // anonyme/dynamiske symboler
                        names.Add(btr.Name);
                    }
                    tx.Commit();
                }
            }
            return names;
        }

        private void SafeSetAttr(BlockReference br, string tag, string value)
        {
            try { br.SetAttributeStringValue(tag, value); } catch { }
        }
    }
}
