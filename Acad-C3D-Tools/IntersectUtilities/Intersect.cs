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
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;

using Dreambuild.AutoCAD;

using GroupByCluster;

using IntersectUtilities.DynamicBlocks;
using IntersectUtilities.Forms;
using IntersectUtilities.GraphClasses;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;

using Microsoft.Win32;

using QuikGraph;
using QuikGraph.Algorithms.Search;
using QuikGraph.Graphviz;

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.Utils;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

[assembly: CommandClass(typeof(IntersectUtilities.Intersect))]
namespace IntersectUtilities
{
    public partial class Intersect : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                SystemObjects.DynamicLinker.LoadModule(
                    "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
            }
            prdDbg("(❁´◡`❁) (っ °Д °;)っ (●'◡'●)");
            prdDbg($" IntersectUtilites loaded!");
            prdDbg("(❁´◡`❁) (っ °Д °;)っ (●'◡'●)");
            prdDbg();
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
                new ResolveEventHandler(EventHandlers.Debug_AssemblyResolve);
#endif
        }
        public void Terminate()
        {
        }
        #endregion
        private static CultureInfo danishCulture = new CultureInfo("da-DK");
        /// <command>LISTINTLAYCHECKALL</command>
        /// <summary>
        /// Checks if all layers of Polyline3d exist in the Krydsninger.csv file.
        /// </summary>
        /// <category>LER</category>
        [CommandMethod("LISTINTLAYCHECKALL")]
        public void listintlaycheck()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database db = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Select XREF -- OBSOLETE

                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select a LER XREF : ");
                    //promptEntityOptions1.SetRejectMessage("\n Not a XREF");
                    //promptEntityOptions1.AddAllowedClass(typeof(Autodesk.AutoCAD.DatabaseServices.BlockReference), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId blkObjId = entity1.ObjectId;
                    //Autodesk.AutoCAD.DatabaseServices.BlockReference blkRef
                    //    = tx.GetObject(blkObjId, OpenMode.ForRead, false)
                    //    as Autodesk.AutoCAD.DatabaseServices.BlockReference;
                    #endregion

                    #region Open XREF and tx -- OBSOLETE

                    //// open the block definition?
                    //BlockTableRecord blockDef = tx.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    //// is not from external reference, exit
                    //if (!blockDef.IsFromExternalReference) return;
                    //// open the xref database
                    //Database xRefDB = new Database(false, true);
                    //editor.WriteMessage($"\nPathName of the blockDef -> {blockDef.PathName}");
                    ////Relative path handling
                    ////I
                    //string curPathName = blockDef.PathName;
                    //bool isFullPath = IsFullPath(curPathName);
                    //if (isFullPath == false)
                    //{
                    //    string sourcePath = Path.GetDirectoryName(doc.Name);
                    //    editor.WriteMessage($"\nSourcePath -> {sourcePath}");
                    //    curPathName = GetAbsolutePath(sourcePath, blockDef.PathName);
                    //    editor.WriteMessage($"\nTargetPath -> {curPathName}");
                    //}
                    //xRefDB.ReadDwgFile(curPathName, System.IO.FileShare.Read, false, string.Empty);
                    ////Transaction from Database of the Xref
                    //Transaction xrefTx = xRefDB.TransactionManager.StartTransaction();
                    #endregion

                    #region Gather layer names

                    HashSet<Line> lines = db.HashSetOfType<Line>(tx);
                    HashSet<Spline> splines = db.HashSetOfType<Spline>(tx);
                    HashSet<Polyline> plines = db.HashSetOfType<Polyline>(tx);
                    HashSet<Polyline3d> plines3d = db.HashSetOfType<Polyline3d>(tx);
                    HashSet<Arc> arcs = db.HashSetOfType<Arc>(tx);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    editor.WriteMessage($"\nNr. of splines: {splines.Count}");
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    editor.WriteMessage($"\nNr. of plines3d: {plines3d.Count}");
                    editor.WriteMessage($"\nNr. of arcs: {arcs.Count}");
                    HashSet<string> layNames = new HashSet<string>();
                    //Local function to avoid duplicate code
                    HashSet<string> LocalListNames<T>(HashSet<string> list, HashSet<T> ents)
                    {
                        foreach (Entity ent in ents.Cast<Entity>())
                        {
                            list.Add(ent.Layer);
                        }
                        return list;
                    }
                    layNames = LocalListNames(layNames, lines);
                    layNames = LocalListNames(layNames, splines);
                    layNames = LocalListNames(layNames, plines);
                    layNames = LocalListNames(layNames, plines3d);
                    layNames = LocalListNames(layNames, arcs);
                    #endregion

                    #region Read Csv Data for Layers and Depth

                    //Establish the pathnames to files
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";
                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");
                    #endregion

                    foreach (string name in layNames)
                    {
                        string nameInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Navn", 0);
                        if (nameInFile.IsNoE())
                        {
                            editor.WriteMessage($"\nDefinition af ledningslag '{name}' mangler i Krydsninger.csv!");
                        }
                        else
                        {
                            string typeInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Type", 0);
                            if (typeInFile == "IGNORE")
                            {
                                editor.WriteMessage($"\nAdvarsel: Ledningslag" +
                                        $" '{name}' er sat til 'IGNORE' og dermed ignoreres.");
                            }
                            else
                            {
                                string layerInFile = ReadStringParameterFromDataTable(name, dtKrydsninger, "Layer", 0);
                                if (layerInFile.IsNoE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Layer\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");
                                if (typeInFile.IsNoE())
                                    editor.WriteMessage($"\nFejl: Definition af kolonne \"Type\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>CONVERTLINESTOPOLIES</command>
        /// <summary>
        /// Converts all lines in the drawing to polylines.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("CONVERTLINESTOPOLIESPSS")]
        public void convertlinestopoliespss()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Line> lines = localDb.HashSetOfType<Line>(tx);
                    prdDbg($"Nr. of lines: {lines.Count}");

                    foreach (Line line in lines)
                    {
                        Polyline pline = new Polyline(2);

                        pline.AddVertexAt(pline.NumberOfVertices, line.StartPoint.To2d(), 0, 0, 0);
                        pline.AddVertexAt(pline.NumberOfVertices, line.EndPoint.To2d(), 0, 0, 0);
                        pline.AddEntityToDbModelSpace(localDb);

                        pline.Layer = line.Layer;
                        pline.Color = line.Color;

                        PropertySetManager.CopyAllProperties(line, pline);
                    }

                    foreach (Line line in lines)
                    {
                        line.CheckOrOpenForWrite();
                        line.Erase(true);
                    }
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

        /// <command>SELECTBYHANDLE, SBH</command>
        /// <summary>
        /// Selects objects by their handle.
        /// </summary>
        /// <category>Selection</category>
        [CommandMethod("SELECTBYHANDLE")]
        [CommandMethod("SBH")]
        public void selectbyhandle()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptResult pr = editor.GetString("\nEnter handle of object to select: ");
                    if (pr.Status == PromptStatus.OK)
                    {
                        // Convert hexadecimal string to 64-bit integer
                        long ln = Convert.ToInt64(pr.StringResult, 16);
                        // Now create a Handle from the long integer
                        Handle hn = new Handle(ln);
                        // And attempt to get an ObjectId for the Handle
                        Oid id = localDb.GetObjectId(false, hn, 0);
                        // Finally let's open the object and erase it
                        editor.SetImpliedSelection(new[] { id });
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>SELECTBYHANDLEMULTIPLE, SBHM</command>
        /// <summary>
        /// Selects multiple objects by their handles. Separate by space.
        /// </summary>
        /// <category>Selection</category>
        [CommandMethod("SELECTBYHANDLEMULTIPLE")]
        [CommandMethod("SBHM")]
        public void selectbyhandlemultiple()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptStringOptions pso = new PromptStringOptions("\nEnter handles of objects to select (separate by space): ");
                    pso.AllowSpaces = true;
                    PromptResult pr = editor.GetString(pso);
                    if (pr.Status == PromptStatus.OK)
                    {
                        string result = pr.StringResult;
                        string[] handles = result.Split(' ');
                        List<Oid> selection = new List<Oid>();
                        for (int i = 0; i < handles.Length; i++)
                        {
                            string handle = handles[i];
                            // Convert hexadecimal string to 64-bit integer
                            long ln = Convert.ToInt64(handle, 16);
                            // Now create a Handle from the long integer
                            Handle hn = new Handle(ln);
                            // And attempt to get an ObjectId for the Handle
                            Oid id = localDb.GetObjectId(false, hn, 0);
                            selection.Add(id);
                        }
                        editor.SetImpliedSelection(selection.ToArray());
                    }
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

        /// <command>IMPORTCIVILSTYLES</command>
        /// <summary>
        /// Imports Civil 3D styles into the current drawing.
        /// </summary>
        /// <category>Style Management</category>
        [CommandMethod("IMPORTCIVILSTYLES")]
        public void importcivilstyles()
        {
            importcivilstylesmethod();
        }
        public void importcivilstylesmethod(Database db = null)
        {
            try
            {
                DocumentCollection docCol = Application.DocumentManager;
                Database localDb = db ?? docCol.MdiActiveDocument.Database;
                CivilDocument civilDoc = CivilDocument.GetCivilDocument(localDb);
                prdDbg("Importing Norsyn Civil styles...");
                #region Setup styles and clone blocks
                string pathToStyles = @"X:\AutoCAD DRI - 01 Civil 3D\Projection_styles.dwg";
                using (Database stylesDB = new Database(false, true))
                {
                    stylesDB.ReadDwgFile(pathToStyles, FileOpenMode.OpenForReadAndAllShare, false, null);
                    using (Transaction localTx = localDb.TransactionManager.StartTransaction())
                    using (Transaction stylesTx = stylesDB.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            CivilDocument stylesDoc = CivilDocument.GetCivilDocument(stylesDB);
                            ObjectIdCollection objIds = new ObjectIdCollection();
                            //Projection Label Styles
                            LabelStyleCollection stc = stylesDoc.Styles.LabelStyles
                                .ProjectionLabelStyles.ProfileViewProjectionLabelStyles;
                            objIds.Add(stc["PROFILE PROJECTION RIGHT v2"]);
                            objIds.Add(stc["PROFILE PROJECTION LEFT v2"]);
                            //Profile View Style
                            ProfileViewStyleCollection pvsc = stylesDoc.Styles.ProfileViewStyles;
                            objIds.Add(pvsc["PROFILE VIEW L TO R 1:250:100"]);
                            objIds.Add(pvsc["PROFILE VIEW L TO R NO SCALE"]);
                            //Alignment styles
                            var ass = stylesDoc.Styles.AlignmentStyles;
                            objIds.Add(ass["FJV TRACÉ SHOW"]);
                            objIds.Add(ass["FJV TRACE NO SHOW"]);
                            //Alignment label styles
                            var als = stylesDoc.Styles.LabelStyles.AlignmentLabelStyles;
                            objIds.Add(als.MajorStationLabelStyles["Perpendicular with Line"]);
                            objIds.Add(als.MinorStationLabelStyles["Tick"]);
                            objIds.Add(stylesDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"]);
                            //Profile Style
                            var psc = stylesDoc.Styles.ProfileStyles;
                            objIds.Add(psc["PROFIL STYLE MGO KANT"]);
                            objIds.Add(psc["PROFIL STYLE MGO MIDT"]);
                            objIds.Add(psc["Terræn"]);
                            //Profile label styles
                            var plss = stylesDoc.Styles.LabelStyles.ProfileLabelStyles.CurveLabelStyles;
                            objIds.Add(plss["Radius Crest"]);
                            objIds.Add(plss["Radius Sag"]);
                            //Band set style
                            objIds.Add(stylesDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["Elevations and Stations"]);
                            objIds.Add(stylesDoc.Styles.BandStyles.ProfileViewProfileDataBandStyles["TitleBuffer"]);
                            objIds.Add(stylesDoc.Styles.ProfileViewBandSetStyles["EG-FG Elevations and Stations"]);
                            //Matchline styles
                            objIds.Add(stylesDoc.Styles.MatchLineStyles["Basic"]);
                            //Point styles
                            objIds.Add(stylesDoc.Styles.PointStyles["PIL"]);
                            objIds.Add(stylesDoc.Styles.PointStyles["LER KRYDS"]);
                            //Point label styles
                            objIds.Add(stylesDoc.Styles.LabelStyles.PointLabelStyles.LabelStyles["_No labels"]);
                            //Default projection label style
                            objIds.Add(stylesDoc.Styles.LabelStyles.ProjectionLabelStyles
                                .ProfileViewProjectionLabelStyles["PROFILE PROJEKTION MGO"]);
                            //int i = 0;
                            //foreach (Oid oid in objIds)
                            //{
                            //    prdDbg($"{i}: {oid.ToString()}");
                            //    i++;
                            //}
                            //prdDbg("Stylebase.ExportTo() doesn't work!");
                            Autodesk.Civil.DatabaseServices.Styles.StyleBase.ExportTo(
                                objIds, localDb, Autodesk.Civil.StyleConflictResolverType.Override);
                        }
                        catch (System.Exception ex)
                        {
                            stylesTx.Abort();
                            localTx.Abort();
                            prdDbg(ex);
                            throw;
                        }
                        stylesTx.Commit();
                        localTx.Commit();
                    }
                    using (Transaction localTx = localDb.TransactionManager.StartTransaction())
                    using (Transaction stylesTx = stylesDB.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(stylesDB);
                            Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                            ObjectIdCollection idsToClone = new ObjectIdCollection();
                            BlockTable sourceBt = stylesTx.GetObject(stylesDB.BlockTableId, OpenMode.ForRead) as BlockTable;
                            idsToClone.Add(sourceBt["EL 10kV"]);
                            idsToClone.Add(sourceBt["EL 0.4kV"]);
                            idsToClone.Add(sourceBt["VerticeArc"]);
                            idsToClone.Add(sourceBt["VerticeLine"]);
                            IdMapping mapping = new IdMapping();
                            stylesDB.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        }
                        catch (System.Exception e)
                        {
                            stylesTx.Abort();
                            localTx.Abort();
                            prdDbg(e);
                            throw;
                        }
                        stylesTx.Commit();
                        localTx.Commit();
                    }
                }
                #endregion
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                return;
            }
        }

        /// <command>REVEALALIGNMENTS, RAL</command>
        /// <summary>
        /// Reveals hidden alignments in the drawing by changing their style and adding labels.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("REVEALALIGNMENTS")]
        [CommandMethod("RAL")]
        public void revealalignments()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Oid alStyle = civilDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                    Oid labelSetStyle = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyle;
                        al.ImportLabelSet(labelSetStyle);
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>HIDEALIGNMENTS, HAL</command>
        /// <summary>
        /// Hides alignments from the drawing view by changing their style and removing labels.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("HIDEALIGNMENTS")]
        [CommandMethod("HAL")]
        public void hidealignments()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Oid alStyle = civilDoc.Styles.AlignmentStyles["FJV TRACE NO SHOW"];
                    Oid labelSetStyle = civilDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["_No Labels"];
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.StyleId = alStyle;
                        al.ImportLabelSet(labelSetStyle);
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>COPYPSFROMENTTOENT, CPYPS</command>
        /// <summary>
        /// Copies property sets from one entity to another.
        /// </summary>
        /// <category>Property Sets</category>
        [CommandMethod("COPYPSFROMENTTOENT")]
        [CommandMethod("CPYPS")]
        public void copypsfromenttoent()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Select entities

                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect entity FROM where to copy OD:");
                    promptEntityOptions1.SetRejectMessage("\n Not an entity!");
                    promptEntityOptions1.AddAllowedClass(typeof(Entity), false);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId sourceId = entity1.ObjectId;
                    Entity sourceEnt = sourceId.Go<Entity>(tx);
                    PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                        "\nSelect entity where to copy OD TO:");
                    promptEntityOptions2.SetRejectMessage("\n Not an entity!");
                    promptEntityOptions2.AddAllowedClass(typeof(Entity), false);
                    PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    if (((PromptResult)entity2).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Autodesk.AutoCAD.DatabaseServices.ObjectId targetId = entity2.ObjectId;
                    Entity targetEnt = targetId.Go<Entity>(tx, OpenMode.ForWrite);
                    #endregion

                    #region Copy all PSs

                    PropertySetManager.CopyAllProperties(sourceEnt, targetEnt);
                    #endregion

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>DELETEALLALIGNMENTS</command>
        /// <summary>
        /// Deletes all alignments in the drawing.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("DELETEALLALIGNMENTS")]
        public void deleteallalignments()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    editor.WriteMessage($"\nNr. of alignments: {als.Count}");
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>LABELALLALIGNMENTS</command>
        /// <summary>
        /// Adds standard labels (STD 20-5) to all alignments.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("LABELALLALIGNMENTS")]
        public void labelallalignments()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    editor.WriteMessage($"\nNr. of alignments: {als.Count}");
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.ImportLabelSet("STD 20-5");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>LABELALLALIGNMENTSNAME</command>
        /// <summary>
        /// Labels all alignments with their names using a label style with the name of the alignment.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("LABELALLALIGNMENTSNAME")]
        public void labelallalignmentsname()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    editor.WriteMessage($"\nNr. of alignments: {als.Count}");
                    foreach (Alignment al in als)
                    {
                        al.CheckOrOpenForWrite();
                        al.ImportLabelSet("20-5 - Name");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>EXPLODENESTEDBLOCKS</command>
        /// <summary>
        /// Explodes nested blocks using the pick set adding the new elements to the owner space.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("EXPLODENESTEDBLOCKS", CommandFlags.UsePickSet)]
        public static void explodenestedblocks2()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                //PromptEntityOptions prEntOpt = new PromptEntityOptions("\nSelect an INSERT:");
                //prEntOpt.SetRejectMessage("\nIt is not an INSERT!");
                //prEntOpt.AddAllowedClass(typeof(BlockReference), true);
                //PromptEntityResult selRes = ed.GetEntity(prEntOpt);
                // Get the PickFirst selection set
                PromptSelectionResult acSSPrompt;
                acSSPrompt = ed.SelectImplied();
                SelectionSet acSSet;
                // If the prompt status is OK, objects were selected before
                // the command was started
                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    acSSet = acSSPrompt.Value;
                    var Ids = acSSet.GetObjectIds();
                    foreach (Oid oid in Ids)
                    {
                        if (oid.ObjectClass.Name != "AcDbBlockReference") continue;
                        //prdDbg("1: " + oid.ObjectClass.Name);
                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                BlockTableRecord btr = tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                foreach (Oid bOid in btr)
                                {
                                    if (bOid.ObjectClass.Name != "AcDbBlockReference") continue;
                                    //prdDbg("2: " + bOid.ObjectClass.Name);
                                    ObjectIdCollection ids = Extensions.ExplodeToOwnerSpace3(bOid);
                                    if (ids.Count > 0)
                                        ed.WriteMessage("\n{0} entities were added into database.", ids.Count);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                tx.Abort();
                                prdDbg("3: " + ex.Message);
                                continue;
                            }
                            tx.Commit();
                        }
                    }
                }
                else
                {
                    ed.WriteMessage("\nSelect before running the command!");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        /// <command>LISTALLNESTEDBLOCKS</command>
        /// <summary>
        /// Lists all nested blocks in the selection.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("LISTALLNESTEDBLOCKS", CommandFlags.UsePickSet)]
        public static void listallnestedblocks()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                //PromptEntityOptions prEntOpt = new PromptEntityOptions("\nSelect an INSERT:");
                //prEntOpt.SetRejectMessage("\nIt is not an INSERT!");
                //prEntOpt.AddAllowedClass(typeof(BlockReference), true);
                //PromptEntityResult selRes = ed.GetEntity(prEntOpt);
                // Get the PickFirst selection set
                PromptSelectionResult acSSPrompt;
                acSSPrompt = ed.SelectImplied();
                SelectionSet acSSet;
                // If the prompt status is OK, objects were selected before
                // the command was started
                if (acSSPrompt.Status == PromptStatus.OK)
                {
                    acSSet = acSSPrompt.Value;
                    var Ids = acSSet.GetObjectIds();
                    foreach (Oid oid in Ids)
                    {
                        if (oid.ObjectClass.Name != "AcDbBlockReference") continue;
                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                BlockReference br = oid.Go<BlockReference>(tx, OpenMode.ForWrite);
                                BlockTableRecord btr = br.IsDynamicBlock ?
                                    tx.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord :
                                    tx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                                prdDbg("Top LEVEL: " + br.RealName());
                                foreach (Oid bOid in btr)
                                {
                                    if (bOid.ObjectClass.Name != "AcDbBlockReference") continue;
                                    BlockReference nBr = bOid.Go<BlockReference>(tx);
                                    prdDbg(nBr.Name);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                tx.Abort();
                                prdDbg("3: " + ex.Message);
                                continue;
                            }
                            tx.Commit();
                        }
                    }
                }
                else
                {
                    ed.WriteMessage("\nSelect before running command!");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
            void WriteNestedBlocksName(BlockReference brNested)
            {
                Transaction txTop = Application.DocumentManager.MdiActiveDocument.TransactionManager.TopTransaction;
                if (brNested.Name != "MuffeIntern" &&
                    brNested.Name != "MuffeIntern2" &&
                    brNested.Name != "MuffeIntern3")
                {
                    string effectiveName = brNested.IsDynamicBlock ?
                            brNested.Name + " *-> " + ((BlockTableRecord)txTop.GetObject(
                            brNested.DynamicBlockTableRecord, OpenMode.ForRead)).Name : brNested.Name;
                    Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n--> {effectiveName}");
                }
                BlockTableRecord btrNested = txTop.GetObject(brNested.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                foreach (Oid OidNested in btrNested)
                {
                    if (OidNested.ObjectClass.Name != "AcDbBlockReference") continue;
                    WriteNestedBlocksName(OidNested.Go<BlockReference>(txTop));
                }
            }
        }

        public void colorizealllerlayersmethod(Database extDb = null)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Database selectedDB = extDb ?? localDb;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string lagSti = "X:\\AutoCAD DRI - 01 Civil 3D\\Lag.csv";
                    System.Data.DataTable dtLag = CsvReader.ReadCsvToDataTable(lagSti, "Lag");
                    LayerTable lt = selectedDB.LayerTableId.Go<LayerTable>(selectedDB.TransactionManager.TopTransaction);
                    Regex regex = new Regex(@"^(?<R>\d+)\*(?<G>\d+)\*(?<B>\d+)");
                    HashSet<string> layerNames = dtLag.AsEnumerable().Select(x => x["Layer"].ToString()).ToHashSet();
                    foreach (string name in layerNames.Where(x => x.IsNotNoE()).OrderBy(x => x))
                    {
                        if (lt.Has(name))
                        {
                            string colorString = ReadStringParameterFromDataTable(name, dtLag, "Farve", 0);
                            if (colorString.IsNotNoE())
                            {
                                var color = UtilsCommon.Utils.ParseColorString(colorString);
                                prdDbg($"Set layer {name} to color: {color}");
                                LayerTableRecord ltr = lt[name].Go<LayerTableRecord>(selectedDB.TransactionManager.TopTransaction, OpenMode.ForWrite);
                                ltr.Color = color;
                                ltr.LineWeight = LineWeight.LineWeight013;
                            }
                            else prdDbg("No match!");
                        }
                    }
                    //List<string> localLayers = localDb.ListLayers();
                    //foreach (string name in localLayers.Where(x => x.IsNotNoE()).OrderBy(x => x))
                    //{
                    //    if (lt.Has(name))
                    //    {
                    //        string colorString = ReadStringParameterFromDataTable(name, dtKrydsninger, "Farve", 0);
                    //        if (colorString.IsNotNoE() && regex.IsMatch(colorString))
                    //        {
                    //            Match match = regex.Match(colorString);
                    //            byte R = Convert.ToByte(int.Parse(match.Groups["R"].Value));
                    //            byte G = Convert.ToByte(int.Parse(match.Groups["G"].Value));
                    //            byte B = Convert.ToByte(int.Parse(match.Groups["B"].Value));
                    //            prdDbg($"Set layer {name} to color: R: {R.ToString()}, G: {G.ToString()}, B: {B.ToString()}");
                    //            LayerTableRecord ltr = lt[name].Go<LayerTableRecord>(selectedDB.TransactionManager.TopTransaction, OpenMode.ForWrite);
                    //            ltr.Color = Color.FromRgb(R, G, B);
                    //            ltr.LineWeight = LineWeight.LineWeight013;
                    //        }
                    //        else prdDbg("No match!");
                    //    }
                    //}
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>ATTACHAREADATA</command>
        /// <summary>
        /// Attaches area data to selected entities. Used long time ago when the areas were defined in a dwf file.
        /// </summary>
        /// <category>Mængdeudtræk</category>
        [CommandMethod("ATTACHAREADATA")]
        public static void attachareadata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                try
                {
                    #region Dialog box for file list selection and path determination

                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose csv file:",
                        DefaultExt = "csv",
                        Filter = "csv files (*.csv)|*.csv|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        path = dialog.FileName;
                    }
                    else return;
                    #endregion

                    #region Populate area data from string

                    System.Data.DataTable areaDescriptions = CsvReader.ReadCsvToDataTable(path, "Areas");
                    //Datatable to list of strings
                    //List<string> areaNames = (from System.Data.DataRow dr in areaDescriptions.Rows select (string)dr[1]).ToList();
                    //foreach (string name in areaNames)
                    foreach (DataRow row in areaDescriptions.Rows)
                    {
                        //string nummer = row["Nummer"].ToString();
                        //string navn = row["Navn"].ToString();
                        //string vejkl = row["Vejkl"].ToString();
                        //string belægning = row["Belaegning"].ToString();
                        string vejkl = row["Vejklasse"].ToString().Replace("Vejkl. ", "");
                        string belægning = row["Belægning"].ToString();
                        string vejnavn = row["Vejnavn"].ToString();
                        //prdDbg(name);
                        prdDbg($"Vejkl. {vejkl}, {belægning}, {vejnavn}");
                        System.Windows.Forms.Application.DoEvents();
                        #region Select pline

                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect polyline to add data to:");
                        promptEntityOptions1.SetRejectMessage("\nNot a polyline!");
                        promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                        PromptEntityResult entity1 = ed.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                        Oid plineId = entity1.ObjectId;
                        #endregion

                        //string[] split1 = name.Split(new[] { ", " }, StringSplitOptions.None);
                        //split1[1] = split1[1].Replace("Vejkl. ", "");
                        #region Old ownership logic

                        //string ownership = "O";
                        ////Handle the ownership dilemma
                        //if (split1[0].Contains("(P)"))
                        //{
                        //    split1[0] = split1[0].Split(new[] { " (" }, StringSplitOptions.None)[0];
                        //    ownership = "P";
                        //}
                        //string[] data = new string[4] { split1[0], ownership, split1[1], split1[2] }; 
                        #endregion

                        //Test change
                        //if (ownership == "P") prdDbg(data[0] + " " + data[1] + " " + data[2] + " " + data[3]);
                        #region Create layer for OK plines

                        string områderLayer = "0-OMRÅDER-OK";
                        using (Transaction txLag = localDb.TransactionManager.StartTransaction())
                        {
                            LayerTable lt = txLag.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                            if (!lt.Has(områderLayer))
                            {
                                LayerTableRecord ltr = new LayerTableRecord();
                                ltr.Name = områderLayer;
                                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                                ltr.LineWeight = LineWeight.LineWeight000;
                                ltr.IsPlottable = false;
                                //Make layertable writable
                                lt.CheckOrOpenForWrite();
                                //Add the new layer to layer table
                                Oid ltId = lt.Add(ltr);
                                txLag.AddNewlyCreatedDBObject(ltr, true);
                            }
                            txLag.Commit();
                        }
                        #endregion

                        using (Transaction tx = localDb.TransactionManager.StartTransaction())
                        {
                            #region Property Set Manager

                            PropertySetManager psmOmråder =
                                new PropertySetManager(localDb, PSetDefs.DefinedSets.DriOmråder);
                            PSetDefs.DriOmråder driOmråder = new PSetDefs.DriOmråder();
                            #endregion

                            Polyline pline = plineId.Go<Polyline>(tx, OpenMode.ForWrite);
                            psmOmråder.WritePropertyString(pline, driOmråder.Vejnavn, vejnavn);
                            psmOmråder.WritePropertyString(pline, driOmråder.Vejklasse, vejkl);
                            psmOmråder.WritePropertyString(pline, driOmråder.Belægning, belægning);
                            pline.Layer = områderLayer;
                            pline.Color = Color.FromColorIndex(ColorMethod.ByAci, 256);
                            pline.LineWeight = LineWeight.ByLayer;
                            tx.Commit();
                        }
                    }
                    #endregion

                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage(ex.Message + " \nCheck if OK layer exists!");
                    throw;
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        /// <command>SETMODELSPACESCALEFORALL</command>
        /// <summary>
        /// Sets model space scale to 1:250 (but only if it is 1:1000) for all files in a file list.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("SETMODELSPACESCALEFORALL")]
        public void setmodelspacescaleforall()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region ChangeLayerOfXref

                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose txt file:",
                        DefaultExt = "txt",
                        Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        path = dialog.FileName;
                    }
                    else return;
                    List<string> fileList;
                    fileList = File.ReadAllLines(path).ToList();
                    path = Path.GetDirectoryName(path) + "\\";
                    prdDbg(path + "\n");
                    foreach (string name in fileList)
                    {
                        if (name.IsNoE()) continue;
                        //prdDbg(name);
                        string fileName = path + name;
                        //prdDbg(fileName);
                        bool needsSaving = false;
                        editor.WriteMessage("-");
                        using (Database extDb = new Database(false, true))
                        {
                            extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");
                            using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                            {
                                try
                                {
                                    //prdDbg("Values for db.Cannoscale:");
                                    if (extDb.Cannoscale.Name == "1:1000")
                                    {
                                        prdDbg(name);
                                        var ocm = extDb.ObjectContextManager;
                                        var occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");
                                        AnnotationScale aScale = occ.GetContext("1:250") as AnnotationScale;
                                        if (aScale == null)
                                            throw new System.Exception("Annotation scale not found!");
                                        extDb.Cannoscale = aScale;
                                        needsSaving = true;
                                    }
                                }
                                catch (System.Exception)
                                {
                                    extTx.Abort();
                                    throw;
                                }
                                extTx.Commit();
                            }
                            if (needsSaving)
                                extDb.SaveAs(extDb.Filename, DwgVersion.Current);
                        }
                        System.Windows.Forms.Application.DoEvents();
                    }
                    #endregion

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>TURNONREVISION</command>
        /// <summary>
        /// Turns on revision A and Revisionsoverskrifter for all drawings in filelist.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("TURNONREVISION")]
        public void turnonrevision()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Turn on revision layers

                    string path = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose a file:",
                        DefaultExt = "dwg",
                        Filter = "dwg files (*.dwg)|*.dwg|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        path = dialog.FileName;
                    }
                    else return;

                    prdDbg(path);
                    //string fileName = path + name;
                    using (Database extDb = new Database(false, true))
                    {
                        extDb.ReadDwgFile(path, System.IO.FileShare.ReadWrite, false, "");
                        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                        {
                            #region Change xref layer

                            //BlockTable bt = extTx.GetObject(extDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                            //foreach (oid oid in bt)
                            //{
                            //    BlockTableRecord btr = extTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                            //    if (btr.Name.Contains("_alignment"))
                            //    {
                            //        var ids = btr.GetBlockReferenceIds(true, true);
                            //        foreach (oid brId in ids)
                            //        {
                            //            BlockReference br = brId.Go<BlockReference>(extTx, OpenMode.ForWrite);
                            //            prdDbg(br.Name);
                            //            if (br.Layer == "0") { prdDbg("Already in 0! Skipping..."); continue; }
                            //            prdDbg("Was in: :" + br.Layer);
                            //            br.Layer = "0";
                            //            prdDbg("Moved to: " + br.Layer);
                            //            System.Windows.Forms.Application.DoEvents();
                            //        }
                            //    }
                            //} 
                            #endregion

                            #region Change Alignment style

                            //CivilDocument extCDoc = CivilDocument.GetCivilDocument(extDb);
                            //HashSet<Alignment> als = extDb.HashSetOfType<Alignment>(extTx);
                            //foreach (Alignment al in als)
                            //{
                            //    al.CheckOrOpenForWrite();
                            //    al.StyleId = extCDoc.Styles.AlignmentStyles["FJV TRACÉ SHOW"];
                            //    oid labelSetOid = extCDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles["STD 20-5"];
                            //    al.ImportLabelSet(labelSetOid);
                            //} 
                            #endregion

                            try
                            {
                                string revAlayerName = "REV.A";
                                Oid revAlayerId = Oid.Null;
                                string overskriftLayerName = "Revisionsoverskrifter";
                                Oid overskriftLayerId = Oid.Null;
                                LayerTable lt = extTx.GetObject(extDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                                LayerTableRecord revAlayer = lt.GetLayerByName(revAlayerName);
                                prdDbg($"{revAlayer.Name} is hidden: {revAlayer.IsHidden}");
                                prdDbg($"{revAlayer.Name} is frozen: {revAlayer.IsFrozen}");
                                prdDbg($"{revAlayer.Name} is off: {revAlayer.IsOff}");
                                prdDbg("Turning revision on...\n");
                                revAlayer.CheckOrOpenForWrite();
                                revAlayer.IsFrozen = false;
                                revAlayer.IsHidden = false;
                                revAlayer.IsOff = false;
                                var overskriftsLayer = lt.GetLayerByName(overskriftLayerName);
                                overskriftsLayer.CheckOrOpenForWrite();
                                overskriftsLayer.IsFrozen = false;
                                overskriftsLayer.IsHidden = false;
                                overskriftsLayer.IsOff = false;
                            }
                            catch (System.Exception)
                            {
                                extTx.Abort();
                                throw;
                            }
                            extTx.Commit();
                        }
                        extDb.SaveAs(extDb.Filename, DwgVersion.Current);
                    }
                    System.Windows.Forms.Application.DoEvents();
                    #endregion

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>BRINGALLBLOCKSTOFRONT, BF</command>
        /// <summary>
        /// Brings all blocks to the front of the draw order.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("BRINGALLBLOCKSTOFRONT")]
        [CommandMethod("BF")]
        public static void bringallblockstofront()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                SortedList<long, Oid> drawOrder = new SortedList<long, Oid>();
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord btrModelSpace = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                        DrawOrderTable dot = tx.GetObject(btrModelSpace.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;
                        ObjectIdCollection ids = new ObjectIdCollection();
                        foreach (Oid oid in bt)
                        {
                            BlockTableRecord btr = tx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                            foreach (Oid bRefId in btr.GetBlockReferenceIds(true, true))
                            {
                                BlockReference bref = tx.GetObject(bRefId, OpenMode.ForWrite) as BlockReference;
                                if (
                                    bref.Name.StartsWith("*") &&
                                    bref.OwnerId == btrModelSpace.Id
                                    )
                                    ids.Add(bRefId);
                            }
                        }
                        dot.MoveToTop(ids);
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        prdDbg(ex);
                        throw;
                    }
                    tx.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage(ex.Message);
            }
        }

        /// <command>LISTPLINESLAYERS</command>
        /// <summary>
        /// Lists all polylines and their layers.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("LISTPLINESLAYERS")]
        public void listplineslayers()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline> pls = localDb.HashSetOfType<Polyline>(tx);
                    foreach (Polyline pl in pls)
                    {
                        prdDbg($"{pl.Handle.ToString()} -> {pl.Layer}");
                    }
                    HashSet<Line> ls = localDb.HashSetOfType<Line>(tx);
                    foreach (Line l in ls)
                    {
                        prdDbg($"{l.Handle.ToString()} -> {l.Layer}");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>REPLACEBLOCK, RB</command>
        /// <summary>
        /// Replaces specified BBR block with another BBR block.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("REPLACEBLOCK")]
        [CommandMethod("RB")]
        public void replaceblock()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            string[] kwds = new string[9]
            {
                        "Naturgas",
                        "Andet",
                        "UDGÅR",
                        "Ingen",
                        "El",
                        "Olie",
                        "Varmepumpe",
                        "Fjernvarme",
                        "Fast brændsel"
            };
            string result = StringGridFormCaller.Call(kwds, "Vælg type block:");
            if (result.IsNoE()) return;
            while (true)
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        #region Select block

                        PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                            "\nSelect block to replace:");
                        promptEntityOptions1.SetRejectMessage("\n Not a block!");
                        promptEntityOptions1.AddAllowedClass(typeof(BlockReference), true);
                        PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                        if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                        Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;
                        BlockReference brOld = entId.Go<BlockReference>(tx, OpenMode.ForWrite);
                        #endregion

                        BlockReference brNew = localDb.CreateBlockWithAttributes(result, brOld.Position);
                        PropertySetManager.CopyAllProperties(brOld, brNew);
                        PropertySetManager.WriteNonDefinedPropertySetString(brNew, "BBR", "Type", result);
                        brOld.Erase(true);
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        editor.WriteMessage("\n" + ex.ToString());
                        return;
                    }
                    tx.Commit();
                }
            }
        }

        /// <command>REDUCETEXT</command>
        /// <summary>
        /// Reduces the number of text objects.
        /// For use with vejnavne which comes with high text density
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("REDUCETEXT")]
        public void reducetext()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    double tol = 0;
                    PromptDoubleResult result = ed.GetDouble("\nEnter tolerance in meters:");
                    if (((PromptResult)result).Status != PromptStatus.OK) { tx.Abort(); return; }
                    tol = result.Value;
                    HashSet<DBText> dBTexts = localDb.HashSetOfType<DBText>(tx);
                    ObjectIdCollection toDelete = new ObjectIdCollection();
                    HashSet<(Oid id, string text, Point3d position)> allTexts =
                        new HashSet<(Oid id, string text, Point3d position)>();
                    //Cache contents of DBText in memory, i think?
                    foreach (DBText dBText in dBTexts)
                        allTexts.Add((dBText.Id, dBText.TextString, dBText.Position));
                    var groupsWithSimilarText = allTexts.GroupBy(x => x.text);
                    foreach (IGrouping<string, (Oid id, string text, Point3d position)>
                        group in groupsWithSimilarText)
                    {
                        Queue<(Oid id, string text, Point3d position)> qu = new Queue<(Oid, string, Point3d)>();
                        //Load the queue
                        foreach ((Oid id, string text, Point3d position) item in group)
                            qu.Enqueue(item);
                        while (qu.Count > 0)
                        {
                            var labelToTest = qu.Dequeue();
                            if (qu.Count == 0) break;
                            for (int i = 0; i < qu.Count; i++)
                            {
                                var curLabel = qu.Dequeue();
                                if (labelToTest.position.DistanceHorizontalTo(curLabel.position) < tol)
                                    toDelete.Add(curLabel.id);
                                else qu.Enqueue(curLabel);
                            }
                        }
                    }
                    //Delete the chosen labels
                    foreach (Oid oid in toDelete)
                    {
                        DBText ent = oid.Go<DBText>(tx, OpenMode.ForWrite);
                        ent.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

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

        /// <command>PLACEOBJLAYCOLOR</command>
        /// <summary>
        /// Creates a text object with the color and linetype of the selected object.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("PLACEOBJLAYCOLOR")]
        public void placeobjlaycolor()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Oid oid = Interaction.GetEntity("Select object to write color of: ", typeof(Entity), false);
            if (oid == Oid.Null) return;
            Point3d location = Interaction.GetPoint("Select where to place the text: ");
            if (location.IsNull()) return;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Entity ent = oid.Go<Entity>(tx);
                    LayerTableRecord ltr = ent.LayerId.Go<LayerTableRecord>(tx);
                    LinetypeTableRecord lttr = ltr.LinetypeObjectId.Go<LinetypeTableRecord>(tx);
                    DBText text = new DBText();
                    text.Position = location;
                    text.TextString = $"{ltr.Color} {lttr.Name}";
                    text.Height = 0.5;
                    text.AddEntityToDbModelSpace(localDb);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>DRAWVIEWPORTRECTANGLES</command>
        /// <summary>
        /// Draws viewports in model space.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("DRAWVIEWPORTRECTANGLES")]
        public void drawviewportrectangles()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            localDb.CheckOrCreateLayer("0-NONPLOT", 40, false);
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Paperspace to modelspace test
                    DBDictionary layoutDict = localDb.LayoutDictionaryId.Go<DBDictionary>(tx);
                    var enumerator = layoutDict.GetEnumerator();
                    while (enumerator.MoveNext())
                    {
                        DBDictionaryEntry item = enumerator.Current;
                        prdDbg(item.Key);
                        if (item.Key == "Model")
                        {
                            prdDbg("Skipping model...");
                            continue;
                        }
                        Layout layout = item.Value.Go<Layout>(tx);
                        //ObjectIdCollection vpIds = layout.GetViewports();
                        BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(tx);
                        foreach (Oid id in layBlock)
                        {
                            if (id.IsDerivedFrom<Viewport>())
                            {
                                Viewport viewport = id.Go<Viewport>(tx);
                                prdDbg("Found viewport!");
                                Extents3d ext = viewport.GeometricExtents;
                                Polyline pl = new Polyline(4);
                                pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                                pl.AddVertexAt(1, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                                pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                                pl.AddVertexAt(3, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                                pl.Closed = true;
                                pl.SetDatabaseDefaults();
                                pl.PaperToModel(viewport);
                                pl.Layer = "0-NONPLOT";
                                pl.AddEntityToDbModelSpace<Polyline>(localDb);
                            }
                        }
                    }
                    #endregion

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

        /// <command>CREATE3DTRACEFROMBUNDPROFILE</command>
        /// <summary>
        /// Creates a 3D Polyline from the XXX BUND profile in a separate file.
        /// Used for excavations. Must be run in Længdeprofiler file.
        /// PLEASE NOTE!!! The polyline3d is created from BUND profile, so you have to add
        /// depth to the elevations if you want the trench bottom.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("CREATE3DTRACEFROMBUNDPROFILE")]
        public void create3dtracefrombundprofile()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            string dbFilename = localDb.OriginalFileName;
            string fileName = Path.GetFileNameWithoutExtension(dbFilename);
            string path = Path.GetDirectoryName(dbFilename);
            string poly3dExportFile = path + "\\" + fileName + "_3D_Bundprofil.dwg";
            Database poly3dDb = new Database(true, true);
            using (Transaction poly3dTx = poly3dDb.TransactionManager.StartTransaction())
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        var pIds = al.GetProfileIds();
                        Profile bundProfile = null;
                        foreach (Oid oid in pIds)
                            if (oid.Go<Profile>(tx).Name.Contains("BUND")) bundProfile = oid.Go<Profile>(tx);
                        if (bundProfile == null)
                        {
                            prdDbg($"Bund profile could not be found for alignment {al.Name}!");
                            continue;
                        }
                        double alLength = al.Length;
                        double step = 0.1;
                        int nrOfSteps = (int)(alLength / step);
                        Point3dCollection p3ds = new Point3dCollection();
                        for (int i = 0; i < nrOfSteps; i++)
                        {
                            double curStation = i * step;
                            double X = 0;
                            double Y = 0;
                            try
                            {
                                al.PointLocation(curStation, 0, ref X, ref Y);
                            }
                            catch (System.Exception ex)
                            {
                                prdDbg("Problem reading al.PointLocation for " + al.Name);
                                throw;
                            }
                            double Z = 0;
                            try
                            {
                                Z = bundProfile.ElevationAt(curStation);
                            }
                            catch (System.Exception)
                            {
                                prdDbg($"Elevation sampling failed at alignment {al.Name}, station {curStation}.");
                            }
                            if (Z == 0) continue;
                            p3ds.Add(new Point3d(X, Y, Z));
                        }
                        Polyline3d pline3d = new Polyline3d(Poly3dType.SimplePoly, p3ds, false);
                        pline3d.AddEntityToDbModelSpace(localDb);
                        HashSet<int> verticesToRemove = new HashSet<int>();
                        PolylineVertex3d[] vertices = pline3d.GetVertices(tx);
                        for (int i = 0; i < vertices.Length - 2; i++)
                        {
                            PolylineVertex3d vertex1 = vertices[i];
                            PolylineVertex3d vertex2 = vertices[i + 1];
                            PolylineVertex3d vertex3 = vertices[i + 2];
                            Vector3d vec1 = vertex1.Position.GetVectorTo(vertex2.Position);
                            Vector3d vec2 = vertex2.Position.GetVectorTo(vertex3.Position);
                            if (vec1.IsCodirectionalTo(vec2, Tolerance.Global)) verticesToRemove.Add(i + 1);
                        }
                        Point3dCollection p3dsClean = new Point3dCollection();
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            if (verticesToRemove.Contains(i)) continue;
                            PolylineVertex3d v = vertices[i];
                            p3dsClean.Add(v.Position);
                        }
                        Polyline3d nyPline = new Polyline3d(Poly3dType.SimplePoly, p3dsClean, false);
                        nyPline.AddEntityToDbModelSpace(poly3dDb);
                        pline3d.CheckOrOpenForWrite();
                        pline3d.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    poly3dTx.Abort();
                    poly3dDb.Dispose();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
                poly3dTx.Commit();
                poly3dDb.SaveAs(poly3dExportFile, DwgVersion.Newest);
                poly3dDb.Dispose();
            }
        }
        /// <command>CONSTRUCTIONLINESETMARK</command>
        /// <summary>
        /// Adds a string to a Line in FlexDataStore to mark it as a construction line.
        /// This is used for internal purposes when creating dynamic blocks.
        /// </summary>
        /// <category>Development</category>
        [CommandMethod("CONSTRUCTIONLINESETMARK")]
        public void constructionlinesetmark()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptEntityOptions peo = new PromptEntityOptions("Select construction line: ");
                    peo.SetRejectMessage("Selected entity is not a Line!");
                    peo.AddAllowedClass(typeof(Line), true);
                    PromptEntityResult per = editor.GetEntity(peo);
                    Oid lineId = per.ObjectId;
                    if (lineId == Oid.Null) { tx.Abort(); return; }
                    FlexDataStore fds = lineId.FlexDataStore(true);
                    string value = fds.GetValue("IsConstructionLine");
                    if (value.IsNoE()) fds.SetValue("IsConstructionLine", "True");
                    else prdDbg("Construction line already marked!");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>CONSTRUCTIONLINEREMOVEMARK</command>
        /// <summary>
        /// Removes the string from a Line in FlexDataStore that marks it as a construction line.
        /// This is used for internal purposes when creating dynamic blocks.
        /// </summary>
        /// <category>Development</category>
        [CommandMethod("CONSTRUCTIONLINEREMOVEMARK")]
        public void constructionlineremovemark()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PromptEntityOptions peo = new PromptEntityOptions("Select construction line: ");
                    peo.SetRejectMessage("Selected entity is not a Line!");
                    peo.AddAllowedClass(typeof(Line), true);
                    PromptEntityResult per = editor.GetEntity(peo);
                    Oid lineId = per.ObjectId;
                    if (lineId == Oid.Null) { tx.Abort(); return; }
                    FlexDataStore fds = lineId.FlexDataStore(false);
                    if (fds == null) { tx.Abort(); return; }
                    string value = fds.GetValue("IsConstructionLine");
                    if (value.IsNotNoE()) fds.RemoveEntry("IsConstructionLine");
                    else prdDbg("Construction line mark already removed!");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        public bool IsConstructionLine(Oid id)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    if (id == Oid.Null || !id.IsDerivedFrom<Line>()) { tx.Abort(); return false; }
                    FlexDataStore fds = id.FlexDataStore(false);
                    if (fds == null) { tx.Abort(); return false; }
                    string value = fds.GetValue("IsConstructionLine");
                    if (value.IsNoE()) { tx.Abort(); return false; }
                    if (value == "True") { tx.Abort(); return true; }
                    else { tx.Abort(); return false; }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return false;
                }
            }
        }

        /// <command>CLEANPLINE</command>
        /// <summary>
        /// Removes colinear and coincident vertices from a selected polyline.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("CLEANPLINE")]
        public void cleanpline()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Remove colinear vertices

                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect polyline to clean:");
                    promptEntityOptions1.SetRejectMessage("\n Not a polyline!");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                    PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    Autodesk.AutoCAD.DatabaseServices.ObjectId plineId = entity1.ObjectId;
                    Polyline pline = plineId.Go<Polyline>(tx);
                    RemoveColinearVerticesPolyline(pline);
                    #endregion

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>CLEANPLINES</command>
        /// <summary>
        /// Removes colinear and coincident vertices from all polylines.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("CLEANPLINES")]
        public void cleanplines()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var plines = localDb.HashSetOfType<Polyline>(tx);
                    int guiltyPlinesCount = 0;
                    int removedVerticesCount = 0;
                    foreach (Polyline pline in plines)
                    {
                        RemoveColinearVerticesPolyline(
                            pline, ref guiltyPlinesCount, ref removedVerticesCount);
                    }
                    prdDbg(
                        $"Found {guiltyPlinesCount} guilty plines and " +
                        $"removed {removedVerticesCount} vertices.");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>REMOVEVEJFROMALIGNMENTNAME</command>
        /// <summary>
        /// Used for removing street names from alignment names.
        /// Was used when migrating projects to new standard (no street names in alignment names).
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("REMOVEVEJFROMALIGNMENTNAME")]
        public void removevejfromalignmentname()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Regex rgx = new Regex(@"(?<number>\d\d\d?)\s");
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment al in als)
                    {
                        if (rgx.IsMatch(al.Name))
                        {
                            string number = rgx.Match(al.Name).Groups["number"].Value;
                            al.CheckOrOpenForWrite();
                            al.Name = number;
                            prdDbg($"{al.Name} -> {number}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }
        public void createpropertysetsfromodtables()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.oddatacreatepropertysetsdefs();
        }
        public void attachodtablepropertysets()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.attachpropertysetstoobjects();
        }
        public void populatepropertysetswithoddata()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.populatepropertysetswithoddata();
        }

        /// <command>CONVERTODTOPSS</command>
        /// <summary>
        /// Used to convert ObjectData tables to Property Sets.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("CONVERTODTOPSS")]
        public void convertodtopss()
        {
            IntersectUtilities.ODDataConverter.ODDataConverter.oddatacreatepropertysetsdefs();
            IntersectUtilities.ODDataConverter.ODDataConverter.attachpropertysetstoobjects();
            IntersectUtilities.ODDataConverter.ODDataConverter.populatepropertysetswithoddata();
        }

        /// <command>SELECTBYPS</command>
        /// <summary>
        /// Selects entities by property set, property and a value.
        /// </summary>
        /// <category>Selection</category>
        [CommandMethod("SELECTBYPS")]
        public void selectbyps()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            var kwd = StringGridFormCaller.Call(
                ["Exact", "Contains"], "Exact or contains match: ");
            if (kwd == null) return;
            PropertySetManager.MatchTypeEnum matchType;
            if (!Enum.TryParse(kwd, out matchType)) return;

            string valueToFind;
            PromptStringOptions opts3 = new PromptStringOptions("\nEnter data to search: ");
            opts3.AllowSpaces = true;
            PromptResult pr3 = editor.GetString(opts3);
            if (pr3.Status != PromptStatus.OK) return;
            else valueToFind = pr3.StringResult;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    prdDbg("Frozen entities are discarded!");
                    editor.SetImpliedSelection(
                        PropertySetManager.SelectByPsValue(
                            localDb, matchType, valueToFind));
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>SELECTBYPSALL</command>
        /// <summary>
        /// Selects all entities by property set and property.
        /// </summary>
        /// <category>Selection</category>
        [CommandMethod("SELECTBYPSALL")]
        public void selectbypsall()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            prdDbg("Frozen entities are discarded!");
            HashSet<string> psNames = PropertySetManager.GetPropertySetNames(localDb);
            var propertySetName = StringGridFormCaller.Call(psNames.OrderBy(x => x), "Select Property Set: ");
            if (propertySetName.IsNoE()) return;
            var pNames = PropertySetManager.GetPropertyNamesAndDataTypes(localDb, propertySetName);
            var propertyName = StringGridFormCaller.Call(pNames.Select(x => x.Key).OrderBy(x => x), "Select Property: ");
            if (propertyName.IsNoE()) return;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.HashSetOfType<Entity>(tx, true);
                    HashSet<string> values = new HashSet<string>();
                    foreach (var ent in ents)
                        if (PropertySetManager.TryReadNonDefinedPropertySetObject(
                            ent, propertySetName, propertyName, out object result))
                            values.Add(result.ToString());
                    var valueToFind = StringGridFormCaller.Call(values.OrderBy(x => x), "Select Value: ");
                    HashSet<Entity> entsToSelect = new HashSet<Entity>();
                    foreach (var ent in ents)
                    {
                        if (PropertySetManager.TryReadNonDefinedPropertySetObject(
                            ent, propertySetName, propertyName, out object result))
                        {
                            if (result == null && valueToFind == null) entsToSelect.Add(ent);
                            else if (result.ToString() == valueToFind) entsToSelect.Add(ent);
                        }
                    }
                    if (entsToSelect.Count > 0)
                        editor.SetImpliedSelection(entsToSelect.Select(x => x.Id).ToArray());
                    else prdDbg("No entities found!");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>SELECTBYPSET</command>
        /// <summary>
        /// Selects all entities by property set.
        /// </summary>
        /// <category>Selection</category>
        [CommandMethod("SELECTBYPSET")]
        public void selectbypset()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            prdDbg("Frozen entities are discarded!");
            HashSet<string> psNames = PropertySetManager.GetPropertySetNames(localDb);
            var propertySetName = StringGridFormCaller.Call(psNames.OrderBy(x => x), "Select Property Set: ");
            if (propertySetName.IsNoE()) return;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.HashSetOfType<Entity>(tx, true);
                    HashSet<Entity> entsToSelect = new HashSet<Entity>();
                    foreach (var ent in ents)
                        if (PropertySetManager.IsPropertySetAttached(ent, propertySetName))
                            entsToSelect.Add(ent);
                    if (entsToSelect.Count > 0)
                        editor.SetImpliedSelection(entsToSelect.Select(x => x.Id).ToArray());
                    else prdDbg("No entities found!");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>LISTUNIQUEPSDATA</command>
        /// <summary>
        /// Lists all unique values of a property in a property set.
        /// </summary>
        /// <category>Property Sets</category>
        [CommandMethod("LISTUNIQUEPSDATA")]
        public void listuniquepsdata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            prdDbg("Frozen entities are discarded!");
            HashSet<string> psNames = PropertySetManager.GetPropertySetNames(localDb);
            var propertySetName = StringGridFormCaller.Call(psNames.OrderBy(x => x), "Select Property Set: ");
            if (propertySetName.IsNoE()) return;
            var pNames = PropertySetManager.GetPropertyNamesAndDataTypes(localDb, propertySetName);
            var propertyName = StringGridFormCaller.Call(pNames.Select(x => x.Key).OrderBy(x => x), "Select Property: ");
            if (propertyName.IsNoE()) return;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var ents = localDb.HashSetOfType<Entity>(tx, true);
                    HashSet<string> values = new HashSet<string>();
                    foreach (var ent in ents)
                        if (PropertySetManager.TryReadNonDefinedPropertySetObject(
                            ent, propertySetName, propertyName, out object result))
                            values.Add(result.ToString());
                    prdDbg($"Unique values for {propertySetName} - {propertyName}:");
                    prdDbg(string.Join("\n", values.OrderBy(x => x)));
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>DIVIDEPLINE</command>
        /// <summary>
        /// Divides a polyline into equal segments.
        /// </summary>
        /// <category>Polylines</category>
        [CommandMethod("DIVIDEPLINE")]
        public void dividepline()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            Oid pId = Interaction.GetEntity("Select pline to divide: ", typeof(Polyline));
            int nrOfSegments = Interaction.GetInteger("Enter number of segments to create: ");
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline originalPline = pId.Go<Polyline>(tx);
                    double length = originalPline.Length;
                    double segmentLength = length / nrOfSegments;
                    for (int i = 0; i < nrOfSegments; i++)
                    {
                        double startLength = i * segmentLength;
                        double endLength = (i + 1) * segmentLength;
                        Polyline newPline = new Polyline(2);
                        newPline.AddVertexAt(0,
                            originalPline.GetPointAtDist(startLength).To2d(), 0, 0, 0);
                        newPline.AddVertexAt(1,
                            originalPline.GetPointAtDist(endLength).To2d(), 0, 0, 0);
                        newPline.AddEntityToDbModelSpace(localDb);
                        newPline.Layer = originalPline.Layer;
                        //PropertySetManager.CopyAllProperties(originalPline, newPline);
                    }
                    originalPline.CheckOrOpenForWrite();
                    originalPline.Erase(true);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>PIPELAYERSCOLOURSET</command>
        /// <summary>
        /// Sets the color of pipe layers.
        /// Usually used in dimensioning drawings to mark sizes with assigned colors.
        /// </summary>
        /// <category>Dimensioning</category>
        [CommandMethod("PIPELAYERSCOLOURSET")]
        public void pipelayerscolourset()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);
                    List<LayerTableRecord> pipeLtrs = new List<LayerTableRecord>();
                    foreach (var ltrid in lt)
                    {
                        LayerTableRecord ltr = ltrid.Go<LayerTableRecord>(tx);
                        if (GetPipeDN(ltr.Name) != 0) pipeLtrs.Add(ltr);
                    }
                    pipeLtrs = pipeLtrs
                        .OrderBy(x => GetSystemString(x.Name))
                        .ThenBy(x => GetPipeType(x.Name))
                        .ThenBy(x => GetPipeDN(x.Name))
                        .ToList();
                    prdDbg($"Number of pipe layers in drawing: {pipeLtrs.Count}");
                    //List<Color> colors = new List<Color>();
                    #region HSL H stepping method, produces many similar-looking colors
                    //Random random = new Random();
                    //for (int h = 0; h < 360; h += 360 / pipeLtrs.Count)
                    //{
                    //    double hue = h;
                    //    double saturation = (90.0 + random.Next(0, 11)) / 100.0;
                    //    //double saturation = random.NextDouble();
                    //    //double lightness = random.NextDouble();
                    //    double lightness = (25.0 + random.Next(0, 36)) / 100.0;
                    //    prdDbg($"HSL: {hue}, {saturation}, {lightness}");
                    //    System.Drawing.Color color = ColorUtils.SimpleColorTransforms.HsLtoRgb(hue, saturation, lightness);
                    //    prdDbg(color.ToString());
                    //    colors.Add(Color.FromRgb(color.R, color.G, color.B));
                    //} 
                    #endregion
                    //Old method to assign colors
                    //if (pipeLtrs.Count > ColorUtils.StaticColorList.StaticColors.Count)
                    //    throw new System.Exception($"There are {pipeLtrs.Count} layers and only " +
                    //        $"{ColorUtils.StaticColorList.StaticColors.Count} colours!");
                    //for (int j = 0; j < pipeLtrs.Count; j++)
                    //{
                    //    string colorString = ColorUtils.StaticColorList.StaticColors[j];
                    //    System.Drawing.Color color = System.Drawing.ColorTranslator.FromHtml(colorString);
                    //    colors.Add(Color.FromRgb(color.R, color.G, color.B));
                    //}
                    //var zip = pipeLtrs.Zip(colors, (x, y) => new { layer = x, color = y });
                    #region Determine upper right corner of all polylines: Point3d UpperRightCorner
                    //To place legend find bbox of all plines and place legend at the top right corner
                    HashSet<Polyline> allPlines = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => GetPipeDN(x) != 0).ToHashSet();
                    //Determine maximum points
                    HashSet<double> Xs = new HashSet<double>();
                    HashSet<double> Ys = new HashSet<double>();
                    foreach (var pl in allPlines)
                    {
                        Extents3d bbox = pl.GeometricExtents;
                        Xs.Add(bbox.MaxPoint.X);
                        Xs.Add(bbox.MinPoint.X);
                        Ys.Add(bbox.MaxPoint.Y);
                        Ys.Add(bbox.MinPoint.Y);
                    }
                    double maxX = Xs.Max();
                    double maxY = Ys.Max();
                    Point3d upperRightCorner = new Point3d(maxX, maxY, 0);
                    #endregion
                    //Assign colors and create legend
                    int i = 0;
                    foreach (var layer in pipeLtrs)
                    {
                        //Determine color
                        Color color = Color.FromColorIndex(ColorMethod.ByAci,
                            GetColorForDim(layer.Name));
                        layer.CheckOrOpenForWrite();
                        layer.Color = color;
                        //Create legend
                        double vDist = 10;
                        Point3d p1 = new Point3d(upperRightCorner.X, upperRightCorner.Y + vDist * i, 0.0);
                        Point3d p2 = new Point3d(upperRightCorner.X + 50.0, upperRightCorner.Y + vDist * i, 0.0);
                        Line line = new Line(p1, p2);
                        line.AddEntityToDbModelSpace(localDb);
                        line.SetDatabaseDefaults();
                        line.Layer = "0";
                        line.Color = color;
                        DBText text = new DBText();
                        text.AddEntityToDbModelSpace(localDb);
                        text.SetDatabaseDefaults();
                        text.Position = new Point3d(p2.X + 10.0, p2.Y, 0.0);
                        text.Height = 5;
                        text.Color = color;
                        text.TextString =
                            $"{GetSystemString(layer.Name)}{GetPipeDN(layer.Name)}-" +
                            $"{GetPipeType(layer.Name)}";
                        i--;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>PIPELAYERSCOLOURRESET</command>
        /// <summary>
        /// Resets the color of pipe layers.
        /// Usually used in dimensioning drawings to reset colors.
        /// </summary>
        /// <category>Dimensioning</category>
        [CommandMethod("PIPELAYERSCOLOURRESET")]
        public void pipelayerscolourreset()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);
                    List<LayerTableRecord> pipeLtrs = new List<LayerTableRecord>();
                    foreach (var ltrid in lt)
                    {
                        LayerTableRecord ltr = ltrid.Go<LayerTableRecord>(tx);
                        if (GetPipeDN(ltr.Name) != 0) pipeLtrs.Add(ltr);
                    }
                    pipeLtrs = pipeLtrs.OrderBy(x => x.Name).ToList();
                    prdDbg($"Number of pipe layers in drawing: {pipeLtrs.Count}");
                    //Assign colors and create legend
                    foreach (var ltr in pipeLtrs)
                    {
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci,
                            GetLayerColor(GetPipeSystem(ltr.Name), GetPipeType(ltr.Name)));
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>FIXLERLAYERS</command>
        /// <summary>
        /// Assigns correct linetypes and colors to Ler polylines(3d).
        /// </summary>
        /// <category>LER</category>
        [CommandMethod("FIXLERLAYERS")]
        public void fixlerlayers()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            fixlerlayersmethod(localDb);
        }
        public void fixlerlayersmethod(Database localDb)
        {
            string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
            string pathLag = "X:\\AutoCAD DRI - 01 Civil 3D\\Lag.csv";
            System.Data.DataTable dtK = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
            System.Data.DataTable dtLag = CsvReader.ReadCsvToDataTable(pathLag, "Lag");
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(localDb.TransactionManager.TopTransaction);
                    //Cache layers in memory
                    HashSet<LayerTableRecord> layers = new HashSet<LayerTableRecord>();
                    foreach (Oid lid in lt) layers.Add(lid.Go<LayerTableRecord>(tx));
                    #region Prepare linetypes
                    LinetypeTable ltt = (LinetypeTable)localDb.TransactionManager.TopTransaction
                            .GetObject(localDb.LinetypeTableId, OpenMode.ForWrite);
                    //Lookup linetype specification and link to layer name
                    Dictionary<string, string> layerLineTypeMap = new Dictionary<string, string>();
                    foreach (var layer in layers)
                    {
                        string targetLayerName = ReadStringParameterFromDataTable(layer.Name, dtK, "Layer", 0);
                        string lineTypeName = ReadStringParameterFromDataTable(targetLayerName, dtLag, "LineType", 0);
                        if (lineTypeName.IsNoE()) prdDbg($"LineTypeName is missing for {layer.Name}!");
                        layerLineTypeMap.Add(layer.Name, lineTypeName);
                    }
                    //Check if all line types are present
                    HashSet<string> missingLineTypes = new HashSet<string>();
                    foreach (var layer in layers)
                    {
                        string lineTypeName = layerLineTypeMap[layer.Name];
                        if (lineTypeName.IsNoE()) continue;
                        if (!ltt.Has(lineTypeName)) missingLineTypes.Add(lineTypeName);
                    }
                    if (missingLineTypes.Count > 0)
                    {
                        Database ltDb = new Database(false, true);
                        ltDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\Projection_styles.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction ltTx = ltDb.TransactionManager.StartTransaction();
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                        LinetypeTable sourceLtt = (LinetypeTable)ltDb.TransactionManager.TopTransaction
                            .GetObject(ltDb.LinetypeTableId, OpenMode.ForRead);
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        foreach (string missingName in missingLineTypes) idsToClone.Add(sourceLtt[missingName]);
                        IdMapping mapping = new IdMapping();
                        ltDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        ltTx.Commit();
                        ltTx.Dispose();
                        ltDb.Dispose();
                    }
                    #endregion
                    foreach (LayerTableRecord ltr in layers)
                    {
                        string layerName = ltr.Name;
                        if (!dtK.AsEnumerable().Any(row => row[0].ToString() == layerName))
                        {
                            prdDbg($"UNKNOWN LAYER: {ltr.Name}");
                            continue;
                        }
                        string type = ReadStringParameterFromDataTable(layerName, dtK, "Type", 0);
                        if (type == "IGNORE") { prdDbg($"Layer {layerName} IGNORED!"); continue; }
                        string lerLayerName = ReadStringParameterFromDataTable(layerName, dtK, "Layer", 0);
                        if (!dtLag.AsEnumerable().Any(row => row[0].ToString() == lerLayerName))
                        {
                            prdDbg($"Ler layer {lerLayerName} not found in Lag.csv! Tilføj laget i Lag.csv.");
                            continue;
                        }
                        string farveString = ReadStringParameterFromDataTable(lerLayerName, dtLag, "Farve", 0);
                        Color color = UtilsCommon.Utils.ParseColorString(farveString);
                        if (color == null)
                        {
                            prdDbg($"Failed to read color for layer name {lerLayerName} with colorstring {farveString}. Skipping!");
                            continue;
                        }
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = color;
                        #region Read and assign layer's linetype
                        Oid lineTypeId;
                        string lineTypeName = layerLineTypeMap[layerName];
                        if (lineTypeName.IsNoE())
                        {
                            prdDbg($"WARNING! Layer name {layerName} does not have a line type specified!.");
                            //If linetype string is NoE -> CONTINUOUS linetype must be used
                            lineTypeId = ltt["Continuous"];
                        }
                        else
                        {
                            //the presence of the linetype is assured in previous section.
                            lineTypeId = ltt[lineTypeName];
                        }
                        ltr.LinetypeObjectId = lineTypeId;
                        #endregion
                    }
                    #region Set objects to byLayer
                    var list = localDb.ListOfType<Entity>(tx);
                    foreach (Entity item in list)
                    {
                        if (item is Polyline || item is Polyline3d)
                        {
                            item.CheckOrOpenForWrite();
                            item.Color = Color.FromColorIndex(ColorMethod.ByAci, 256);
                            item.Linetype = "ByLayer";
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>EXPORTBLOCKSPSDATATOCSV</command>
        /// <summary>
        /// Exports BBR block property set data to a CSV file. BBR.csv placed in the same folder as the drawing.
        /// Det er meningen at denne kommando skal bruges til at eksportere BBR data til en CSV fil.
        /// Herefter kan man importere dataen i et regneark og/eller QGIS og arbejde videre med det.
        /// </summary>
        /// <category>Dimensioning</category>
        [CommandMethod("EXPORTBLOCKSPSDATATOCSV")]
        public void exportblockspsdatatocsv()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.CurrentDocument;
            Database localDb = docCol.MdiActiveDocument.Database;
            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    StringBuilder sb = new StringBuilder();
                    PropertySetManager psMan = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();
                    var propList = bbrDef.ListOfProperties();
                    foreach (var item in propList)
                    {
                        sb.Append(item.Name + ";");
                    }
                    sb.Append("X;Y;Lat;Long");
                    sb.AppendLine();
                    foreach (var br in brs)
                    {
                        foreach (var prop in propList)
                        {
                            sb.Append(psMan.ReadPropertyString(br, prop) + ";");
                        }
                        sb.Append(br.Position.X.ToString() + ";");
                        sb.Append(br.Position.Y.ToString() + ";");

                        var ll = ToWGS84FromUtm32N(br.Position.X, br.Position.Y);
                        sb.Append(ll[0] + ";");
                        sb.Append(ll[1]);

                        sb.AppendLine();
                    }
                    string dbFilename = localDb.OriginalFileName;
                    string path = Path.GetDirectoryName(dbFilename);
                    prdDbg(path + "\\BBR.csv");
                    Utils.OutputWriter(
                        path + "\\BBR.csv", sb.ToString(), true);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>DISPLAYNROFHISTLINES</command>
        /// <summary>
        /// Displays the number of history lines in AutoCAD console.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("DISPLAYNROFHISTLINES")]
        public void displaynrofhistlines()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    doc.SendStringToExecute("(GETENV \"CmdHistLines\")\n", true, false, false);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>SETNROFHISTLINES</command>
        /// <summary>
        /// Sets the number of history lines in AutoCAD console.
        /// The number must be between 25 and 2048.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("SETNROFHISTLINES")]
        public void setnrofhistlines()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    int lines = Interaction.GetInteger("Enter number of lines for AutoCAD command line history: ");
                    if (lines == -1) { AbortGracefully("Number of lines cancelled!", localDb); return; }
                    if (lines < 25 || lines > 2048)
                    { AbortGracefully("Number of lines must be between 25 and 2048!", localDb); return; }
                    doc.SendStringToExecute($"(SETENV \"CmdHistLines\" \"{lines}\")\n", true, false, false);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>SETTBLDATA, STD</command>
        /// <summary>
        /// A tool to set data used for TBL quantities.
        /// </summary>
        /// <category>Mængdeudtræk</category>
        [CommandMethod("SETTBLDATA")]
        [CommandMethod("STD")]
        public void settbldata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            //Jesper simpel metode
            //Nummer og Vejnavn udelades

            Oid oid = Interaction.GetEntity("Select polyline til TBL områder: ");
            if (oid == Oid.Null) { return; }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Entity ent = oid.Go<Entity>(tx, OpenMode.ForWrite);
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriOmråder);
                    PSetDefs.DriOmråder psDef = new PSetDefs.DriOmråder();
                    string[] kwds = new string[]
                    {
                        "Vejbane",
                        "Cykelsti",
                        "Belægningssten",
                        "Flisebelægning",
                        "FOrtov",
                        "Overkørsel",
                        "Ubefæstet"
                    };
                    //string[] kwds = new string[]
                    //{
                    //    "Vejbane",
                    //    "Befæstet",
                    //    "Ubefæstet"
                    //};
                    string kwd = Interaction.GetKeywords("Angiv belægning: ", kwds);
                    if (kwd == null) { tx.Abort(); return; }
                    if (kwd == "FOrtov") kwd = "Fortov";
                    psm.WritePropertyString(ent, psDef.Belægning, kwd);
                    kwds = new string[]
                    {
                        "1",
                        "2",
                        "3",
                        "4"
                    };
                    kwd = null;
                    kwd = Interaction.GetKeywords("Angiv vejklasse: ", kwds);
                    if (kwd == null) { tx.Abort(); return; }
                    psm.WritePropertyString(ent, psDef.Vejklasse, kwd);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>XREFSUNLOADSELECTBATCH</command>
        /// <summary>
        /// Unloads selected Xrefs from all dwgs in a folder.
        /// The xrefs must be listed in a file xrefNames.txt in the same folder.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("XREFSUNLOADSELECTBATCH")]
        public void unloadxrefsbatch()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string pathToFolder;
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select folder where LER dwg files are stored: ",
            };
            if (folderDialog.ShowDialog() == true)
            {
                pathToFolder = folderDialog.FolderName + "\\";
            }
            else return;
            var files = Directory.EnumerateFiles(pathToFolder, "*.dwg");
            //Path to textfile with Xref names
            string xrefNamesPath = pathToFolder + "xrefNames.txt";
            if (!File.Exists(xrefNamesPath)) throw new System.Exception(
                "Text file \"xrefNames.txt\" is missing at the specified location!");
            var xrefNames = File.ReadAllLines(xrefNamesPath);
            foreach (var f in files)
            {
                ObjectIdCollection idsToUnload = new ObjectIdCollection();
                using (Database xDb = new Database(false, true))
                {
                    xDb.ReadDwgFile(f, FileOpenMode.OpenForReadAndWriteNoShare, false, "");
                    using (Transaction xTx = xDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            BlockTable bt = xDb.BlockTableId.Go<BlockTable>(xTx);
                            foreach (Oid oid in bt)
                            {
                                BlockTableRecord btr = oid.Go<BlockTableRecord>(xTx);
                                if (btr.IsFromExternalReference)
                                    if (xrefNames.Contains(btr.Name))
                                        idsToUnload.Add(btr.Id);
                            }
                            if (idsToUnload.Count > 0) xDb.UnloadXrefs(idsToUnload);
                        }
                        catch (System.Exception ex)
                        {
                            xTx.Abort();
                            prdDbg(ex);
                            throw;
                        }
                        xTx.Commit();
                    }
                    xDb.SaveAs(f, true, DwgVersion.Newest, null);
                }
            }
        }
        /// <command>XREFSUNLOADALLBATCH</command>
        /// <summary>
        /// Unloads all Xrefs in all dwgs in a folder.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("XREFSUNLOADALLBATCH")]
        public void unloadallxrefsbatch()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string pathToFolder;
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select folder where LER dwg files are stored: ",
            };
            if (folderDialog.ShowDialog() == true)
            {
                pathToFolder = folderDialog.FolderName + "\\";
            }
            else return;
            var files = Directory.EnumerateFiles(pathToFolder, "*.dwg");
            foreach (var f in files)
            {
                ObjectIdCollection idsToUnload = new ObjectIdCollection();
                using (Database xDb = new Database(false, true))
                {
                    prdDbg(Path.GetFileName(f));
                    System.Windows.Forms.Application.DoEvents();
                    xDb.ReadDwgFile(f, FileOpenMode.OpenForReadAndWriteNoShare, false, "");
                    using (Transaction xTx = xDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            BlockTable bt = xDb.BlockTableId.Go<BlockTable>(xTx);
                            foreach (Oid oid in bt)
                            {
                                BlockTableRecord btr = oid.Go<BlockTableRecord>(xTx);
                                if (btr.IsFromExternalReference) idsToUnload.Add(btr.Id);
                            }
                            if (idsToUnload.Count > 0) xDb.UnloadXrefs(idsToUnload);
                        }
                        catch (System.Exception ex)
                        {
                            xTx.Abort();
                            prdDbg(ex);
                            throw;
                        }
                        xTx.Commit();
                    }
                    xDb.SaveAs(f, true, DwgVersion.Newest, null);
                }
            }
            prdDbg("Finished!");
        }
        /// <command>XREFSRELOADALLBATCH</command>
        /// <summary>
        /// Reloads all Xrefs in all dwgs in a folder.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("XREFSRELOADALLBATCH")]
        public void reloadallxrefsbatch()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string pathToFolder;
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select folder where LER dwg files are stored: ",
            };
            if (folderDialog.ShowDialog() == true)
            {
                pathToFolder = folderDialog.FolderName + "\\";
            }
            else return;
            var files = Directory.EnumerateFiles(pathToFolder, "*.dwg");
            foreach (var f in files)
            {
                ObjectIdCollection idsToUnload = new ObjectIdCollection();
                using (Database xDb = new Database(false, true))
                {
                    prdDbg(Path.GetFileName(f));
                    System.Windows.Forms.Application.DoEvents();
                    xDb.ReadDwgFile(f, FileOpenMode.OpenForReadAndWriteNoShare, false, "");
                    using (Transaction xTx = xDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            BlockTable bt = xDb.BlockTableId.Go<BlockTable>(xTx);
                            foreach (Oid oid in bt)
                            {
                                BlockTableRecord btr = oid.Go<BlockTableRecord>(xTx);
                                if (btr.IsFromExternalReference) idsToUnload.Add(btr.Id);
                            }
                            if (idsToUnload.Count > 0) xDb.ReloadXrefs(idsToUnload);
                        }
                        catch (System.Exception ex)
                        {
                            xTx.Abort();
                            prdDbg(ex);
                            throw;
                        }
                        xTx.Commit();
                    }
                    xDb.SaveAs(f, true, DwgVersion.Newest, null);
                }
            }
            prdDbg("Finished!");
        }
        /// <command>OPENSAVECLOSEALLDWGS</command>
        /// <summary>
        /// Opens, saves, and closes all databases in memory.
        /// In memory means that the dwg is not opened in editor.
        /// As far as we can tell, this cannot be used to refresh titleblocks.
        /// This is not final statement, as I don't know how the method
        /// that is used in this command works 100%.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("OPENSAVECLOSEALLDWGS", CommandFlags.Session)]
        public void opensaveclosealldwgs()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string pathToFolder;
            var folderDialog = new OpenFolderDialog
            {
                Title = "Select folder where LER dwg files are stored: ",
            };
            if (folderDialog.ShowDialog() == true)
            {
                pathToFolder = folderDialog.FolderName + "\\";
            }
            else return;
            var files = Directory.EnumerateFiles(pathToFolder, "*.dwg");
            foreach (var f in files)
            {
                //using (Database xDb = new Database(false, true))
                //{
                prdDbg(Path.GetFileName(f));
                System.Windows.Forms.Application.DoEvents();
                if (File.Exists(f))
                {
                    Document newDoc = docCol.Open(f);
                    using (DocumentLock dl = newDoc.LockDocument())
                    {
                        newDoc.Editor.ZoomExtents();
                    }
                    newDoc.Database.SaveAs(f, DwgVersion.Newest);
                    newDoc.CloseAndDiscard();
                }
                else
                {
                    prdDbg($"File {f} does not exist!");
                    continue;
                }
            }
            prdDbg("Finished!");
        }
        /// <command>LISTXREFSINFILE</command>
        /// <summary>
        /// Lists all Xrefs in all dwg files in the selected folder.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("LISTXREFSINFILE")]
        public void listxrefsinfile()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            #region Dialog box for file list selection and path determination
            string fileName;
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = "Choose .dwg file to list xrefs: ",
                DefaultExt = "dwg",
                Filter = "dwg files (*.dwg)|*.dwg|All files (*.*)|*.*",
                FilterIndex = 0
            };
            if (dialog.ShowDialog() == true)
            {
                fileName = dialog.FileName;
            }
            else return;
            #endregion
            using (Database xDb = new Database(false, true))
            {
                xDb.ReadDwgFile(fileName, FileOpenMode.OpenForReadAndWriteNoShare, false, "");
                using (Transaction xTx = xDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        BlockTable bt = xDb.BlockTableId.Go<BlockTable>(xTx);
                        foreach (Oid oid in bt)
                        {
                            BlockTableRecord btr = oid.Go<BlockTableRecord>(xTx);
                            if (btr.IsFromExternalReference) prdDbg(btr.Name);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        xTx.Abort();
                        prdDbg(ex);
                        throw;
                    }
                    xTx.Commit();
                }
            }
        }

        /// <command>LISTALLALIGNMENTS</command>
        /// <summary>
        /// Lists all alignments in the drawing and writes this to a file
        /// in the same folder as the drawing.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("LISTALLALIGNMENTS")]
        public void listallalignments()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    StringBuilder sb = new StringBuilder();
                    foreach (var al in als.OrderBy(x => x.Name))
                    {
                        sb.AppendLine(al.Name);
                    }
                    string dbFilename = localDb.OriginalFileName;
                    string path = Path.GetDirectoryName(dbFilename);
                    string listFileName = path + "\\AlignmentsList.txt";
                    OutputWriter(listFileName, sb.ToString(), true);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>DELETESPECIFICALIGNMENTS</command>
        /// <summary>
        /// Deletes alignments in the current drawing according to items listed in the selected text file.
        /// Direct -> deletes alignments listed in the file.
        /// Inverse -> deletes all alignments except those listed in the file.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("DELETESPECIFICALIGNMENTS")]
        public void deletespecificalignments()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string path = string.Empty;
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = "Choose txt file with alignments listed: ",
                DefaultExt = "txt",
                Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*",
                FilterIndex = 0
            };
            if (dialog.ShowDialog() == true)
            {
                path = dialog.FileName;
            }
            else return;
            List<string> list = File.ReadAllLines(path).ToList();
            string kwd = Interaction.GetKeywords("Direct or inverse? ", new string[] { "Direct", "Inverse" });
            if (kwd == null) return;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Alignment> alignments = localDb.HashSetOfType<Alignment>(tx);
                    foreach (var al in alignments)
                    {
                        if (kwd == "Direct")
                        {
                            if (list.Contains(al.Name))
                            {
                                al.CheckOrOpenForWrite();
                                al.Erase(true);
                            }
                        }
                        else if (kwd == "Inverse")
                        {
                            if (list.Contains(al.Name)) continue;
                            al.CheckOrOpenForWrite();
                            al.Erase(true);
                        }
                        else throw new System.Exception($"Wrong keyword: {kwd}!");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>DELETEBADENTITIES</command>
        /// <summary>
        /// Deletes polylines and points in the drawing that have a specific property set attached.
        /// Currently hardcoded name of the property set is Contains("(2)").
        /// This was used for a specific task and is not a general purpose command.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("DELETEBADENTITIES")]
        public void deletebadentities()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    HashSet<DBPoint> points = localDb.HashSetOfType<DBPoint>(tx);
                    HashSet<Entity> ents = new HashSet<Entity>();
                    ents.UnionWith(plines);
                    ents.UnionWith(points);
                    foreach (Entity ent in ents)
                    {
                        if (PropertySetManager.IsPropertySetAttached(ent, "(2)", PropertySetManager.MatchTypeEnum.Contains))
                        {
                            ent.CheckOrOpenForWrite();
                            ent.Erase();
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        /// <command>SELECTBADENTITIES</command>
        /// <summary>
        /// Selects polylines and points in the drawing that have a specific property set attached.
        /// Currently hardcoded name of the property set is Contains("(2)").
        /// This was used for a specific task and is not a general purpose command.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("SELECTBADENTITIES")]
        public void selectbadentities()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    HashSet<DBPoint> points = localDb.HashSetOfType<DBPoint>(tx);
                    HashSet<Entity> ents = new HashSet<Entity>();
                    ents.UnionWith(plines);
                    ents.UnionWith(points);
                    HashSet<Entity> selectedEnts = new HashSet<Entity>();
                    foreach (Entity ent in ents)
                    {
                        if (PropertySetManager.IsPropertySetAttached(ent, "(2)", PropertySetManager.MatchTypeEnum.Contains))
                        {
                            selectedEnts.Add(ent);
                        }
                    }
                    editor.SetImpliedSelection(selectedEnts.Select(x => x.Id).ToArray());
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>DELETEEMPTYTEXT</command>
        /// <summary>
        /// Deletes empty text entities (DBText -> Text, not MText) in the drawing.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("DELETEEMPTYTEXT")]
        public void selectemptytext()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            int count = 0;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<DBText> text = localDb.HashSetOfType<DBText>(tx);
                    foreach (DBText txt in text)
                    {
                        if (txt.TextString.IsNoE())
                        {
                            count++;
                            txt.CheckOrOpenForWrite();
                            txt.Erase();
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
            prdDbg($"Erased {count} emtpy text object(s)!");
        }

        /// <command>DUMPPSPROPERTYNAMES</command>
        /// <summary>
        /// Dumps all property names from a selected property set to C:\Temp\names.txt.
        /// </summary>
        /// <category>Property Sets</category>
        [CommandMethod("DUMPPSPROPERTYNAMES")]
        public void dumppspropertynames()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    OutputWriter(
                        "C:\\Temp\\names.txt",
                        string.Join("\n",
                        PropertySetManager.AllPropertyNamesAndDataType(localDb).OrderBy(x => x.Item1)),
                        true);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>SELECTCUSTOMBRS, SCB</command>
        /// <summary>
        /// Selects blocks in the current drawing that have value "Custom"
        /// instead of the correct type "Type" property.
        /// </summary>
        /// <category>Selection</category>
        [CommandMethod("SELECTCUSTOMBRS")]
        [CommandMethod("SCB")]
        public void selectcustombrs()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
            var dt = CsvData.FK;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var brs = localDb.GetFjvEntities(tx)
                        .Where(x => x is BlockReference).Cast<BlockReference>();
                    var query = brs.Where(x =>
                    {
                        string type = x.ReadDynamicPropertyValue("Type");
                        if (type == "Custom") return true;
                        else return false;
                    }).Select(x => x.Id);
                    var result = query.ToArray();
                    if (result.Length > 0)
                        editor.SetImpliedSelection(result);
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>CLIPPLINESOUTSIDEPLINE</command>
        /// <summary>
        /// Breaks all polylines that cross the selected polyline which must be a closed polyline.
        /// </summary>
        /// <category>Polylines</category>
        [CommandMethod("CLIPPLINESOUTSIDEPLINE")]
        public void clipplineoutsidepline()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            PromptEntityOptions peo = new PromptEntityOptions("\nSelect polyline to clip: ");
            peo.SetRejectMessage("\nNot a polyline!");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = editor.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;
            Oid clipPlineId = per.ObjectId;
            HashSet<Oid> plineOidsToCheckForDisjunction;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline clipPolyline = tx.GetObject(clipPlineId, OpenMode.ForRead) as Polyline;
                    if (clipPolyline == null) { tx.Abort(); return; }
                    ;
                    //Try to optimize the intersection algorithm
                    double splitLength = 500.0;
                    int nrOfSegments = (int)(clipPolyline.Length / splitLength);
                    HashSet<Extents2d> bboxes = new HashSet<Extents2d>();
                    double previousDist = 0;
                    double dist = 0;
                    for (int i = 0; i <= nrOfSegments; i++)
                    {
                        if (i != nrOfSegments) // Not the last iteration
                        {
                            dist += splitLength;
                        }
                        else // Last iteration
                        {
                            dist = clipPolyline.Length;
                        }
                        Point2d p1 = clipPolyline.GetPointAtDist(previousDist).To2d();
                        Point2d p2 = clipPolyline.GetPointAtDist(dist).To2d();
                        double minX = p1.X < p2.X ? p1.X : p2.X;
                        double minY = p1.Y < p2.Y ? p1.Y : p2.Y;
                        double maxX = p1.X > p2.X ? p1.X : p2.X;
                        double maxY = p1.Y > p2.Y ? p1.Y : p2.Y;
                        bboxes.Add(new Extents2d(minX, minY, maxX, maxY));
                        previousDist = dist;
                    }
                    HashSet<Polyline> allPlines = new HashSet<Polyline>();
                    foreach (Polyline ent in localDb.HashSetOfType<Polyline>(tx, true))
                    {
                        if (ent.ObjectId == clipPlineId) continue;
                        allPlines.Add(ent);
                    }
                    HashSet<Polyline> plinesToIntersect = new HashSet<Polyline>();
                    HashSet<Polyline> plinesThatDoNotOverlap = new HashSet<Polyline>();
                    foreach (Polyline pline in allPlines)
                    {
                        if (bboxes.Any(x => x.IsOverlapping(
                            pline.GeometricExtents.ToExtents2d())))
                        {
                            plinesToIntersect.Add(pline);
                        }
                        else plinesThatDoNotOverlap.Add(pline);
                    }
                    Plane plane = new Plane();
                    List<double> splitPts = new List<double>();
                    List<Point3d> ints = new List<Point3d>();
                    IntPtr zero = new IntPtr(0);
                    foreach (Polyline pline in plinesToIntersect)
                    {
                        splitPts.Clear();
                        ints.Clear();

                        clipPolyline.IntersectWithValidation(pline, ints);

                        if (ints.Count == 0) plinesThatDoNotOverlap.Add(pline);
                        foreach (Point3d intPoint in ints)
                        {
                            try
                            {
                                double param = pline.GetParameterAtPoint(intPoint);
                                splitPts.Add(param);
                            }
                            catch (System.Exception)
                            {
                                prdDbg($"Pline: {pline.Handle}, intPoint: {intPoint}");
                                if (ints.Count == 1)
                                {
                                    plinesThatDoNotOverlap.Add(pline);
                                    continue;
                                }
                                else
                                {
                                    prdDbg("Unhandled edge case encountered! " +
                                    $"H: {pline.Handle} - {intPoint}");
                                }
                            }
                        }
                        if (splitPts.Count == 0) continue;
                        splitPts.Sort();
                        DBObjectCollection objs = pline.GetSplitCurves(
                                new DoubleCollection(splitPts.ToArray()));
                        foreach (DBObject obj in objs)
                        {
                            if (obj is Polyline newPline)
                            {
                                if (newPline.Length < 0.1) continue;
                                newPline.AddEntityToDbModelSpace(localDb);
                                PropertySetManager.CopyAllProperties(pline, newPline);
                                newPline.Layer = pline.Layer;
                                plinesThatDoNotOverlap.Add(newPline);
                            }
                        }
                        pline.CheckOrOpenForWrite();
                        pline.Erase(true);
                    }
                    plineOidsToCheckForDisjunction = plinesThatDoNotOverlap
                        .Select(x => x.Id).ToHashSet();
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Polyline clipPolyline = tx.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                    if (clipPolyline == null) { tx.Abort(); return; }
                    ;
                    //Determine if the polyline is inside or outside the clip polyline
                    using (MPolygon mpg = new MPolygon())
                    {
                        mpg.AppendLoopFromBoundary(clipPolyline, true, Tolerance.Global.EqualPoint);
                        foreach (Oid oid in plineOidsToCheckForDisjunction)
                        {
                            Polyline pline = oid.Go<Polyline>(tx);
                            bool isInside = true;
                            if (pline.NumberOfVertices == 2)
                            {
                                //Next for misses plines with only two vertici
                                //Handle those
                                bool firstIsInside = (mpg.IsPointInsideMPolygon(
                                        pline.GetPoint3dAt(0), Tolerance.Global.EqualPoint).Count == 1);
                                bool secondIsInside = (mpg.IsPointInsideMPolygon(
                                        pline.GetPoint3dAt(1), Tolerance.Global.EqualPoint).Count == 1);
                                if (!firstIsInside && !secondIsInside)
                                {
                                    pline.CheckOrOpenForWrite();
                                    pline.Erase(true);
                                }
                            }
                            else
                            {
                                for (int i = 0; i < pline.NumberOfVertices; i++)
                                {
                                    if (i == 0 || i == pline.NumberOfVertices - 1) continue;
                                    isInside = (mpg.IsPointInsideMPolygon(
                                        pline.GetPoint3dAt(i), Tolerance.Global.EqualPoint).Count == 1);
                                    if (!isInside)
                                    {
                                        pline.CheckOrOpenForWrite();
                                        pline.Erase(true);
                                        break;
                                    }
                                }
                            }
                        }
                    }
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

        /// <command>CREATEMANUALVIEWFRAME, CMVF</command>
        /// <summary>
        /// Creates a manual view frame for GIS data.
        /// Is not generally used and is not a part of the standard workflow.
        /// </summary>
        /// <category>GIS</category>
        [CommandMethod("CREATEMANUALVIEWFRAME")]
        [CommandMethod("CMVF")]
        public void createmanualviewframe()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            string kwd = Interaction.GetKeywords("New or add to existing? [New/Add]",
                               new string[] { "New", "Add" });
            if (kwd.IsNoE()) return;
            string pathToFolder;
            string fileName;
            GeoJsonFeatureCollection gjfc;
            if (kwd == "New")
            {
                string prevFolder = @"C:\";
                if (File.Exists(
                    @"X:\AutoCAD DRI - 01 Civil 3D\Netload\Support\CreateManualViewFrameLastDir.txt"))
                {
                    var lines = File.ReadAllLines(
                        @"X:\AutoCAD DRI - 01 Civil 3D\Netload\Support\CreateManualViewFrameLastDir.txt");
                    if (lines.Length > 0) prevFolder = lines[0];
                }
                var folderDialog = new OpenFolderDialog
                {
                    Title = "\"Choose folder where to store view frame GeoJSON: ",
                    InitialDirectory = prevFolder,
                };
                if (folderDialog.ShowDialog() == true)
                {
                    pathToFolder = folderDialog.FolderName;
                    File.WriteAllText(
                        @"X:\AutoCAD DRI - 01 Civil 3D\Netload\Support\CreateManualViewFrameLastDir.txt",
                        pathToFolder, Encoding.UTF8);
                    fileName = Path.Combine(pathToFolder, "ViewFrames.geojson");
                    gjfc = new GeoJsonFeatureCollection("ViewFrames");
                }
                else return;
            }
            else
            {
                string prevFolder = @"C:\";
                if (File.Exists(
                    @"X:\AutoCAD DRI - 01 Civil 3D\Netload\Support\CreateManualViewFrameLastDir.txt"))
                {
                    var lines = File.ReadAllLines(
                        @"X:\AutoCAD DRI - 01 Civil 3D\Netload\Support\CreateManualViewFrameLastDir.txt");
                    if (lines.Length > 0) prevFolder = lines[0];
                }
                var ofd = new OpenFileDialog
                {
                    Title = "Select ViewFrames.geojson to add to: ",
                    InitialDirectory = prevFolder,
                    Filter = "GeoJSON files (*.geojson)|*.geojson|All files (*.*)|*.*"
                };
                if (ofd.ShowDialog() == true)
                {
                    fileName = ofd.FileName;
                    File.WriteAllText(
                        @"X:\AutoCAD DRI - 01 Civil 3D\Netload\Support\CreateManualViewFrameLastDir.txt",
                        Path.GetDirectoryName(fileName), Encoding.UTF8);
                    var jsonString = File.ReadAllText(fileName, Encoding.UTF8);
                    gjfc = JsonSerializer.Deserialize<GeoJsonFeatureCollection>(jsonString);
                    if (gjfc == null) return;
                }
                else return;
            }
            int featureCountCached = gjfc.Features.Count;
            bool cont = true;
            while (cont)
            {
                string name = Interaction.GetString("Name of view frame: ");
                if (name.IsNoE()) break;
                double[][] coords = new double[5][];
                for (int i = 0; i < 4; i++)
                {
                    Point3d p = Interaction.GetPoint($"Pick {i + 1}. corner of view frame: ");
                    if (p.IsNull()) { cont = false; break; }
                    coords[i] = new double[] { p.X, p.Y };
                }
                if (cont)
                {
                    coords[4] = coords[0];
                    GeoJsonGeometryLineString gjls = new GeoJsonGeometryLineString();
                    gjls.Coordinates = coords;
                    GeoJsonFeature feature = new GeoJsonFeature();
                    feature.Geometry = gjls;
                    feature.Properties.Add("DwgNumber", name);
                    gjfc.Features.Add(feature);
                }
            }
            if (gjfc.Features.Count > featureCountCached)
            {
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    WriteIndented = true
                };
                string jsonString = JsonSerializer.Serialize(gjfc, options);
                File.WriteAllText(fileName, jsonString, Encoding.UTF8);
            }
        }

        /// <command>GOOGLESTREETVIEW, GS</command>
        /// <summary>
        /// Opens Google Street View at the specified location.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("GOOGLESTREETVIEW")]
        [CommandMethod("GS")]
        public void googlestreetview()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Point3d p = Interaction.GetPoint($"Pick point for Google Street View: ");
            if (p.IsNull()) { return; }
            var latlong = p.ToWGS84FromUtm32N();
            prdDbg($"Opening Google Street View with coordinates: {latlong[0]}, {latlong[1]}.");
            string url = $"https://www.google.com/maps/@?api=1&map_action=pano&viewpoint={latlong[0]},{latlong[1]}";
            System.Diagnostics.Process.Start(
                new ProcessStartInfo(url) { UseShellExecute = true });
        }

        /// <command>SKRAAFOTO, SF</command>
        /// <summary>
        /// Opens Skraafoto at the specified location.
        /// </summary>
        /// <category>Miscellaneous</category>
        [CommandMethod("SKRAAFOTO")]
        [CommandMethod("SF")]
        public void skraafoto()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Point3d p = Interaction.GetPoint($"Pick point for Skråfoto: ");
            if (p.IsNull()) { return; }
            //var latlong = p.ToWGS84FromUtm32N();
            int x = (int)p.X;
            int y = (int)p.Y;
            prdDbg($"Opening Skraafoto with coordinates: {x}, {y}.");
            string url = $"https://skraafoto.dataforsyningen.dk/?center={x}%2C{y}";
            System.Diagnostics.Process.Start(
                new ProcessStartInfo(url) { UseShellExecute = true });
        }

        /// <command>FINDALIGNMENT</command>
        /// <summary>
        /// Selects all entities that belong to a specific alignment.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("FINDALIGNMENT")]
        public void findalignment()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable dt = CsvData.FK;
                    PropertySetManager psmPipeLineData = new PropertySetManager(
                        localDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData =
                        new PSetDefs.DriPipelineData();
                    var ents = localDb.GetFjvEntities(tx, false, false, true);
                    var list = ents.Select(
                        x => psmPipeLineData.ReadPropertyString(x, driPipelineData.BelongsToAlignment))
                        .Distinct().OrderBy(x => x);
                    StringGridForm sgf = new StringGridForm(list, "SELECT ALIGNMENT NAME");
                    sgf.ShowDialog();
                    if (sgf.SelectedValue != null)
                    {
                        var result = ents.Where(x => psmPipeLineData
                        .FilterPropetyString(x, driPipelineData.BelongsToAlignment, sgf.SelectedValue))
                        .Select(x => x.Id)
                        .ToArray();
                        if (result.Length == 0)
                        {
                            prdDbg("No entities found with this alignment name!");
                            tx.Abort();
                            return;
                        }
                        docCol.MdiActiveDocument.Editor.SetImpliedSelection(
                            result
                            );
                    }
                    else { prdDbg("Cancelled!"); }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>SETALLBLOCKSTOSERIE</command>
        /// <summary>
        /// Sets all blocks to a specific series.
        /// </summary>
        /// <category>Blocks</category>
        [CommandMethod("SETALLBLOCKSTOSERIE")]
        public void setallblockstoserie()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable dt = CsvData.FK;
                    var ents = localDb.GetFjvBlocks(tx, dt, false, false, true);
                    List<string> brNames = ents.Select(x => x.RealName())
                        .Distinct().OrderBy(x => x).ToList();
                    StringGridForm sgf = new StringGridForm(brNames, "Select block NAME to set Serie: ");
                    sgf.ShowDialog();
                    if (sgf.SelectedValue == null)
                    {
                        prdDbg("Cancelled!");
                        tx.Abort();
                        return;
                    }
                    var result = ents.Where(x => x.RealName() == sgf.SelectedValue).ToArray();
                    List<PipeSeriesEnum> list = new List<PipeSeriesEnum>()
                    {
                        PipeSeriesEnum.S1, PipeSeriesEnum.S2, PipeSeriesEnum.S3,
                    };
                    StringGridForm sgf2 = new StringGridForm(list.Select(x => x.ToString()), "Select Serie: ");
                    sgf2.ShowDialog();
                    if (sgf2.SelectedValue == null)
                    {
                        prdDbg("Cancelled!");
                        tx.Abort();
                        return;
                    }
                    PipeSeriesEnum pipeSeriesEnum = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum), sgf2.SelectedValue);
                    foreach (var br in result)
                    {
                        //br.CheckOrOpenForWrite();
                        //br. ("Serie", pipeSeriesEnum.ToString());
                        var dbrpc = br.DynamicBlockReferencePropertyCollection;
                        foreach (DynamicBlockReferenceProperty item in dbrpc)
                        {
                            if (item.PropertyName == "Serie")
                            {
                                if (item.Value.ToString() == pipeSeriesEnum.ToString()) continue;
                                br.CheckOrOpenForWrite();
                                item.Value = pipeSeriesEnum.ToString();
                            }
                        }
                        br.CheckOrOpenForWrite();
                        br.AttSync();
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>TLEN</command>
        /// <summary>
        /// Calculates the total length of DH pipes in the drawing.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("TLEN")]
        public void totallengthofpipes()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pipes = localDb.GetFjvPipes(tx);
                    var gps = pipes.GroupBy(x => x.Layer).OrderBy(x => x.Key);
                    List<(string layer, double length)> list = new List<(string layer, double length)>();
                    gps.ForEach(x =>
                    {
                        double length = x.Sum(y => y.Length);
                        list.Add((x.Key, length));
                    });
                    var s = list.Select(x => $"{x.layer}: {x.length}").ToArray();
                    string res = string.Join("\n", s);
                    prdDbg(res);
                    s = list.Select(x => $"{x.layer};{x.length}").ToArray();
                    res = string.Join("\n", s);
                    File.WriteAllLines(@"C:\Temp\Lengths.txt", s);
                    //string path =
                    //        Environment.ExpandEnvironmentVariables("%temp%") + "\\" + "errorElId.txt";
                    Process.Start("notepad.exe", @"C:\Temp\Lengths.txt");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>ISBLOCKDYNAMIC</command>
        /// <summary>
        /// Prints true if block is a Dynamic Block or false if not.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("ISBLOCKDYNAMIC")]
        public void isblockdynamic()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var oid = Interaction.GetEntity("Select block: ", typeof(BlockReference));
                    if (oid == Oid.Null) { tx.Abort(); return; }

                    BlockReference br = oid.Go<BlockReference>(tx);
                    if (br.IsDynamicBlock)
                    {
                        prdDbg("True");
                    }
                    else
                    {
                        prdDbg("False");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>DELETEALLPROPERTYSETS</command>
        /// <summary>
        /// Deletes ALL property sets in the drawing.
        /// </summary>
        /// <category>Property Sets</category>
        [CommandMethod("DELETEALLPROPERTYSETS")]
        public void deleteallpropertysets()
        {
            PropertySetManager.DeleteAllPropertySets(
                Application.DocumentManager.MdiActiveDocument.Database);
        }

        /// <command>SETALIGNMENTDESCRIPTIONS</command>
        /// <summary>
        /// Sets the descriptions for alignments in the drawing equal to alignments names.
        /// NOTE!!! This doesn't work for some reason.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("SETALIGNMENTDESCRIPTIONS")]
        public void setalignmentdescriptions()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var als = localDb.HashSetOfType<Alignment>(tx);
                    foreach (Alignment a in als)
                    {
                        string name = a.Name;
                        a.CheckOrOpenForWrite();
                        a.Description = name;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>OPENDWG</command>
        /// <summary>
        /// Opens DWG of specified type for the chosen project and phase.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("OPENDWG")]
        public void opendwg()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            var dro = new DataReferencesOptions();

            var result = StringGridFormCaller.Call(
                ["Fremtid", "Alignments", "Surface", "Ler", "Længdeprofiler"], "What DWG to open?");
            if (result.IsNoE()) return;

            var type = (StierDataType)Enum.Parse(typeof(StierDataType), result);
            var dm = new DataManager(dro);

            string path;
            var paths = dm.GetFileNames(type);
            if (paths == null || paths.Count() == 0) return;
            if (paths.Count() > 1)
            {
                var fs = paths.ToDictionary(x => Path.GetFileNameWithoutExtension(x), x => x);
                result = StringGridFormCaller.Call(fs.Keys.Order(), "Select individual file: ");
                if (result.IsNoE()) return;
                path = fs[result];
            }
            else path = paths.First();

            if (path.IsNoE()) return;
            if (!File.Exists(path))
            {
                prdDbg("File does not exist!");
                return;
            }
            Document doc = docCol.Open(path, false);
            docCol.MdiActiveDocument = doc;
        }
        //[CommandMethod("TESTQUIKGRAPH")]
        public void testquikgraph()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Read CSV
                    System.Data.DataTable dt = default;
                    try
                    {
                        dt = CsvReader.ReadCsvToDataTable(
                                @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                        prdDbg(ex);
                        throw;
                    }
                    if (dt == default)
                    {
                        prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                        throw new System.Exception("Failed to read FJV Dynamiske Komponenter.csv");
                    }
                    #endregion
                    PropertySetManager psmPLD = new PropertySetManager(
                        localDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPLD =
                        new PSetDefs.DriPipelineData();
                    var ents = localDb.GetFjvEntities(tx, true, true, true);
                    #region Create graph
                    HashSet<POI2> POIs = new HashSet<POI2>();
                    foreach (Entity ent in ents) AddEntityToPOIs(ent, POIs,
                        ents.Where(x => x is Polyline).Cast<Polyline>());
                    IEnumerable<IGrouping<POI2, POI2>> clusters
                        = POIs.GroupByCluster((x, y) => x.Point.GetDistanceTo(y.Point), 0.01);
                    foreach (IGrouping<POI2, POI2> cluster in clusters)
                    {
                        //Create unique pairs
                        var pairs = cluster.SelectMany((value, index) => cluster.Skip(index + 1),
                                                       (first, second) => new { first, second });
                        //Create reference to each other for each pair
                        foreach (var pair in pairs)
                        {
                            if (pair.first.IsSameSource(pair.second)) continue;
                            pair.first.AddReference(pair.second);
                            pair.second.AddReference(pair.first);
                        }
                    }
                    //First crate a graph that start from a random entity
                    var startingGraph = new BidirectionalGraph<Entity, EdgeTyped>();
                    //var startingGraph = new UndirectedGraph<Entity, Edge<Entity>>();
                    var groups = POIs.GroupBy(x => x.Source.Handle);
                    foreach (var group in groups)
                        startingGraph.AddVertex(group.First().Source);
                    foreach (var group in groups)
                    {
                        Entity source = group.First().Source;
                        foreach (var poi in group)
                            if (poi.Target != null)
                                startingGraph.AddEdge(new EdgeTyped(source, poi.Target, poi.EndType));
                    }
                    //var vertice = simplifiedGraph.Vertices.Where(
                    //    x => x.Handle.ToString() == "11EFA0").FirstOrDefault();
                    //foreach (var edge in simplifiedGraph.AdjacentEdges(vertice))
                    //    prdDbg($"{edge.Source.Handle} - {edge.Target.Handle} - {edge.EndType}");
                    var leafVerts = startingGraph.Vertices.Where(
                        x => startingGraph.OutEdges(x).Where(
                            y => y.EndType != EndType.WeldOn).Count() == 1);
                    var startVert = leafVerts.MaxBy(x => GetDn(x, dt));
                    //var vert = startingGraph.Vertices.Where(
                    //    x => x.Handle.ToString() == "11EFA0").FirstOrDefault();
                    //foreach (var edge in startingGraph.OutEdges(vert))
                    //    prdDbg($"OE: {edge.Source.Handle} - {edge.Target.Handle} - {edge.EndType}");
                    //foreach (var edge in startingGraph.InEdges(vert))
                    //    prdDbg($"IE: {edge.Source.Handle} - {edge.Target.Handle} - {edge.EndType}");
                    var dfs = new DepthFirstSearchAlgorithm<Entity, EdgeTyped>(startingGraph);
                    //var dfs = new BreadthFirstSearchAlgorithm<Entity, EdgeTyped>(startingGraph);
                    List<Entity> visitedVertices = new List<Entity>();
                    List<EdgeTyped> visitedEdges = new List<EdgeTyped>();
                    dfs.DiscoverVertex += vertex => visitedVertices.Add(vertex);
                    dfs.ExamineEdge += edge =>
                    {
                        if (!visitedEdges.Contains(edge))
                            visitedEdges.Add(edge);
                    };
                    dfs.Compute(startVert);
                    var newGraph = new BidirectionalGraph<Entity, EdgeTyped>();
                    foreach (var vertex in visitedVertices)
                        newGraph.AddVertex(vertex);
                    foreach (var edge in visitedEdges)
                        newGraph.AddEdge(edge);
                    var adjacencyGraph = new AdjacencyGraph<Entity, EdgeTyped>(true);
                    adjacencyGraph.AddVertexRange(newGraph.Vertices);
                    adjacencyGraph.AddEdgeRange(newGraph.Edges);
                    // Create subgraphs
                    var clusteredGraph = new ClusteredAdjacencyGraph<Entity, EdgeTyped>(adjacencyGraph);
                    Dictionary<string, ClusteredAdjacencyGraph<Entity, EdgeTyped>> clusterDict =
                        new Dictionary<string, ClusteredAdjacencyGraph<Entity, EdgeTyped>>();
                    foreach (var vertex in adjacencyGraph.Vertices)
                    {
                        string clusterName = GetClusterName(vertex, psmPLD, driPLD, dt);
                        if (clusterName.IsNoE()) continue;
                        if (!clusterDict.ContainsKey(clusterName))
                            clusterDict[clusterName] = clusteredGraph.AddCluster();
                        clusterDict[clusterName].AddVertex(vertex);
                    }
                    // Create Graphviz algorithm instance
                    //var graphviz = new GraphvizAlgorithm<Entity, EdgeTyped>(newGraph);
                    //var graphviz = new GraphvizAlgorithm<Entity, EdgeTyped>(adjacencyGraph);
                    var graphviz = new GraphvizAlgorithm<Entity, EdgeTyped>(clusteredGraph);
                    // Customize appearance
                    graphviz.FormatVertex += (sender, args) =>
                    {
                        args.VertexFormat.Shape = QuikGraph.Graphviz.Dot.GraphvizVertexShape.Record;
                        //args.VertexFormat.Label = args.Vertex.Handle.ToString();
                        switch (args.Vertex)
                        {
                            case Polyline pline:
                                int dn = GetPipeDN(pline);
                                string system = GetPipeType(pline).ToString();
                                args.VertexFormat.Label =
                                $"{{{pline.Handle}|Rør L{pline.Length.ToString("0.##")}}}|{system}\\n{dn}";
                                break;
                            case BlockReference br:
                                string dn1 = ComponentSchedule.ReadDynamicCsvProperty(br, DynamicProperty.DN1);
                                string dn2 = ComponentSchedule.ReadDynamicCsvProperty(br, DynamicProperty.DN2);
                                string dnStr = dn2 == "0" ? dn1 : dn1 + "/" + dn2;
                                system = ComponentSchedule.ReadComponentSystem(br, dt);
                                string type = ComponentSchedule.ReadComponentType(br, dt);
                                if (type == "Reduktion")
                                    args.VertexFormat.StrokeColor =
                                    QuikGraph.Graphviz.Dot.GraphvizColor.Red;
                                args.VertexFormat.Label =
                                $"{{{br.Handle}|{type}}}|{system}\\n{dnStr}";
                                break;
                        }
                    };
                    graphviz.FormatEdge += (sender, args) =>
                    {
                        //args.EdgeFormat.Direction = QuikGraph.Graphviz.Dot.GraphvizEdgeDirection.Both;
                    };
                    // Generate .dot file content for this alignment
                    string dotPath = @"C:\Temp\wholeSystem.dot";
                    string pdfPath = @"C:\Temp\wholeSystem.pdf";
                    string dot = graphviz.Generate();
                    File.WriteAllText(dotPath, dot);
                    var startInfo = new ProcessStartInfo("dot")
                    {
                        Arguments = $"-Tpdf \"{dotPath}\" -o \"{pdfPath}\"",
                        RedirectStandardInput = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        UseShellExecute = false
                    };
                    Process.Start(startInfo).WaitForExit();
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        private int GetDn(Entity entity, System.Data.DataTable dt)
        {
            if (entity is Polyline pline)
                return GetPipeDN(pline);
            else if (entity is BlockReference br)
            {
                if (br.ReadDynamicCsvProperty(DynamicProperty.Type, false) == "Afgreningsstuds" ||
                    br.ReadDynamicCsvProperty(DynamicProperty.Type, false) == "Svanehals")
                    return int.Parse(ComponentSchedule.ReadDynamicCsvProperty(br, DynamicProperty.DN2));
                else return
                        int.Parse(ComponentSchedule.ReadDynamicCsvProperty(br, DynamicProperty.DN1));
            }
            else throw new System.Exception("Invalid entity type");
        }
        private string GetClusterName(
            Entity entity, PropertySetManager psm, PSetDefs.DriPipelineData pld, System.Data.DataTable dt)
        {
            if (entity is Polyline)
                return psm.ReadPropertyString(entity, pld.BelongsToAlignment);
            else if (entity is BlockReference)
            {
                //if (br.ReadDynamicCsvProperty(DynamicProperty.Type, dt, false) == "Afgreningsstuds" ||
                //    br.ReadDynamicCsvProperty(DynamicProperty.Type, dt, false) == "Svanehals")
                //    return psm.ReadPropertyString(entity, pld.BranchesOffToAlignment);
                return psm.ReadPropertyString(entity, pld.BelongsToAlignment);
            }
            else throw new System.Exception("Invalid entity type");
        }
        public void AddEntityToPOIs(Entity ent, HashSet<POI2> POIs, IEnumerable<Polyline> allPipes)
        {
            switch (ent)
            {
                case Polyline pline:
                    switch (GetPipeSystem(pline))
                    {
                        case PipeSystemEnum.Ukendt:
                            prdDbg($"Wrong type of pline supplied: {pline.Handle}");
                            throw new System.Exception("Supplied a new PipeSystemEnum! Add to code kthxbai.");
                        case PipeSystemEnum.Kobberflex:
                        case PipeSystemEnum.AluPex:
                            #region STIK//Find forbindelse til forsyningsrøret
                            Point3d pt = pline.StartPoint;
                            var query = allPipes.Where(x =>
                                pt.IsOnCurve(x, 0.025) &&
                                pline.Handle != x.Handle &&
                                GetPipeSystem(x) == PipeSystemEnum.Stål);
                            if (query.FirstOrDefault() != default)
                            {
                                Polyline parent = query.FirstOrDefault();
                                POIs.Add(new POI2(parent, parent.GetClosestPointTo(pt, false).To2d(), EndType.StikAfgrening));
                            }
                            pt = pline.EndPoint;
                            if (query.FirstOrDefault() != default)
                            {
                                //This shouldn't happen now, because AssignPlinesAndBlocksToAlignments
                                //guarantees that the end point is never on a supply pipe
                                Polyline parent = query.FirstOrDefault();
                                POIs.Add(new POI2(parent, parent.GetClosestPointTo(pt, false).To2d(), EndType.StikAfgrening));
                            }
                            #endregion
                            //Tilføj almindelige ender til POIs
                            POIs.Add(new POI2(pline, pline.StartPoint.To2d(), EndType.StikStart));
                            POIs.Add(new POI2(pline, pline.EndPoint.To2d(), EndType.StikEnd));
                            break;
                        default:
                            POIs.Add(new POI2(pline, pline.StartPoint.To2d(), EndType.Start));
                            POIs.Add(new POI2(pline, pline.EndPoint.To2d(), EndType.End));
                            break;
                    }
                    break;
                case BlockReference br:
                    Transaction tx = br.Database.TransactionManager.TopTransaction;
                    BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                    //Quick and dirty fix for missing data
                    if (br.RealName() == "SH LIGE")
                    {
                        PropertySetManager psmPipeline =
                                    new PropertySetManager(br.Database, PSetDefs.DefinedSets.DriPipelineData);
                        PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
                        string belongsTo = psmPipeline.ReadPropertyString(br, driPipelineData.BelongsToAlignment);
                        if (belongsTo.IsNoE())
                        {
                            string branchesOffTo = psmPipeline.ReadPropertyString(br, driPipelineData.BranchesOffToAlignment);
                            if (branchesOffTo.IsNotNoE())
                                psmPipeline.WritePropertyString(br, driPipelineData.BelongsToAlignment, branchesOffTo);
                        }
                    }
                    foreach (Oid oid in btr)
                    {
                        if (!oid.IsDerivedFrom<BlockReference>()) continue;
                        BlockReference nestedBr = oid.Go<BlockReference>(tx);
                        if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                        Point3d wPt = nestedBr.Position;
                        wPt = wPt.TransformBy(br.BlockTransform);
                        EndType endType;
                        if (nestedBr.Name.Contains("BRANCH")) { endType = EndType.Branch; }
                        else
                        {
                            endType = EndType.Main;
                            //Handle special case of AFGRSTUDS
                            //which does not coincide with an end on polyline
                            //but is situated somewhere along the polyline
                            if (br.RealName() == "AFGRSTUDS" || br.RealName() == "SH LIGE")
                            {
                                PropertySetManager psmPipeline =
                                    new PropertySetManager(br.Database, PSetDefs.DefinedSets.DriPipelineData);
                                PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
                                string branchAlName = psmPipeline.ReadPropertyString(br, driPipelineData.BranchesOffToAlignment);
                                if (branchAlName.IsNoE())
                                    prdDbg(
                                        $"WARNING! Afgrstuds {br.Handle} has no BranchesOffToAlignment value.\n" +
                                        $"This happens if there are objects with no alignment assigned.\n" +
                                        $"To fix enter main alignment name in BranchesOffToAlignment field.");
                                var polylines = allPipes
                                    //.GetFjvPipes(tx, true)
                                    //.HashSetOfType<Polyline>(tx, true)
                                    .Where(x => psmPipeline.FilterPropetyString
                                            (x, driPipelineData.BelongsToAlignment, branchAlName));
                                //.ToHashSet();
                                foreach (Polyline polyline in polylines)
                                {
                                    Point3d nearest = polyline.GetClosestPointTo(wPt, false);
                                    if (nearest.DistanceHorizontalTo(wPt) < 0.01)
                                    {
                                        POIs.Add(new POI2(polyline, nearest.To2d(), EndType.WeldOn));
                                        break;
                                    }
                                }
                            }
                        }
                        POIs.Add(new POI2(br, wPt.To2d(), endType));
                    }
                    break;
                default:
                    throw new System.Exception("Wrong type of object supplied!");
            }
        }
        //[CommandMethod("TESTENUMS")]
        public void testenums()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Document doc = docCol.CurrentDocument;
            Database localDb = docCol.MdiActiveDocument.Database;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    TestEnum testEnum = TestEnum.None;
                    //| TestEnum.TestSecond
                    //| TestEnum.TestThird

                    testEnum |= TestEnum.TestFirst;
                    testEnum |= TestEnum.TestSecond;
                    testEnum |= TestEnum.TestThird;
                    //testEnum |= TestEnum.TestFourth;
                    prdDbg((testEnum & TestEnum.TestFourth) == 0);
                    prdDbg(testEnum.ToString());
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
        [Flags]
        private enum TestEnum
        {
            None = 0,
            TestFirst = 1,
            TestSecond = 2,
            TestThird = 4,
            TestFourth = 8,
            TestFith = 16,
        }
        public class STPEdge
        {
            public int Start { get; set; }
            public int End { get; set; }
        }
        public class STPCoordinate
        {
            public int Id { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
        }
        /// <command>PARSEANDCREATELINES</command>
        /// <summary>
        /// Used for a specific task during development.
        /// Is not used in the standard workflow.
        /// </summary>
        /// <category>Development</category>
        [CommandMethod("PARSEANDCREATELINES")]
        public static void ParseAndCreateLines()
        {
            string edgesFilePath = @"X:\110 - 1529 - Greve Landsby - Dokumenter\01 Intern\04 Projektering\01 Dimensionering\01 Udvikling\stp_output_coords.txt";
            string coordinatesFilePath = @"X:\110 - 1529 - Greve Landsby - Dokumenter\01 Intern\04 Projektering\01 Dimensionering\01 Udvikling\stp_input.stp";
            var edges = ParseEdges(edgesFilePath);
            var coordinates = ParseCoordinates(coordinatesFilePath);
            if (edges.Count > 0 && coordinates.Count > 0)
            {
                CreateLinesWithCoordinates(edges, coordinates);
            }
            else
            {
                Console.WriteLine("Edges or coordinates are missing.");
            }
        }


        /// <command>PARSEEDGES</command>
        /// <summary>
        /// Used for a specific task during development.
        /// Is not used in the standard workflow.
        /// </summary>
        /// <category>Development</category>
        [CommandMethod("PARSEEDGES")]
        public static void ParseEdgesAndCreateLines()
        {
            string filePath = @"X:\110 - 1529 - Greve Landsby - Dokumenter\01 Intern\04 Projektering\01 Dimensionering\01 Udvikling\stp_input.stp";
            var edges = new List<STPEdge>();
            var coordinates = new Dictionary<int, STPCoordinate>();
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("E "))
                    {
                        var parts = line.Split(' ');
                        edges.Add(new STPEdge
                        {
                            Start = int.Parse(parts[1]),
                            End = int.Parse(parts[2])
                        });
                    }
                    else if (line.StartsWith("DD "))
                    {
                        var parts = line.Split(' ');
                        coordinates[int.Parse(parts[1])] = new STPCoordinate
                        {
                            Id = int.Parse(parts[1]),
                            X = double.Parse(parts[2]),
                            Y = double.Parse(parts[3])
                        };
                    }
                }
            }
            if (edges.Count > 0 && coordinates.Count > 0)
            {
                CreateLinesWithCoordinates(edges, coordinates);
            }
            else
            {
                Console.WriteLine("Edges or coordinates are missing in the file.");
            }
        }
        private static List<STPEdge> ParseEdges(string filePath)
        {
            var edges = new List<STPEdge>();
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("E "))
                    {
                        var parts = line.Split(' ');
                        edges.Add(new STPEdge
                        {
                            Start = int.Parse(parts[1]),
                            End = int.Parse(parts[2])
                        });
                    }
                }
            }
            return edges;
        }
        private static Dictionary<int, STPCoordinate> ParseCoordinates(string filePath)
        {
            var coordinates = new Dictionary<int, STPCoordinate>();
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("DD "))
                    {
                        var parts = line.Split(' ');
                        coordinates[int.Parse(parts[1])] = new STPCoordinate
                        {
                            Id = int.Parse(parts[1]),
                            X = double.Parse(parts[2]),
                            Y = double.Parse(parts[3])
                        };
                    }
                }
            }
            return coordinates;
        }
        private static void CreateLinesWithCoordinates(List<STPEdge> edges, Dictionary<int, STPCoordinate> coordinates)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using (var transaction = db.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)transaction.GetObject(db.BlockTableId, OpenMode.ForRead);
                var blockTableRecord = (BlockTableRecord)transaction.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (var edge in edges)
                {
                    if (coordinates.TryGetValue(edge.Start, out var startCoord) && coordinates.TryGetValue(edge.End, out var endCoord))
                    {
                        var line = new Line(
                            new Point3d(startCoord.X, startCoord.Y, 0),
                            new Point3d(endCoord.X, endCoord.Y, 0)
                        );
                        blockTableRecord.AppendEntity(line);
                        transaction.AddNewlyCreatedDBObject(line, true);
                    }
                }
                transaction.Commit();
            }
            Console.WriteLine("Lines created successfully.");
        }

        /// <command>CONVERT3DPOLIESTOPOLIESPSS</command>
        /// <summary>
        /// Converts 3D polylines to polylines and copies property sets.
        /// </summary>
        /// <category>Polylines</category>
        [CommandMethod("CONVERT3DPOLIESTOPOLIESPSS")]
        public void convert3dpoliestopoliespss()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            prdDbg("Remember that the PropertySets need be defined in advance!!!");

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var p3ds = localDb.HashSetOfType<Polyline3d>(tx);
                    editor.WriteMessage($"\nNr. of polylines3d: {p3ds.Count}");

                    foreach (var p3d in p3ds)
                    {
                        var verts = p3d.GetVertices(tx);

                        Polyline pline = new Polyline(verts.Length);

                        foreach (var vert in verts)
                        {
                            pline.AddVertexAt(pline.NumberOfVertices, vert.Position.To2d(), 0, 0, 0);
                        }

                        pline.AddEntityToDbModelSpace(localDb);

                        pline.Layer = p3d.Layer;
                        pline.Color = p3d.Color;

                        PropertySetManager.CopyAllProperties(p3d, pline);
                    }

                    foreach (var line in p3ds)
                    {
                        line.CheckOrOpenForWrite();
                        line.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    editor.WriteMessage("\n" + ex.ToString());
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>CONVERT3DPOLIESTOPOLIESPSS</command>
        /// <summary>
        /// Converts 3D polylines to polylines and copies property sets.
        /// </summary>
        /// <category>Polylines</category>
        [CommandMethod("CONVERTPOLIESTO3DPOLIESPSS")]
        public void convertpoliesto3dpoliespss()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            prdDbg("Remember that the PropertySets need be defined in advance!!!");

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx);
                    prdDbg($"Nr. of polylines: {pls.Count}");

                    foreach (var pl in pls)
                    {
                        List<Point3d> pts = new List<Point3d>();
                        for (int i = 0; i < pl.NumberOfVertices; i++) pts.Add(pl.GetPoint2dAt(i).To3d());

                        Polyline3d p3d = new Polyline3d(
                            Poly3dType.SimplePoly, new Point3dCollection(pts.ToArray()), false);

                        p3d.AddEntityToDbModelSpace(localDb);

                        p3d.Layer = pl.Layer;
                        p3d.Color = pl.Color;

                        PropertySetManager.CopyAllProperties(pl, p3d);

                        pl.UpgradeOpen();
                        pl.Erase(true);
                    }
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

        /// <command>SETGLOBALWIDTH</command>
        /// <summary>
        /// Reads diameter information from a CSV file and sets the GLOBAL width of polylines.
        /// Intented for setting global widths in LER 2D drawings.
        /// </summary>
        /// <category>Polylines</category>
        [CommandMethod("SETGLOBALWIDTH")]
        public void setglobalwidth()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region BlockTables
                    // Open the Block table for read
                    BlockTable bt = tx.GetObject(localDb.BlockTableId,
                                                       OpenMode.ForRead) as BlockTable;
                    // Open the Block table record Model space for write
                    BlockTableRecord modelSpace = tx.GetObject(bt[BlockTableRecord.ModelSpace],
                                                          OpenMode.ForWrite) as BlockTableRecord;
                    #endregion

                    #region Read Csv Data for Layers
                    //Establish the pathnames to files
                    //Files should be placed in a specific folder on desktop
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";

                    System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    #endregion

                    #region Load linework for analysis
                    prdDbg("\nLoading linework for analyzing...");

                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    editor.WriteMessage($"\nNr. of polylines: {plines.Count}");
                    #endregion

                    #region Read diameter and set width
                    int layerNameNotDefined = 0;
                    int layerNameIgnored = 0;
                    int layerDiameterDefMissing = 0;
                    int findDescriptionPartsFailed = 0;
                    foreach (Polyline pline in plines)
                    {
                        //Set color to by layer
                        pline.CheckOrOpenForWrite();
                        pline.ColorIndex = 256;

                        //Check if pline's layer exists in krydsninger
                        string nameInFile = ReadStringParameterFromDataTable(pline.Layer, dtKrydsninger, "Navn", 0);
                        if (nameInFile.IsNoE())
                        {
                            layerNameNotDefined++;
                            continue;
                        }

                        //Check if pline's layer is IGNOREd
                        string typeInFile = ReadStringParameterFromDataTable(pline.Layer, dtKrydsninger, "Type", 0);
                        if (typeInFile == "IGNORE")
                        {
                            layerNameIgnored++;
                            continue;
                        }

                        //Check if diameter information exists
                        string diameterDef = ReadStringParameterFromDataTable(pline.Layer,
                                dtKrydsninger, "Diameter", 0);
                        if (diameterDef.IsNoE())
                        {
                            layerDiameterDefMissing++;
                            continue;
                        }

                        //var list = FindDescriptionParts(diameterDef);
                        var parts = FindPropertySetParts(diameterDef);
                        if (parts.setName == default && parts.propertyName == default)
                        {
                            findDescriptionPartsFailed++;
                            continue;
                        }
                        //int diaOriginal = ReadIntPropertyValue(tables, pline.Id, parts[0], parts[1]);
                        object diaOriginal = PropertySetManager.ReadNonDefinedPropertySetObject(
                            pline, parts.setName, parts.propertyName);

                        double dia = default;

                        switch (diaOriginal)
                        {
                            case null:
                                dia = 90;
                                break;
                            case int integer:
                                dia = Convert.ToDouble(integer);
                                break;
                            case double d:
                                dia = d;
                                break;
                            case string s:
                                if (s.IsNoE()) s = "90";
                                try
                                {
                                    dia = Convert.ToDouble(s);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg($"Fails: {s}");
                                    throw;
                                }
                                break;
                            default:
                                dia = 90;
                                break;
                        }

                        prdDbg(pline.Handle.ToString() + ": " + dia.ToString());

                        //Quick hack to guard against 999 diameter for GAS
                        if (dia == 999) dia = 100;

                        dia = dia / 1000;

                        if (dia == 0) dia = 0.09;

                        pline.ConstantWidth = dia;
                    }
                    #endregion

                    #region Reporting

                    prdDbg($"Layer name not defined in Krydsninger.csv for {layerNameNotDefined} polyline(s).");
                    prdDbg($"Layer name is set to IGNORE in Krydsninger.csv for {layerNameIgnored} polyline(s).");
                    prdDbg($"Diameter definition is not defined in Krydsninger.csv for {layerDiameterDefMissing} polyline(s).");
                    prdDbg($"Getting diameter definition parts failed for {findDescriptionPartsFailed} polyline(s).");
                    #endregion

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        /// <command>CREATELABELSFOR2D</command>
        /// <summary>
        /// Creates text labels for polylines based on label definitions specified in Krydsninger.csv.
        /// Intented for displaying text labels in LER 2D drawings.
        /// </summary>
        /// <category>Polylines</category>
        [CommandMethod("CREATELABELSFOR2D")]
        public void createlabelsfor2d()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Settings
            const double labelOffset = 0.375;
            const double labelHeight = 0.75;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Read krydsninger
                    string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    System.Data.DataTable dtK = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    if (dtK == null) throw new System.Exception("Failed to read Krydsninger.csv!");
                    #endregion

                    #region Create layer for labels
                    string labelLayer = "0-LABELS";
                    localDb.CheckOrCreateLayer(labelLayer);
                    #endregion

                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(tx);

                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    var layerGroups = plines.GroupBy(x => x.Layer);

                    foreach (var group in layerGroups)
                    {
                        string layerName = group.Key;

                        string labelRecipe =
                            ReadStringParameterFromDataTable(layerName, dtK, "Label", 0);

                        if (labelRecipe.IsNoE())
                        {
                            prdDbg($"Layer {layerName} does not have a recipe for labels defined! Skipping...");
                            continue;
                        }

                        LayerTableRecord ltr = lt[layerName].Go<LayerTableRecord>(tx);

                        //Create labels
                        foreach (var pline in group)
                        {
                            //Filter plines to avoid overpopulation
                            if (pline.Length < 5.0) continue;

                            //Compose label
                            string label = ConstructStringFromPSByRecipe(pline, labelRecipe);

                            //quick hack
                            if (
                                label == "ø0 - " ||
                                label == "ø0" ||
                                label == "ø - " ||
                                label == "ø0 - Uoplyst") continue;

                            //Create text object
                            DBText textEnt = new DBText();
                            textEnt.Layer = labelLayer;
                            textEnt.Color = ltr.Color;
                            textEnt.TextString = label;
                            textEnt.Height = labelHeight;

                            //Manage position
                            Point3d cen = pline.GetPointAtDist(pline.Length / 2);
                            var deriv = pline.GetFirstDerivative(cen);
                            var perp = deriv.GetPerpendicularVector();
                            Point3d loc = cen + perp * labelOffset;

                            Vector3d normal = new Vector3d(0.0, 0.0, 1.0);
                            double rotation = UtilsCommon.Utils.GetRotation(deriv, normal);

                            textEnt.HorizontalMode = TextHorizontalMode.TextCenter;
                            textEnt.VerticalMode = TextVerticalMode.TextBottom;

                            textEnt.Rotation = rotation;
                            textEnt.AlignmentPoint = loc;

                            textEnt.AddEntityToDbModelSpace(localDb);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("PRINTPIPEVOLUMESUMMARY")]
        public static void PrintPipeVolumeSummaryAllInOne()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // Prompt the user whether single pipes are drawn as one polyline.
            PromptKeywordOptions pko = new PromptKeywordOptions("\nAre single pipes (ENKELT) drawn as one polyline (representing both pipes)? [Yes/No] <Yes>:");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.Keywords.Default = "Yes";
            pko.AllowNone = true;
            PromptResult pr = ed.GetKeywords(pko);
            if (pr.Status != PromptStatus.OK) return;
            bool singleAsOne = (pr.StringResult == "Yes" || pr.StringResult == "");

            // Collect all polylines from Model Space.
            List<Polyline> polylines = new List<Polyline>();
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tx.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord modelSpace = tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                foreach (ObjectId objId in modelSpace)
                {
                    Entity ent = tx.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (ent is Polyline pl)
                        polylines.Add(pl);
                }
                tx.Commit();
            }

            if (polylines.Count == 0)
            {
                ed.WriteMessage("\nNo polylines found in model space.");
                return;
            }

            // We'll aggregate data into a dictionary keyed by (system, type, DN).
            // If the type is FREM or RETUR, we'll recode it as "ENKELT".
            Dictionary<(string system, string type, int dn), (double totalLength, double totalVolume)> summary =
                new Dictionary<(string, string, int), (double, double)>();

            double overallLength = 0.0;
            double overallVolume = 0.0;

            foreach (Polyline pl in polylines)
            {
                // Get system, type, and DN using your PipeScheduleV2 methods.
                PipeSystemEnum systemEnum = GetPipeSystem(pl);
                PipeTypeEnum typeEnum = GetPipeType(pl);
                int dn = GetPipeDN(pl);
                if (dn == 0)
                {
                    ed.WriteMessage($"\nWarning: Could not determine DN for entity {pl.Handle}");
                    continue;
                }

                // Convert system "DN" to "STÅL"
                string sysString = GetSystemString(systemEnum);
                if (sysString.Equals("DN", StringComparison.OrdinalIgnoreCase))
                    sysString = "STÅL";

                // Recode FREM/RETUR as "ENKELT". Use the original type for Twin.
                string finalType;
                if (typeEnum == PipeTypeEnum.Frem || typeEnum == PipeTypeEnum.Retur)
                    finalType = "ENKELT";
                else
                    finalType = typeEnum.ToString();

                // Get the internal diameter (in mm) and convert to meters.
                double idMm = GetPipeId(pl);
                if (idMm <= 0.0)
                {
                    ed.WriteMessage($"\nWarning: Could not find internal diameter for entity {pl.Handle}");
                    continue;
                }
                double idMeters = idMm / 1000.0;
                double area = Math.PI * Math.Pow(idMeters, 2) / 4.0; // m²
                double length = pl.Length; // in m

                // If pipes of type ENKELT are drawn as one polyline, multiply the length by 2.
                if (finalType.Equals("ENKELT", StringComparison.OrdinalIgnoreCase) && singleAsOne)
                    length *= 2.0;

                double volume = area * length; // m³

                if (finalType.Equals("Twin", StringComparison.OrdinalIgnoreCase))
                    volume *= 2.0;

                var key = (system: sysString, type: finalType, dn: dn);
                if (summary.ContainsKey(key))
                {
                    var current = summary[key];
                    summary[key] = (current.totalLength + length, current.totalVolume + volume);
                }
                else
                {
                    summary.Add(key, (length, volume));
                }

                overallLength += length;
                overallVolume += volume;
            }

            if (summary.Count == 0)
            {
                ed.WriteMessage("\nNo valid pipe items found.");
                return;
            }

            // Sort the summary entries.
            // 1) "STÅL" appears first; then other systems sorted alphabetically.
            // 2) Within the same system, sort by DN descending.
            // 3) Within same system and DN, "ENKELT" appears before "Twin".
            var sortedEntries = summary.OrderBy(entry =>
                    entry.Key.system.Equals("STÅL", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(entry => entry.Key.system, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(entry => entry.Key.dn)
                .ThenBy(entry => entry.Key.type.Equals("ENKELT", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();

            // Print header with increased spacing.
            ed.WriteMessage("\nPipe Volume Summary (PipeScheduleV2):");
            ed.WriteMessage("\n--------------------------------------");
            string header = string.Format("{0,-30}{1,19}{2,14}",
                "Pipe (System-Type-DN)", "Pipe Length (m)", "Volume (m³)");
            ed.WriteMessage("\n" + header);

            // Print each sorted entry (one aggregated line per unique key, no extra blank lines)
            foreach (var entry in sortedEntries)
            {
                string label = $"{entry.Key.system}-{entry.Key.type}-DN{entry.Key.dn}";
                double len = Math.Round(entry.Value.totalLength, 1);
                double vol = Math.Round(entry.Value.totalVolume, 1);
                string line = string.Format("{0,-30}{1,19:0.0}{2,14:0.0}",
                    label, len, vol);
                ed.WriteMessage("\n" + line);
            }

            // Print total line.
            double totLen = Math.Round(overallLength, 1);
            double totVol = Math.Round(overallVolume, 1);
            string totalLine = string.Format("{0,-30}{1,19:0.0}{2,14:0.0}",
                "TOTAL", totLen, totVol);
            ed.WriteMessage("\n" + totalLine);
        }

        /// <command>CREATEVEJMIDTETEXT</command>
        /// <summary>
        /// Creates labels for vejmidte lines.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("CREATEVEJMIDTETEXT")]
        public void createvejmidtetext()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            string layNavn = "Vejnavne";

            double distance = 0;
            distance = Interaction.GetDistance(
                "Specify distance from the center of the line to the text: ");

            if (double.IsNaN(distance) || distance <= 0)
            {
                prdDbg("Invalid distance specified.");
                return;
            }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Fix standard text
                    TextStyleTable tst = tx.GetObject(localDb.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
                    if (tst == null)
                    {
                        prdDbg("TextStyleTable is null.");
                        tx.Abort();
                        return;
                    }

                    var standard = tst["Standard"].Go<TextStyleTableRecord>(tx, OpenMode.ForWrite);
                    if (standard == null)
                    {
                        prdDbg("Standard text style is null.");
                        tx.Abort();
                        return;
                    }

                    standard.BigFontFileName = "Arial.ttf";
                    standard.TextSize = 1.6;
                    #endregion

                    Func<Entity, string> reader = ent =>
                        PropertySetManager.ReadNonDefinedPropertySetString(ent, "NavngivenVej", "vejnavn");

                    localDb.CheckOrCreateLayer(layNavn);

                    var plines = localDb.HashSetOfType<Polyline>(tx);

                    foreach (var pline in plines)
                    {
                        string vejNavn = reader(pline);
                        if (vejNavn.IsNoE()) continue;

                        double plLength = pline.Length;
                        if (plLength < Tolerance.Global.EqualPoint)
                        {
                            prdDbg($"Polyline {pline.Handle} is too short.");
                            continue;
                        }

                        int textCount = (int)(plLength / distance);
                        if (textCount < 1) textCount = 1;

                        double spacing = distance;
                        if (textCount == 1)
                        {
                            double param = pline.GetParameterAtDistance(plLength / 2);
                            Point3d pos = pline.GetPointAtParameter(param);
                            Vector3d dir = pline.GetFirstDerivative(pos).GetNormal();

                            AddText(localDb, layNavn, vejNavn, pos, dir.AngleOnPlane(new Plane()));
                        }
                        else
                        {
                            double totalTextLength = (textCount - 1) * spacing;
                            double startDist = (plLength - totalTextLength) / 2;

                            for (int i = 0; i < textCount; i++)
                            {
                                double dist = startDist + i * spacing;
                                if (dist > plLength) break;

                                Point3d pos = pline.GetPointAtDist(dist);
                                Vector3d dir = pline.GetFirstDerivative(pos).GetNormal();

                                AddText(localDb, layNavn, vejNavn, pos, dir.AngleOnPlane(new Plane()));
                            }
                        }
                    }

                    void AddText(Database db, string layer, string textStr, Point3d pos, double rotation)
                    {
                        DBText text = new DBText
                        {
                            Position = pos,
                            TextString = textStr,
                            Height = 1.6,
                            Rotation = rotation,
                            Layer = layer,
                            HorizontalMode = TextHorizontalMode.TextCenter,
                            VerticalMode = TextVerticalMode.TextVerticalMid,
                            AlignmentPoint = pos
                        };

                        text.AddEntityToDbModelSpace(db);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("BBRFROMPTSDE")]
        public void bbrfromptsde()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbr = new PSetDefs.BBR();

                var pts = localDb.HashSetOfType<DBPoint>(tx);
                foreach (var pt in pts)
                {
                    var id = ReadIntPropertyValue(
                        tables, pt.ObjectId, "Leistungsangaben_Oststadt", "FeatId");

                    var last = ReadDoublePropertyValue(
                        tables, pt.ObjectId, "Leistungsangaben_Oststadt", "LeistungMV");

                    var br = localDb.CreateBlockWithAttributes("Naturgas", pt.Position);

                    psm.WritePropertyString(br, bbr.Adresse, id.ToString());
                    psm.WritePropertyObject(br, bbr.EstimeretVarmeForbrug, last);
                }
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }

            tx.Commit();
        }

        [CommandMethod("BBRFROMPTS")]
        public void bbrfrompts()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbr = new PSetDefs.BBR();

                var pts = localDb.HashSetOfType<DBPoint>(tx);

                var plines = localDb.HashSetOfType<Polyline>(tx);

                var ptsByDist = pts.SelectMany(p => plines.Select(y => new
                {
                    Pline = y,
                    Point = p,
                    Dist = y.GetDistToPoint(p.Position, false),
                    Parameter = y.GetParameterAtPoint(
                        y.GetClosestPointTo(p.Position, false))
                }))
                    .GroupBy(x => x.Point)
                    .Select(x => x.MinBy(y => y.Dist)!);

                var startNode = plines
                    .Where(x => plines.Any(y => x.StartPoint.Equalz(y.StartPoint) ||
                    x.StartPoint.Equalz(y.EndPoint)))
                    .FirstOrDefault();

                if (startNode == null) { prdDbg("No start node found!"); tx.Abort(); return; }

                var stack = new Stack<Polyline>();
                stack.Push(startNode);

                int vidx = 1;
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    var connected = plines
                        .Where(x => x != current &&
                        current.EndPoint.Equalz(x.StartPoint)); //tree structure assumed                            

                    foreach (var conn in connected)
                    {
                        stack.Push(conn);
                    }

                    var closePts = ptsByDist
                        .Where(x => x.Pline == current)
                        .OrderBy(x => x.Parameter);

                    int ndix = 1;
                    foreach (var pt in closePts)
                    {
                        var br = localDb.CreateBlockWithAttributes("Naturgas", pt.Point.Position);
                        psm.WritePropertyString(br, bbr.Adresse, $"STR{vidx.ToString("00")} {ndix.ToString("00")}");
                        psm.WritePropertyString(br, bbr.Type, "Naturgas");
                        psm.WritePropertyString(br, bbr.DistriktetsNavn, "Verificering");
                        psm.WritePropertyString(br, bbr.id_lokalId, Guid.NewGuid().ToString());
                        psm.WritePropertyObject(br, bbr.EstimeretVarmeForbrug, GetRandForbrug());

                        ndix++;
                    }

                    double GetRandForbrug()
                    {
                        var rnd = new Random();
                        return 15 + rnd.NextDouble() * (25 - 15);
                    }

                    vidx++;
                }
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }

            tx.Commit();
        }

        /// <command>FIXBROKENLABELS</command>
        /// <summary>
        /// Sletter korrupte projection labels i tegningen. Problemet er set med nogle længdeprofiler,
        /// hvor labels er blevet korrupte og ikke kan læses korrekt.
        /// Programmet laver fejl når den tilgår projicerede punkter via deres labels.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("FIXBROKENLABELS")]
        public void fixbrokenlabels()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var labels = localDb.HashSetOfType<ProfileProjectionLabel>(tx);

                var toDelete = new HashSet<ProfileProjectionLabel>();

                foreach (var label in labels)
                {
                    try
                    {
                        var x = label.LabelLocation.X;
                    }
                    catch (System.Exception)
                    {
                        toDelete.Add(label);
                        continue;
                    }
                }

                foreach (var label in toDelete)
                {
                    label.UpgradeOpen();
                    label.Erase(true);
                }
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }

            tx.Commit();
        }

        /// <command>CHECKALLPOLYLINERADII</command>
        /// <summary>
        /// Gennemgår alle 2D-polylinjer i tegningen, eksploderer dem midlertidigt for at finde bue-segmenter,
        /// beregner hver arcs radius og udskriver den mindste bue-radius pr. lag i kommandolinjen.
        /// Tegningen forbliver uændret, da eksploderingen kun foregår i hukommelsen.
        /// </summary>
        /// <category>Utilities</category>

        [CommandMethod("CHECKALLPOLYLINERADII")]
        public void checkallpolylineradii()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                //Get all polylines
                var pls = localDb.GetFjvPipes(tx);

                var arcsWithRadius = new List<(string layer, double radius, Polyline originalPl)>();

                foreach (var p in pls)
                {
                    using var exploded = new DBObjectCollection();
                    p.Explode(exploded);

                    foreach (DBObject obj in exploded)
                    {
                        if (obj is Arc arc)
                        {
                            arcsWithRadius.Add((p.Layer, arc.Radius, p));
                        }

                        obj.Dispose();
                    }
                }

                var gps = arcsWithRadius
                    .GroupBy(x => x.layer)
                    .OrderBy(x => GetPipeSystem(x.Key))
                    .ThenBy(x => GetPipeDN(x.Key));

                var rows = gps.Select(gp =>
                {
                    double fr = gp.Min(x => x.radius);
                    double tr =
                    GetPipeMinElasticRadiusHorizontalCharacteristic(
                        GetPipeSystem(gp.Key),
                        GetPipeDN(gp.Key),
                        GetPipeType(gp.Key),
                        false);
                    string status = fr >= tr ? "OK" : "ERROR";
                    string handles = status == "OK"
                        ? ""
                        : string.Join(", ", gp
                            .Where(x => x.radius < tr)
                            .Select(x => x.originalPl.Handle.ToString())
                            .Distinct());

                    return new object[]
                    {
                        gp.Key,     // Layer
                        fr.ToString("#.0"),         // FR (Faktisk radius)
                        tr,         // TR (Tilladt radius)
                        status,     // Status
                        handles     // Handles (if any)
                    };
                });

                prdDbg("Arc radius analysis:");
                prdDbg("FR = Faktisk min. radius");
                prdDbg("TR = Tilladt min. radius");

                string[] hdrs = ["Layer", "FR", "TR", "Status", "Handles"];

                PrintTable(hdrs, rows);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Abort();
        }

        /// <command>CENTERVIEWFRAMENUMBER</command>
        /// <summary>
        /// Centrerer tal-teksten i Civil 3D View Frame label-stilen "Basic".
        /// Alle tekstkomponenter sættes til vedhæftning MiddleCenter med 0 i X/Y-offset.
        /// Stilen hentes fra det aktive CivilDocument; geometri ændres ikke.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("CENTERVIEWFRAMENUMBER")]
        public void CenterViewFrameNumber()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var cdoc = CivilApplication.ActiveDocument;

                // Get the View Frame label style root
                var vfRoot = cdoc.Styles.LabelStyles.ViewFrameLabelStyles;

                // Try to get the "Basic" style (change name if needed)
                if (!vfRoot.LabelStyles.Contains("Basic"))
                {
                    prdDbg("View Frame label style 'Basic' not found.");
                    tx.Abort();
                    return;
                }

                var styleId = vfRoot.LabelStyles["Basic"];
                var style = (LabelStyle)tx.GetObject(styleId, OpenMode.ForWrite);

                prdDbg($"Number of text components: {style.GetComponentsCount(LabelStyleComponentType.Text)}");

                // Get all TEXT components and adjust them
                foreach (ObjectId compId in style.GetComponents(LabelStyleComponentType.Text))
                {
                    var txt = (LabelStyleTextComponent)tx.GetObject(compId, OpenMode.ForWrite);

                    txt.Text.Attachment.Value = LabelTextAttachmentType.MiddleCenter;
                    txt.Text.XOffset.Value = 0.0;
                    txt.Text.YOffset.Value = 0.0;
                }

                prdDbg("View Frame label style 'Basic' centered successfully.");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }

            tx.Abort();
        }

        private static double _lastTangentArcRadius = 2.5;

        [CommandMethod("TANGENTARCFROMLINE")]
        public void TangentArcFromLine()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptDistanceOptions arcRadiusOptions = new PromptDistanceOptions("\nEnter arc radius");
            arcRadiusOptions.AllowNegative = false;
            arcRadiusOptions.AllowNone = false;
            arcRadiusOptions.DefaultValue = _lastTangentArcRadius;
            arcRadiusOptions.UseDefaultValue = true;

            PromptDoubleResult arcRadiusResult = ed.GetDistance(arcRadiusOptions);
            if (arcRadiusResult.Status != PromptStatus.OK)
                return;

            double arcRadius = arcRadiusResult.Value;
            _lastTangentArcRadius = arcRadius;

            var baseLineOptions = new PromptEntityOptions("\nSelect line");
            baseLineOptions.SetRejectMessage("\nNot a line");
            baseLineOptions.AddAllowedClass(typeof(Line), exactMatch: true);

            var baseLineResult = ed.GetEntity(baseLineOptions);
            if (baseLineResult.Status != PromptStatus.OK)
                return;

            Point3d startPoint;
            Point3d endPoint;
            double baseLineAngle;
            Line baseLineEntity;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    baseLineEntity = (Line)tr.GetObject(baseLineResult.ObjectId, OpenMode.ForRead);

                    startPoint = baseLineEntity.StartPoint;
                    endPoint = baseLineEntity.EndPoint;
                    baseLineAngle = baseLineEntity.Angle;

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tr.Abort();
                    return;
                }
            }

            var directionPointOptions = new PromptPointOptions("\nSelect direction");

            var directionPointResult = ed.GetPoint(directionPointOptions);
            if (directionPointResult.Status != PromptStatus.OK)
                return;

            Point3d directionPoint = directionPointResult.Value;
            Point3d midPoint = startPoint.MidPoint(endPoint);

            Vector3d baseLineVectorUnit = startPoint.GetVectorTo(endPoint).GetNormal();
            Vector3d perpendicularVectorUnit = baseLineVectorUnit.RotateBy(Math.PI / 2, Vector3d.ZAxis);
            Vector3d directionVectorUnit = midPoint.GetVectorTo(directionPoint).GetNormal();

            double angleToBase = directionVectorUnit.GetAngleTo(baseLineVectorUnit);
            double angleToPerpendicular = directionVectorUnit.GetAngleTo(perpendicularVectorUnit);

            Point3d arcCenterPoint;
            double arcStartAngle;
            double arcEndAngle;

            if (angleToBase < Math.PI / 2 && angleToPerpendicular < Math.PI / 2)
            {
                arcCenterPoint = endPoint + perpendicularVectorUnit.MultiplyBy(arcRadius);
                arcStartAngle = baseLineAngle - Math.PI / 2;
                arcEndAngle = arcStartAngle + Math.PI / 4;
            }
            else if (angleToBase > Math.PI / 2 && angleToPerpendicular < Math.PI / 2)
            {
                arcCenterPoint = startPoint + perpendicularVectorUnit.MultiplyBy(arcRadius);
                arcEndAngle = baseLineAngle - Math.PI / 2;
                arcStartAngle = arcEndAngle - Math.PI / 4;
            }
            else if (angleToBase > Math.PI / 2 && angleToPerpendicular > Math.PI / 2)
            {
                arcCenterPoint = startPoint - perpendicularVectorUnit.MultiplyBy(arcRadius);
                arcStartAngle = baseLineAngle + Math.PI / 2;
                arcEndAngle = arcStartAngle + Math.PI / 4;
            }
            else if (angleToBase < Math.PI / 2 && angleToPerpendicular > Math.PI / 2)
            {
                arcCenterPoint = endPoint - perpendicularVectorUnit.MultiplyBy(arcRadius);
                arcEndAngle = baseLineAngle + Math.PI / 2;
                arcStartAngle = arcEndAngle - Math.PI / 4;
            }
            else
            {
                prdDbg("Could not determine quadrant");
                return;
            }

            Arc arc = new Arc(arcCenterPoint, arcRadius, arcStartAngle, arcEndAngle);

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    btr.AppendEntity(arc);
                    tr.AddNewlyCreatedDBObject(arc, true);

                    tr.Commit();
                }
                catch(System.Exception ex)
                {
                    prdDbg(ex);
                    tr.Abort();
                    return;
                }
            }
        }
    }
}