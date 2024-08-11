﻿using Autodesk.AutoCAD.ApplicationServices;
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

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("DELETEWELDPOINTS")]
        [CommandMethod("DWP")]
        public void deleteweldpoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                string blockLayerName = "0-SVEJSEPKT";
                string blockName = "SVEJSEPUNKT";
                string textLayerName = "0-DEBUG-TXT";
                //////////////////////////////////////

                #region Delete previous blocks
                //Delete previous blocks
                var existingBlocks = localDb.GetBlockReferenceByName(blockName);
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                //Delete previous blocks
                existingBlocks = localDb.GetBlockReferenceByName(blockName + "-NOTXT");
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                #endregion
                tx.Commit();
            }
        }

        [CommandMethod("CREATEWELDPOINTS")]
        [CommandMethod("CWP")]
        public void createweldpoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            System.Data.DataTable dt = CsvData.FK;

            bool noNumbers = true;
            string[] kwds = new string[] { "Uden nummer", "Med nummer" };
            string blockChoice = Interaction.GetKeywords(
                "Skal svejsepunkter være med nummer?", kwds);
            if (blockChoice == "Med nummer") noNumbers = false;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Open alignment db
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database alDb = new Database(false, true);
                alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction alTx = alDb.TransactionManager.StartTransaction();
                HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                #endregion

                #region Initialize property set
                PropertySetManager psm = new PropertySetManager(
                    localDb,
                    PSetDefs.DefinedSets.DriPipelineData);
                PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
                #endregion

                try
                {
                    //////////////////////////////////////
                    string blockLayerName = "0-SVEJSEPKT";
                    string blockName = noNumbers ? "SVEJSEPUNKT-NOTXT" : "SVEJSEPUNKT";
                    string textLayerName = "0-DEBUG-TXT";
                    //////////////////////////////////////
                    
                    #region Delete previous blocks
                    //Delete previous blocks
                    var existingBlocks = localDb.GetBlockReferenceByName(blockName);
                    foreach (BlockReference br in existingBlocks)
                    {
                        br.CheckOrOpenForWrite();
                        br.Erase(true);
                    }
                    #endregion

                    #region Create layer for weld blocks and
                    localDb.CheckOrCreateLayer(blockLayerName);
                    localDb.CheckOrCreateLayer(textLayerName);
                    #endregion

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    #region Import weld block if missing
                    if (!bt.Has(blockName))
                    {
                        prdDbg("Block for weld annotation is missing! Importing...");
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        idsToClone.Add(sourceBt[blockName]);

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion

                    //List to gather ALL weld points
                    var wps = new List<WeldPointData>();

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        ////////////////////////////////////////////
                        //if (al.Name != "10 Juni Allé") continue;
                        ////////////////////////////////////////////
                        prdDbg($"\nProcessing: {al.Name}...");
                        //Cache the alingment polyline
                        Polyline alPoly = al.GetPolyline().Go<Polyline>(alTx);

                        #region GetCurvesAndBRs from fremtidig
                        HashSet<Curve> curves = localDb.ListOfType<Curve>(tx, true)
                            .Where(x => psm.FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                            .ToHashSet();
                        HashSet<BlockReference> brs = localDb.ListOfType<BlockReference>(tx, true)
                            .Where(x => psm.FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                            .ToHashSet();
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                        #endregion

                        //Check to see if sequences contains anything
                        if (curves.Count == 0 && brs.Count == 0)
                        {
                            prdDbg($"SKIP: Alignment {al.Name} does not have any components in the drawing!");
                            continue;
                        }
                        if (curves.Count == 0 && brs.Count != 0)
                        {
                            prdDbg($"ERROR: Alignment {al.Name} has only {brs.Count} block(s) and 0 curves!");
                            continue;
                        }

                        TypeOfIteration iterType = 0;

                        //Sort curves according to their DN -> bigger DN at start
                        Queue<Curve> kø = Utils.GetSortedQueue(localDb, al, curves, ref iterType);

                        #region Gather weldpoints for curves
                        double pipeStdLength = 0;
                        while (kø.Count > 0)
                        {
                            Curve curve = kø.Dequeue();
                            pipeStdLength = GetPipeStdLength(curve);
                            double pipeLength = curve.GetDistanceAtParameter(curve.EndParam);
                            double division = pipeLength / pipeStdLength;
                            int nrOfSections = (int)division;
                            double remainder = division - nrOfSections;

                            //if (string.Equals(curve.Handle.ToString(), "19caf", StringComparison.OrdinalIgnoreCase))
                            //{
                            //    prdDbg($"pipeStdLength: {pipeStdLength}");
                            //    prdDbg($"pipeLength: {pipeLength}");
                            //    prdDbg($"Division: {division}");
                            //    prdDbg($"nrOfSections: {nrOfSections}");
                            //    prdDbg($"remainder: {remainder}");
                            //    prdDbg($"QA: {nrOfSections * pipeStdLength + remainder * pipeStdLength} = {pipeLength}");
                            //}

                            for (int j = 1; j < nrOfSections + 1; j++)
                            {//1 to skip start, which is handled separately
                                Point3d wPt = curve.GetPointAtDist(j * pipeStdLength);
                                //Point3d temp = alPoly.GetClosestPointTo(wPt, false);
                                double station = 0;
                                double offset = 0;
                                try
                                {
                                    al.StationOffset(wPt.X, wPt.Y, ref station, ref offset);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(al.Name);
                                    prdDbg(wPt.ToString());
                                    prdDbg(curve.Handle.ToString());
                                    throw;
                                }
                                //Create weldpoint
                                wps.Add(new WeldPointData()
                                {
                                    WeldPoint = wPt,
                                    Alignment = al,
                                    IterationType = iterType,
                                    Station = station,
                                    SourceEntity = curve,
                                    DN = GetPipeDN(curve),
                                    System = GetPipeType(curve, true).ToString(),
                                    Serie = GetPipeSeriesV2(curve, true).ToString()
                                });
                            }

                            { //Extra scope to localise variables station and offset
                                //Handle start and end points separately
                                double station = 0;
                                double offset = 0;

                                Point3d pt = curve.GetPointAtParameter(curve.StartParam);
                                try
                                {
                                    al.StationOffset(pt.X, pt.Y, ref station, ref offset);
                                }
                                catch (System.Exception ex)
                                {
                                    prdDbg("Failing point: " + pt);
                                    prdDbg("Curve: " + curve.Handle);
                                    throw;
                                }
                                wps.Add(new WeldPointData()
                                {
                                    WeldPoint = curve.GetPointAtParameter(curve.StartParam),
                                    Alignment = al,
                                    IterationType = iterType,
                                    Station = station,
                                    SourceEntity = curve,
                                    DN = GetPipeDN(curve),
                                    System = GetPipeType(curve, true).ToString(),
                                    Serie = GetPipeSeriesV2(curve, true).ToString()
                                });


                                pt = curve.GetPointAtParameter(curve.EndParam);
                                al.StationOffset(pt.X, pt.Y, ref station, ref offset);
                                wps.Add(new WeldPointData()
                                {
                                    WeldPoint = curve.GetPointAtParameter(curve.EndParam),
                                    Alignment = al,
                                    IterationType = iterType,
                                    Station = station,
                                    SourceEntity = curve,
                                    DN = GetPipeDN(curve),
                                    System = GetPipeType(curve, true).ToString(),
                                    Serie = GetPipeSeriesV2(curve, true).ToString()
                                });
                            }
                            #region Debug
                            //if (curve is Polyline pline)
                            //{
                            //    Point3d midPoint = pline.GetPointAtDist(pline.Length / 2);
                            //    DBText text = new DBText();
                            //    text.SetDatabaseDefaults();
                            //    text.TextString = (i + 1).ToString("D2");
                            //    text.Height = 10;
                            //    text.Position = midPoint;
                            //    text.Layer = textLayerName;
                            //    text.AddEntityToDbModelSpace(localDb);
                            //} 
                            #endregion
                        }
                        #endregion

                        #region Gather weldpoints for blocks
                        foreach (BlockReference br in brs)
                        {
                            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                            foreach (Oid oid in btr)
                            {
                                if (!oid.IsDerivedFrom<BlockReference>()) continue;
                                BlockReference nestedBr = oid.Go<BlockReference>(tx);
                                if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                                Point3d wPt = nestedBr.Position;
                                wPt = wPt.TransformBy(br.BlockTransform);

                                #region Read DN
                                int DN = 0;
                                bool parseSuccess = false;
                                if (nestedBr.Name.Contains("BRANCH"))
                                {
                                    parseSuccess = int.TryParse(
                                        ComponentSchedule.ReadComponentDN2(br, dt), out DN);
                                }
                                else //Else catches "MAIN" and ordinary case
                                {
                                    parseSuccess = int.TryParse(
                                        ComponentSchedule.ReadComponentDN1(br, dt), out DN);
                                }

                                if (!parseSuccess)
                                {
                                    prdDbg($"ERROR: Parsing of DN failed for block handle: {br.Handle}! " +
                                        $"Returned value: {ComponentSchedule.ReadComponentDN1(br, dt)}");
                                }
                                #endregion

                                #region Read System
                                //string system = ODDataReader.DynKomponenter.ReadComponentSystem(br, komponenter).StrValue;
                                string system = br.ReadDynamicCsvProperty(DynamicProperty.System);

                                if (system.IsNoE())
                                {
                                    prdDbg($"ERROR: Parsing of DN failed for block handle: {br.Handle}!");
                                    system = "";
                                }
                                #endregion

                                #region Determine correct alignment name
                                //This is to mitigate parallelafgreninger which place
                                //Branch weld on the wrong alignment
                                Alignment alignment = al;
                                if (br.RealName() == "PA TWIN S3" ||
                                    br.RealName() == "T ENKELT S3" ||
                                    br.RealName() == "T TWIN S3")
                                {
                                    HashSet<(double dist, Alignment al)> alDistTuples =
                                        new HashSet<(double, Alignment)>();
                                    try
                                    {
                                        foreach (Alignment newAl in als)
                                        {
                                            Polyline pline = newAl.GetPolyline().Go<Polyline>(alTx);
                                            Point3d tempP3d = pline.GetClosestPointTo(wPt, false);
                                            alDistTuples.Add((tempP3d.DistanceHorizontalTo(wPt), newAl));
                                        }
                                    }
                                    catch (System.Exception ex)
                                    {
                                        //prdDbg(ex);
                                        prdDbg("Error in GetClosestPointTo -> loop incomplete!");
                                    }

                                    alignment = alDistTuples.MinByEnumerable(x => x.dist).FirstOrDefault().al;
                                }
                                #endregion

                                double station = 0;
                                double offset = 0;

                                Point3d tempPt = alignment.GetPolyline().Go<Polyline>(alTx).GetClosestPointTo(wPt, false);

                                try
                                {
                                    alignment.StationOffset(tempPt.X, tempPt.Y, ref station, ref offset);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg(wPt.ToString());
                                    throw;
                                }

                                wps.Add(new WeldPointData()
                                {
                                    WeldPoint = wPt,
                                    Alignment = alignment,
                                    IterationType = iterType,
                                    Station = station,
                                    SourceEntity = br,
                                    DN = DN,
                                    System = system,
                                    Serie = ComponentSchedule.ReadDynamicCsvProperty(br, DynamicProperty.Serie)
                                });
                            }
                        }
                        #endregion

                        System.Windows.Forms.Application.DoEvents();
                    }

                    #region Place weldpoints
                    var ordered = wps.OrderBy(x => x.WeldPoint.X).ThenBy(x => x.WeldPoint.Y);
                    IEnumerable<IGrouping<WeldPointData, WeldPointData>> clusters
                        = ordered.GroupByCluster((x, y) => GetDistance(x, y), 0.02);

                    double GetDistance(WeldPointData first, WeldPointData second)
                    {
                        return first.WeldPoint.DistanceHorizontalTo(second.WeldPoint);
                    }

                    var distinct = clusters.Select(x => x.First());
                    var groupedByAlignment = distinct.GroupBy(x => x.Alignment.Name);

                    //Prepare modelspace
                    BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();
                    modelSpace.CheckOrOpenForWrite();
                    //Prepare block table record
                    if (!bt.Has(blockName)) throw new System.Exception("Block for weld points is missing!");
                    Oid btrId = bt[blockName];
                    BlockTableRecord btrWp = btrId.Go<BlockTableRecord>(tx);
                    List<AttributeDefinition> attDefs = new List<AttributeDefinition>();
                    foreach (Oid arOid in btrWp)
                    {
                        if (!arOid.IsDerivedFrom<AttributeDefinition>()) continue;
                        AttributeDefinition at = arOid.Go<AttributeDefinition>(tx);
                        if (!at.Constant) attDefs.Add(at);
                    }

                    foreach (var alGroup in groupedByAlignment.OrderBy(x => x.Key))
                    {
                        prdDbg($"Placing welds for alignment: {alGroup.First().Alignment.Name}...");
                        System.Windows.Forms.Application.DoEvents();
                        IOrderedEnumerable<WeldPointData> orderedByDist;
                        if (alGroup.First().IterationType == TypeOfIteration.Forward)
                            orderedByDist = alGroup.OrderBy(x => x.Station);
                        else orderedByDist = alGroup.OrderByDescending(x => x.Station);

                        Regex regex = new Regex(@"(?<number>^\d{2,3}\s)");
                        string currentPipelineNumber = "";
                        if (regex.IsMatch(alGroup.First().Alignment.Name))
                        {
                            Match match = regex.Match(alGroup.First().Alignment.Name);
                            currentPipelineNumber = match.Groups["number"].Value;
                        }

                        int idx = 1;
                        foreach (var wp in orderedByDist)
                        {
                            Polyline pline = wp.Alignment.GetPolyline().Go<Polyline>(alTx);
                            Point3d temp = pline.GetClosestPointTo(wp.WeldPoint, false);
                            double station = 0;
                            double offset = 0;
                            wp.Alignment.StationOffset(temp.X, temp.Y, ref station, ref offset);
                            Vector3d deriv = pline.GetFirstDerivative(pline.GetClosestPointTo(wp.WeldPoint, false));
                            double rotation = Math.Atan2(deriv.Y, deriv.X);
                            //BlockReference wpBr = localDb.CreateBlockWithAttributes(blockName, wp.WeldPoint, rotation);
                            var wpBr = new BlockReference(wp.WeldPoint, btrId);
                            wpBr.Rotation = rotation;
                            wpBr.Layer = blockLayerName;

                            modelSpace.AppendEntity(wpBr);
                            tx.AddNewlyCreatedDBObject(wpBr, true);

                            foreach (AttributeDefinition attDef in attDefs)
                            {
                                AttributeReference atRef = new AttributeReference();
                                atRef.SetAttributeFromBlock(attDef, wpBr.BlockTransform);
                                atRef.Position = attDef.Position.TransformBy(wpBr.BlockTransform);
                                atRef.TextString = attDef.getTextWithFieldCodes();
                                wpBr.AttributeCollection.AppendAttribute(atRef);
                                tx.AddNewlyCreatedDBObject(atRef, true);
                            }

                            if (!noNumbers)
                                wpBr.SetAttributeStringValue("NUMMER", currentPipelineNumber + "." + idx.ToString("D3"));

                            psm.WritePropertyString(wpBr, driPipelineData.BelongsToAlignment, wp.Alignment.Name);

                            //if (idx == 1) DisplayDynBlockProperties(editor, wpBr, wpBr.Name);
                            SetDynBlockProperty(wpBr, "Type", wp.DN.ToString());
                            SetDynBlockPropertyObject(wpBr, "System", (object)wp.System);
                            SetDynBlockProperty(wpBr, "Serie", wp.Serie);

                            try
                            {
                                wpBr.AttSync();
                            }
                            catch (System.Exception)
                            {
                                prdDbg("ERROR: " + wpBr.Position);
                                idx++;
                                continue;
                            }

                            idx++;
                        }
                    }
                    #endregion


                }
                catch (System.Exception ex)
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                alTx.Abort();
                alTx.Dispose();
                alDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("CREATESTIKPOINTS")]
        [CommandMethod("CSP")]
        public void createstikpoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //////////////////////////////////////
            string blockLayerName = "0-STIKKOMPONENTER";
            string stikAfgrBlockName = "STIKAFGRENING";
            string stikTeeBlockName = "STIKTEE";
            string textLayerName = "0-DEBUG-TXT";
            //////////////////////////////////////

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    #region Import stik blocks if missing
                    if (!bt.Has(stikAfgrBlockName) || !bt.Has(stikTeeBlockName))
                    {
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            //blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Dev.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();

                        if (!bt.Has(stikAfgrBlockName))
                            if (sourceBt.Has(stikAfgrBlockName))
                            {
                                prdDbg("Block for stik branch is missing! Importing...");
                                idsToClone.Add(sourceBt[stikAfgrBlockName]);
                            }

                        if (!bt.Has(stikTeeBlockName))
                            if (sourceBt.Has(stikTeeBlockName))
                            {
                                prdDbg("Block for stik tee is missing! Importing...");
                                idsToClone.Add(sourceBt[stikTeeBlockName]);
                            }

                        if (idsToClone.Count > 0)
                        {
                            IdMapping mapping = new IdMapping();
                            blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        }

                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Initialize property set
                PropertySetManager psm = new PropertySetManager(
                    localDb,
                    PSetDefs.DefinedSets.DriPipelineData);
                PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
                #endregion

                #region Delete previous blocks
                //Delete previous blocks
                var existingBlocks = localDb.GetBlockReferenceByName(stikAfgrBlockName);
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                existingBlocks = localDb.GetBlockReferenceByName(stikTeeBlockName);
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                #endregion

                try
                {
                    #region Create layer for weld blocks and
                    localDb.CheckOrCreateLayer(blockLayerName);
                    localDb.CheckOrCreateLayer(textLayerName);
                    #endregion

                    #region Read components file
                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    #endregion

                    HashSet<Polyline> allPipes = localDb.GetFjvPipes(tx);
                    var mainPipes = allPipes.Where(x => GetPipeSystem(x) == PipeSystemEnum.Stål);
                    HashSet<PipeSystemEnum> stikSystems =
                        new HashSet<PipeSystemEnum>() { PipeSystemEnum.AluPex, PipeSystemEnum.Kobberflex };
                    var stikPipes = allPipes.Where(x => stikSystems.Contains(GetPipeSystem(x)));

                    #region Place stik branch blocks
                    //First find all the main pipes which have at least one stik branch
                    var mainPipesWithStikBranch = mainPipes.Where(x => stikPipes.Any(y => y.IsConnectedTo(x)));
                    //Iterate over them and place blocks at branches
                    foreach (Polyline mainPipe in mainPipesWithStikBranch)
                    {
                        //Iterate over all connected stik pipes
                        foreach (Polyline stikPipe in stikPipes.Where(x => x.IsConnectedTo(mainPipe)))
                        {
                            //Determine the location of the connection
                            Point3d location = mainPipe.GetClosestPointTo(stikPipe.StartPoint, false);

                            //Determine rotation
                            Vector3d deriv = mainPipe.GetFirstDerivative(location);
                            double rotation = Math.Atan2(deriv.Y, deriv.X);

                            //Determine if block needs to be rotated 180°
                            Vector3d stikDeriv = stikPipe.GetFirstDerivative(stikPipe.StartPoint);
                            if (deriv.CrossProduct(stikDeriv).Z > 0) rotation += Math.PI;

                            //string stikName = psm.ReadPropertyString(stikPipe, driPipelineData.BelongsToAlignment);
                            //prdDbg($"{stikName} -> {deriv.CrossProduct(stikDeriv).Z}");

                            BlockReference br = localDb.CreateBlockWithAttributes(stikAfgrBlockName, location, rotation);

                            int mainPipeDn = GetPipeDN(mainPipe);
                            PipeTypeEnum type = GetPipeType(mainPipe);
                            string pipeType = "Enkelt";
                            switch (type)
                            {
                                case PipeTypeEnum.Ukendt:
                                    pipeType = "Twin";
                                    break;
                                case PipeTypeEnum.Twin:
                                    pipeType = "Twin";
                                    break;
                                case PipeTypeEnum.Frem:
                                case PipeTypeEnum.Retur:
                                    break;
                                default:
                                    break;
                            }

                            Utils.SetDynBlockPropertyObject(br, "StikType", mainPipeDn < 50 ? "Type 1" : "Type 2");
                            Utils.SetDynBlockPropertyObject(br, "DN1", (double)mainPipeDn);
                            Utils.SetDynBlockPropertyObject(br, "DN2", (double)GetPipeDN(stikPipe));
                            Utils.SetDynBlockPropertyObject(br, "System", pipeType);

                            string alignmentName = psm.ReadPropertyString(mainPipe, driPipelineData.BelongsToAlignment);
                            string stikName = psm.ReadPropertyString(stikPipe, driPipelineData.BelongsToAlignment);
                            psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, stikName);
                            psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, alignmentName);

                            br.AttSync();
                        }
                    }
                    #endregion

                    #region Place stik tee blocks
                    //Gather locations of points
                    HashSet<Graph.POI> pois = new HashSet<Graph.POI>();
                    foreach (var stik in stikPipes)
                    {
                        pois.Add(new Graph.POI(stik, stik.StartPoint.To2D(), EndType.Start,
                            null, null));
                        pois.Add(new Graph.POI(stik, stik.EndPoint.To2D(), EndType.End,
                            null, null));
                    }

                    var teeClusters = pois
                        .GroupByCluster((x, y) => x.Point.GetDistanceTo(y.Point), 0.01)
                        .Where(x => x.Count() > 2);

                    foreach (var cluster in teeClusters)
                    {
                        Graph.POI end = cluster.FirstOrDefault(x => x.EndType == EndType.End);
                        var starts = cluster.Where(x => x.EndType == EndType.Start);

                        //Rotation of block on main stikpipe
                        Curve curve = end.Owner as Curve;
                        Vector3d deriv = curve.GetFirstDerivative(curve.EndPoint);
                        double rotation = Math.Atan2(deriv.Y, deriv.X);

                        foreach (var item in starts)
                        {
                            Curve nextCurve = item.Owner as Curve;
                            Vector3d stikDeriv = nextCurve.GetFirstDerivative(nextCurve.StartPoint);
                            //First eliminate the parallel pipe
                            if (Math.Abs(deriv.CrossProduct(stikDeriv).Z) > 1.0)
                                if (deriv.CrossProduct(stikDeriv).Z > 0.0)
                                    rotation += Math.PI;
                        }

                        BlockReference br = localDb
                            .CreateBlockWithAttributes(stikTeeBlockName, curve.EndPoint, rotation);

                        int mainPipeDn = GetPipeDN(curve);
                        PipeTypeEnum type = GetPipeType(curve);
                        string pipeType = "Enkelt";
                        switch (type)
                        {
                            case PipeTypeEnum.Ukendt:
                                pipeType = "Twin";
                                break;
                            case PipeTypeEnum.Twin:
                                pipeType = "Twin";
                                break;
                            case PipeTypeEnum.Frem:
                            case PipeTypeEnum.Retur:
                                break;
                            default:
                                break;
                        }

                        double DN2; double DN3;
                        //First get DN of continuous pipe
                        var parallelPipe = starts.FirstOrDefault(x =>
                        {
                            Curve nextCurve = x.Owner as Curve;
                            Vector3d stikDeriv = nextCurve.GetFirstDerivative(nextCurve.StartPoint);
                            if (Math.Abs(deriv.CrossProduct(stikDeriv).Z) < 1.0) return true;
                            else return false;
                        });

                        if (parallelPipe != null) DN2 = GetPipeDN(parallelPipe.Owner);
                        else DN2 = 0.0;

                        //Then get DN of perpendicular pipe
                        var perpendicularPipe = starts.FirstOrDefault(x =>
                        {
                            Curve nextCurve = x.Owner as Curve;
                            Vector3d stikDeriv = nextCurve.GetFirstDerivative(nextCurve.StartPoint);
                            if (Math.Abs(deriv.CrossProduct(stikDeriv).Z) > 1.0) return true;
                            else return false;
                        });

                        if (perpendicularPipe != null) DN3 = GetPipeDN(perpendicularPipe.Owner);
                        else DN3 = 0.0;

                        Utils.SetDynBlockPropertyObject(br, "DN1", (double)mainPipeDn);
                        Utils.SetDynBlockPropertyObject(br, "DN2", DN2);
                        Utils.SetDynBlockPropertyObject(br, "DN3", DN3);
                        Utils.SetDynBlockPropertyObject(br, "System", pipeType);

                        string alignmentName = psm.ReadPropertyString(curve, driPipelineData.BelongsToAlignment);
                        psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, alignmentName);

                        br.AttSync();
                    }
                    #endregion

                    #region Place weldpoints
                    ////Prepare modelspace
                    //BlockTableRecord modelSpace = localDb.GetModelspaceForWrite();
                    //modelSpace.CheckOrOpenForWrite();
                    ////Prepare block table record
                    //if (!bt.Has(blockName)) throw new System.Exception("Block for weld points is missing!");
                    //Oid btrId = bt[blockName];
                    //BlockTableRecord btrWp = btrId.Go<BlockTableRecord>(tx);
                    //List<AttributeDefinition> attDefs = new List<AttributeDefinition>();
                    //foreach (Oid arOid in btrWp)
                    //{
                    //    if (!arOid.IsDerivedFrom<AttributeDefinition>()) continue;
                    //    AttributeDefinition at = arOid.Go<AttributeDefinition>(tx);
                    //    if (!at.Constant) attDefs.Add(at);
                    //}

                    //foreach (var alGroup in groupedByAlignment.OrderBy(x => x.Key))
                    //{
                    //    prdDbg($"Placing welds for alignment: {alGroup.First().Alignment.Name}...");
                    //    System.Windows.Forms.Application.DoEvents();
                    //    IOrderedEnumerable<WeldPointData> orderedByDist;
                    //    if (alGroup.First().IterationType == TypeOfIteration.Forward)
                    //        orderedByDist = alGroup.OrderBy(x => x.Station);
                    //    else orderedByDist = alGroup.OrderByDescending(x => x.Station);

                    //    Regex regex = new Regex(@"(?<number>^\d\d)");
                    //    string currentPipelineNumber = "";
                    //    if (regex.IsMatch(alGroup.First().Alignment.Name))
                    //    {
                    //        Match match = regex.Match(alGroup.First().Alignment.Name);
                    //        currentPipelineNumber = match.Groups["number"].Value;
                    //    }

                    //    int idx = 1;
                    //    foreach (var wp in orderedByDist)
                    //    {
                    //        Polyline pline = wp.Alignment.GetPolyline().Go<Polyline>(alTx);
                    //        Point3d temp = pline.GetClosestPointTo(wp.WeldPoint, false);
                    //        double station = 0;
                    //        double offset = 0;
                    //        wp.Alignment.StationOffset(temp.X, temp.Y, ref station, ref offset);
                    //        Vector3d deriv = pline.GetFirstDerivative(pline.GetClosestPointTo(wp.WeldPoint, false));
                    //        double rotation = Math.Atan2(deriv.Y, deriv.X);
                    //        //BlockReference wpBr = localDb.CreateBlockWithAttributes(blockName, wp.WeldPoint, rotation);
                    //        var wpBr = new BlockReference(wp.WeldPoint, btrId);
                    //        wpBr.Rotation = rotation;
                    //        wpBr.Layer = blockLayerName;

                    //        modelSpace.AppendEntity(wpBr);
                    //        tx.AddNewlyCreatedDBObject(wpBr, true);

                    //        foreach (AttributeDefinition attDef in attDefs)
                    //        {
                    //            AttributeReference atRef = new AttributeReference();
                    //            atRef.SetAttributeFromBlock(attDef, wpBr.BlockTransform);
                    //            atRef.Position = attDef.Position.TransformBy(wpBr.BlockTransform);
                    //            atRef.TextString = attDef.getTextWithFieldCodes();
                    //            wpBr.AttributeCollection.AppendAttribute(atRef);
                    //            tx.AddNewlyCreatedDBObject(atRef, true);
                    //        }

                    //        if (!noNumbers)
                    //            wpBr.SetAttributeStringValue("NUMMER", currentPipelineNumber + "." + idx.ToString("D3"));

                    //        //if (idx == 1) DisplayDynBlockProperties(editor, wpBr, wpBr.Name);
                    //        SetDynBlockProperty(wpBr, "Type", wp.DN.ToString());
                    //        SetDynBlockProperty(wpBr, "System", wp.System);

                    //        psm.WritePropertyString(wpBr, driPipelineData.BelongsToAlignment, wp.Alignment.Name);

                    //        wpBr.RecordGraphicsModified(true);

                    //        idx++;
                    //   }
                    //}
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("DELETESTIKPOINTS")]
        [CommandMethod("DSP")]
        public void deletestikpoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Delete previous blocks
                //Delete previous blocks
                var existingBlocks = localDb.GetBlockReferenceByName("STIKAFGRENING");
                prdDbg(existingBlocks.Count.ToString());
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                //Delete previous blocks
                existingBlocks = localDb.GetBlockReferenceByName("STIKTEE");
                foreach (BlockReference br in existingBlocks)
                {
                    br.CheckOrOpenForWrite();
                    br.Erase(true);
                }
                #endregion
                tx.Commit();
            }
        }

        [CommandMethod("PLACESTIKPOINTS")]
        public void placestikpoints()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            Regex cuflexRgx = new Regex(@"^CuFlex\s(?<DN>\d\d)-\d+/\d+");
            Regex steelRgx = new Regex(@"^DN(?<DN>\d+)");

            #region Read adresse rør dim katalog
            System.Data.DataTable dtBbr = CsvReader.ReadCsvToDataTable(
                    @"X:\120-1312 - Herstedøster - 01 Intern\04 Projektering\adresserogstik.csv",
                    "BBRData");

            Dictionary<string, string> adresseRørDimDict = new Dictionary<string, string>();

            var query = dtBbr.AsEnumerable().GroupBy(x => x["Vejnavn"].ToString());
            if (query.Any(x => x.Count() > 1))
            {
                prdDbg("Duplicate addresses detected:");
                foreach (var item in query.Where(x => x.Count() > 1))
                {
                    prdDbg(item.Key);
                }
                return;
            }

            foreach (DataRow dr in dtBbr.Rows)
                adresseRørDimDict.Add(dr["Vejnavn"].ToString(), dr["RørDim"].ToString());
            #endregion

            while (true)
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        #region Guard against missing DH pipes
                        HashSet<Polyline> pls = localDb.GetFjvPipes(tx);
                        if (pls.Count == 0)
                        {
                            prdDbg("No DH pipes in drawing!");
                            tx.Abort();
                            return;
                        }
                        #endregion

                        #region Ask for BBR block
                        string message = "Select BBR block: ";
                        var optBlock = new PromptEntityOptions(message);
                        Type allowedType = typeof(BlockReference);
                        optBlock.SetRejectMessage("Allowed type: " + allowedType.Name); // Must call this first
                        optBlock.AddAllowedClass(allowedType, true);

                        Oid bbrOid = Oid.Null;
                        do
                        {
                            var res = ed.GetEntity(optBlock);
                            if (res.Status == PromptStatus.Cancel)
                            {
                                tx.Abort();
                                return;
                            }
                            if (res.Status == PromptStatus.OK) bbrOid = res.ObjectId;
                        }
                        while (bbrOid == Oid.Null);
                        BlockReference bbrBlock = bbrOid.Go<BlockReference>(tx);
                        #endregion

                        #region Get BBR address
                        PropertySetManager bbrPsm = new PropertySetManager(
                            localDb, PSetDefs.DefinedSets.BBR);
                        PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                        string adresse = bbrPsm.ReadPropertyString(bbrBlock, bbrDef.Adresse);
                        #endregion

                        #region Ask for point
                        //message for the ask for point prompt
                        message = "Select location to place elbow: ";
                        var optPoint = new PromptPointOptions(message);

                        Point3d location = Algorithms.NullPoint3d;
                        do
                        {
                            var res = ed.GetPoint(optPoint);
                            if (res.Status == PromptStatus.Cancel)
                            {
                                tx.Abort();
                                return;
                            }
                            if (res.Status == PromptStatus.OK) location = res.Value;
                        }
                        while (location == Algorithms.NullPoint3d);
                        #endregion

                        #region Find nearest pline
                        Polyline pl = pls
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false))
                            ).FirstOrDefault();

                        if (pl == default)
                        {
                            prdDbg("Nearest pipe cannot be found!");
                            tx.Abort();
                            continue;
                        }
                        #endregion

                        #region Test to see if point is on the pline
                        bool isOnTheLine = false;
                        if (pl.GetClosestPointTo(location, false)
                            .DistanceHorizontalTo(location).IsZero(0.001))
                        {
                            location = pl.GetClosestPointTo(location, false);
                            isOnTheLine = true;
                        }
                        #endregion

                        #region Determine rotation if any and create block
                        BlockReference stikBlock;
                        if (isOnTheLine)
                        {
                            Vector3d deriv = pl.GetFirstDerivative(location);
                            double rotation = Math.Atan2(deriv.Y, deriv.X);
                            stikBlock = localDb.CreateBlockWithAttributes("STIKAFGRENING", location, rotation);
                        }
                        else stikBlock = localDb.CreateBlockWithAttributes("STIKAFGRENING", location);
                        #endregion

                        #region Fill out the block properties
                        SetDynBlockPropertyObject(stikBlock, "StikType", adresse);
                        SetDynBlockPropertyObject(stikBlock, "System", "Twin");
                        SetDynBlockPropertyObject(stikBlock, "DN1",
                            Convert.ToDouble(GetPipeDN(pl)));

                        //Determine stik dimension
                        if (adresseRørDimDict.ContainsKey(adresse))
                        {
                            string rørDimRaw = adresseRørDimDict[adresse];

                            double dn = 0.0;

                            if (cuflexRgx.IsMatch(rørDimRaw))
                                dn = Convert.ToDouble(
                                    cuflexRgx.Match(rørDimRaw).Groups["DN"].Value);
                            else if (steelRgx.IsMatch(rørDimRaw))
                                dn = Convert.ToDouble(
                                    steelRgx.Match(rørDimRaw).Groups["DN"].Value);

                            SetDynBlockPropertyObject(stikBlock, "DN2", dn);
                        }
                        else
                        {
                            prdDbg($"Adresse: {adresse} ikke fundet i stikoversigten!");
                            tx.Abort();
                            continue;
                        }
                        #endregion

                        stikBlock.AttSync();
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
        }

        //[CommandMethod("CORRECTPIPESTOCUTLENGTHS")]
        //[CommandMethod("CPTCL")]
        public void callcptcl() => correctpipestocutlengths();
        public void correctpipestocutlengths()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Open alignment db
                DataReferencesOptions dro = new DataReferencesOptions();
                string projectName = dro.ProjectName;
                string etapeName = dro.EtapeName;

                // open the xref database
                Database alDb = new Database(false, true);
                alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction alTx = alDb.TransactionManager.StartTransaction();
                HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                #endregion

                try
                {
                    #region Read components file
                    System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    #endregion

                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    #region Propertyset init
                    PropertySetManager psm = new PropertySetManager(
                        localDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
                    #endregion

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        #region GetCurvesAndBRs

                        HashSet<Curve> curves = localDb.ListOfType<Curve>(tx, true)
                            .Where(x => psm.FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                            .ToHashSet();
                        HashSet<BlockReference> brs = localDb.ListOfType<BlockReference>(tx, true)
                            .Where(x => psm.FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                            .Where(x => x.RealName() != "SVEJSEPUNKT")
                            .ToHashSet();


                        if (curves.Count == 0 && brs.Count == 0) continue;
                        prdDbg($"\nProcessing: {al.Name}...");
                        prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");

                        HashSet<(Entity ent, double dist)> distTuples = new HashSet<(Entity ent, double dist)>();
                        #endregion

                        //Sort curves according to their DN -> bigger DN at start
                        TypeOfIteration iterType = 0;
                        Queue<Curve> kø = Utils.GetSortedQueue(localDb, al, curves, ref iterType);
                        LinkedList<Curve> ll = new LinkedList<Curve>(kø.ToList());

                        #region Analyze curves and correct lengths
                        while (ll.Count > 0)
                        {
                            Curve curve = ll.First.Value;
                            curve.CheckOrOpenForWrite();
                            ll.RemoveFirst();
                            if (brs.Count == 0) { continue; }

                            //Detect the component at curve end
                            //If it is a transition --> analyze and correct
                            //If not --> continue
                            Point3d endPoint = curve.GetPointAtParameter(curve.EndParam);
                            var distsToEndPoint = CreateDistTuples(endPoint, brs).OrderBy(x => x.dist);
                            var first = distsToEndPoint.First();
                            var nearestBlock = first.ent as BlockReference;
                            if (nearestBlock.RealName() != "RED KDLR" &&
                                nearestBlock.RealName() != "RED KDLR x2")
                                continue;
                            //Limit the distance or buerør will give false true
                            if (first.dist > 0.5)
                                continue;

                            double pipeStdLegnth = GetPipeStdLength(curve);
                            double pipeLength = curve.GetDistanceAtParameter(curve.EndParam);
                            double division = pipeLength / pipeStdLegnth;
                            int nrOfSections = (int)division;
                            double modulo = division - nrOfSections;
                            double remainder = modulo * pipeStdLegnth;
                            double missingLength = pipeStdLegnth - remainder;

                            //prdDbg(
                            //    $"pipeStdLength: {pipeStdLegnth}\n" +
                            //    $"pipeLength: {pipeLength}\n" +
                            //    $"division: {division}\n" +
                            //    $"nrOfSections: {nrOfSections}\n" +
                            //    $"modulo: {modulo}\n" +
                            //    $"missingLength: {missingLength}"
                            //    );

                            Polyline pline = curve as Polyline;
                            pline.CheckOrOpenForWrite();
                            pline.ConstantWidth = pline.GetStartWidthAt(0);
                            double globalWidth = pline.ConstantWidth;
                            //prdDbg($"Width: {globalWidth}");

                            if (remainder > 1e-3 && pipeStdLegnth - remainder > 1e-3)
                            {
                                prdDbg($"Remainder: {remainder}, missing length: {missingLength}");
                                double transitionLength = GetTransitionLength(tx, nearestBlock);

                                Curve nextCurve = null;
                                nextCurve = ll.First?.Value;
                                if (nextCurve == null) continue;
                                nextCurve.CheckOrOpenForWrite();
                                ll.RemoveFirst();

                                if (missingLength <= transitionLength)
                                {
                                    prdDbg("Case 1");
                                    //Case where the point is in transition
                                    //Extend the current curve
                                    curve.CheckOrOpenForWrite();
                                    Vector3d v = curve.GetFirstDerivative(endPoint).GetNormal();
                                    Point3d newEndPoint = endPoint + v * missingLength;
                                    curve.Extend(false, newEndPoint);

                                    //Move block
                                    nearestBlock.CheckOrOpenForWrite();
                                    Vector3d moveVector = endPoint.GetVectorTo(newEndPoint);
                                    nearestBlock.TransformBy(Matrix3d.Displacement(moveVector));

                                    //Split the piece from next curve
                                    List<double> splitPars = new List<double>();
                                    splitPars.Add(nextCurve.GetParameterAtDistance(missingLength));
                                    try
                                    {
                                        DBObjectCollection objs = nextCurve
                                            .GetSplitCurves(new DoubleCollection(splitPars.ToArray()));
                                        Curve toAdd = objs[1] as Curve;
                                        toAdd.AddEntityToDbModelSpace(localDb);
                                        //Add the newly created curve to linkedlist
                                        ll.AddFirst(toAdd);

                                        PropertySetManager.CopyAllProperties(nextCurve, toAdd);

                                        nextCurve.CheckOrOpenForWrite();
                                        nextCurve.Erase(true);
                                    }
                                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                    {
                                        Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                        throw new System.Exception("Splitting of pline failed!");
                                    }

                                    //Yellow line
                                    //When the remainder is shorter than the length of transition
                                    Line line = new Line(new Point3d(), newEndPoint);
                                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    line.AddEntityToDbModelSpace(localDb);
                                }
                                else if (missingLength > transitionLength && missingLength < pipeStdLegnth - 2)
                                {
                                    prdDbg("Case 2");
                                    //Case where the point is on the next curve
                                    //Find the location of new endpoint
                                    double newEndDist = missingLength - transitionLength;
                                    //Catch a case where the missing length is longer than the next
                                    //Curves length
                                    if (newEndDist > nextCurve.GetDistanceAtParameter(nextCurve.EndParam))
                                    {
                                        prdDbg("Case 2.1 (Next line shorter than needed->Continue)");

                                        //Red line
                                        Line line2 = new Line(new Point3d(), endPoint);
                                        line2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        line2.AddEntityToDbModelSpace(localDb);

                                        //Return the curve to the queue
                                        ll.AddFirst(nextCurve);
                                        continue;
                                    }

                                    Point3d newEndPoint = nextCurve.GetPointAtDist(newEndDist);
                                    double parameter = Math.Truncate(nextCurve.GetParameterAtPoint(newEndPoint));
                                    SegmentType st = ((Polyline)nextCurve).GetSegmentType((int)parameter);

                                    if (st == SegmentType.Arc)
                                    {
                                        prdDbg("Case 2.2 (Next line is an arc -> abort)");
                                        //Red line
                                        //When segment is an arc -- abort -- must be done manually
                                        //Generally a transition must not be on a curve
                                        Line line2 = new Line(new Point3d(), newEndPoint);
                                        line2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        line2.AddEntityToDbModelSpace(localDb);

                                        //Return the curve to the queue
                                        ll.AddFirst(nextCurve);
                                        continue;
                                    }
                                    else
                                    {
                                        //Move block and rotate
                                        nearestBlock.CheckOrOpenForWrite();
                                        Vector3d moveVector = endPoint.GetVectorTo(newEndPoint);
                                        nearestBlock.TransformBy(Matrix3d.Displacement(moveVector));

                                        //prdDbg($"L: 6102: {nearestBlock.Handle} - {newEndDist}");
                                        //Vector3d deriv = nextCurve.GetFirstDerivative(
                                        //    nextCurve.GetPointAtDist(newEndDist + transitionLength / 2));
                                        //double rotation = Math.Atan2(deriv.Y, deriv.X) - Math.PI / 2;
                                        //nearestBlock.Rotation = rotation;

                                        //Split the piece from next curve
                                        List<double> splitPars = new List<double>();
                                        splitPars.Add(nextCurve.GetParameterAtDistance(newEndDist));
                                        splitPars.Add(nextCurve.GetParameterAtDistance(newEndDist + transitionLength));
                                        try
                                        {
                                            DBObjectCollection objs = nextCurve
                                                .GetSplitCurves(new DoubleCollection(splitPars.ToArray()));
                                            Polyline toMerge = objs[0] as Polyline;

                                            for (int i = 0; i < toMerge.NumberOfVertices; i++)
                                            {
                                                Point2d cp = new Point2d(toMerge.GetPoint3dAt(i).X, toMerge.GetPoint3dAt(i).Y);
                                                pline.AddVertexAt(
                                                    pline.NumberOfVertices,
                                                    cp, toMerge.GetBulgeAt(i), 0, 0);
                                            }
                                            pline.ConstantWidth = globalWidth;
                                            RemoveColinearVerticesPolyline(pline);

                                            Curve toAdd = objs[2] as Curve;
                                            //Add the newly created curve to linkedlist
                                            toAdd.AddEntityToDbModelSpace(localDb);

                                            PropertySetManager.CopyAllProperties(nextCurve, toAdd);

                                            ll.AddFirst(toAdd);

                                            nextCurve.CheckOrOpenForWrite();
                                            nextCurve.Erase(true);
                                        }
                                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                        {
                                            Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                            throw new System.Exception("Splitting of pline failed!");
                                        }

                                        //Cyan line
                                        //When the remainder is longer than the length of transition
                                        Line line = new Line(new Point3d(), newEndPoint);
                                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 4);
                                        line.AddEntityToDbModelSpace(localDb);
                                    }
                                }
                                else if (missingLength >= pipeStdLegnth - 2)
                                {
                                    prdDbg("Case 4 (Missing length is small -> moving backwards).");
                                    //Take care to reverse them back!!!
                                    curve.ReverseCurve();
                                    nextCurve.ReverseCurve();
                                    //Exchange references to be able to reuse code with minimal changes
                                    var temp = curve;
                                    curve = nextCurve;
                                    nextCurve = temp;

                                    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                                    //*******************************************
                                    //Missing length redefined!!!
                                    missingLength = pipeStdLegnth - missingLength;
                                    //End point redefined!!!
                                    endPoint = curve.GetPointAtParameter(curve.EndParam);

                                    //Case where the transition is moved back instead of forward
                                    if (missingLength <= transitionLength)
                                    {
                                        prdDbg($"Case 4.1 (MissingLength {missingLength} is SHORTER than transition.");
                                        //Case where the point is in transition
                                        //Extend the current curve
                                        curve.CheckOrOpenForWrite();
                                        Vector3d v = curve.GetFirstDerivative(endPoint).GetNormal();
                                        Point3d newEndPoint = endPoint + v * missingLength;
                                        curve.Extend(false, newEndPoint);

                                        //Move block
                                        nearestBlock.CheckOrOpenForWrite();
                                        Vector3d moveVector = endPoint.GetVectorTo(newEndPoint);
                                        nearestBlock.TransformBy(Matrix3d.Displacement(moveVector));

                                        //Split the piece from next curve
                                        List<double> splitPars = new List<double>();
                                        splitPars.Add(nextCurve.GetParameterAtDistance(missingLength));
                                        try
                                        {
                                            DBObjectCollection objs = nextCurve
                                                .GetSplitCurves(new DoubleCollection(splitPars.ToArray()));
                                            Curve toAdd = objs[1] as Curve;
                                            toAdd.AddEntityToDbModelSpace(localDb);
                                            //Add the newly created curve to linkedlist
                                            //REMEMBER: it is reversed still!
                                            ll.AddFirst(curve);

                                            PropertySetManager.CopyAllProperties(nextCurve, toAdd);

                                            nextCurve.CheckOrOpenForWrite();
                                            nextCurve.Erase(true);

                                            //Reverse curves back again
                                            toAdd.ReverseCurve();
                                            curve.ReverseCurve();
                                        }
                                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                        {
                                            Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                            throw new System.Exception("Splitting of pline failed!");
                                        }

                                        //Magenta line
                                        //When the remainder is shorter than the length of transition
                                        //And THE MOVEMENT IS REVERSED!
                                        Line line = new Line(new Point3d(), newEndPoint);
                                        line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                        line.AddEntityToDbModelSpace(localDb);
                                    }
                                    else if (missingLength > transitionLength && missingLength <= 2)
                                    {
                                        prdDbg("Case 4.2 (MissingLength is LONGER than transition)");
                                        //Case where the point is on the next curve
                                        //Find the location of new endpoint
                                        double newEndDist = missingLength - transitionLength;
                                        //Catch a case where the missing length is longer than the next
                                        //Curves length
                                        if (newEndDist > nextCurve.GetDistanceAtParameter(nextCurve.EndParam))
                                        {
                                            prdDbg($"L: 6077: {nearestBlock.Handle} - {newEndDist}");
                                            prdDbg("Case 4.2.1 (Curve is shorter than required -> abort)");

                                            //Red line
                                            Line line2 = new Line(new Point3d(), endPoint);
                                            line2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                            line2.AddEntityToDbModelSpace(localDb);

                                            //Return the curve to the queue
                                            //Remember: REVERSED!!
                                            ll.AddFirst(curve);
                                            curve.ReverseCurve();
                                            nextCurve.ReverseCurve();
                                            continue;
                                        }

                                        Point3d newEndPoint = nextCurve.GetPointAtDist(newEndDist);
                                        double parameter = Math.Truncate(nextCurve.GetParameterAtPoint(newEndPoint));
                                        SegmentType st = ((Polyline)nextCurve).GetSegmentType((int)parameter);

                                        if (st == SegmentType.Arc)
                                        {
                                            prdDbg("Case 4.2.2 (NewEndPoint lands on arc segment -> abort)");
                                            //Red line
                                            //When segment is an arc -- abort -- must be done manually
                                            //Generally a transition must not be on a curve
                                            Line line2 = new Line(new Point3d(), newEndPoint);
                                            line2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                            line2.AddEntityToDbModelSpace(localDb);

                                            //Return the curve to the queue
                                            //Remember: REVERSED!!
                                            ll.AddFirst(curve);
                                            curve.ReverseCurve();
                                            nextCurve.ReverseCurve();
                                            continue;
                                        }
                                        else
                                        {
                                            //Move block and rotate
                                            nearestBlock.CheckOrOpenForWrite();
                                            Vector3d moveVector = endPoint.GetVectorTo(newEndPoint);
                                            nearestBlock.TransformBy(Matrix3d.Displacement(moveVector));

                                            //prdDbg($"L: 6102: {nearestBlock.Handle} - {newEndDist}");
                                            //Vector3d deriv = nextCurve.GetFirstDerivative(
                                            //    nextCurve.GetPointAtDist(newEndDist + transitionLength / 2));
                                            //double rotation = Math.Atan2(deriv.Y, deriv.X) - Math.PI / 2;
                                            //nearestBlock.Rotation = rotation;

                                            //Split the piece from next curve
                                            List<double> splitPars = new List<double>();
                                            splitPars.Add(nextCurve.GetParameterAtDistance(newEndDist));
                                            splitPars.Add(nextCurve.GetParameterAtDistance(newEndDist + transitionLength));
                                            try
                                            {
                                                DBObjectCollection objs = nextCurve
                                                    .GetSplitCurves(new DoubleCollection(splitPars.ToArray()));
                                                Polyline toMerge = objs[0] as Polyline;

                                                //Remember exchanged references!!!
                                                pline = curve as Polyline;
                                                for (int i = 0; i < toMerge.NumberOfVertices; i++)
                                                {
                                                    Point2d cp = new Point2d(toMerge.GetPoint3dAt(i).X, toMerge.GetPoint3dAt(i).Y);
                                                    pline.AddVertexAt(
                                                        pline.NumberOfVertices,
                                                        cp, toMerge.GetBulgeAt(i), 0, 0);
                                                }
                                                pline.ConstantWidth = globalWidth;
                                                RemoveColinearVerticesPolyline(pline);

                                                Curve toAdd;
                                                try
                                                {
                                                    toAdd = objs[2] as Curve;
                                                }
                                                catch (System.Exception ex)
                                                {
                                                    prdDbg($"Fails at {nextCurve.Handle}");
                                                    prdDbg(ex);
                                                    throw;
                                                }
                                                //Add the newly created curve to linkedlist
                                                toAdd.AddEntityToDbModelSpace(localDb);

                                                PropertySetManager.CopyAllProperties(nextCurve, toAdd);
                                                toAdd.ReverseCurve();

                                                //Remember exchanged references!!!
                                                ll.AddFirst(curve);

                                                nextCurve.CheckOrOpenForWrite();
                                                nextCurve.Erase(true);

                                                //Remember: REVERSED!!
                                                curve.ReverseCurve();
                                            }
                                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                                            {
                                                Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                                throw new System.Exception("Splitting of pline failed!");
                                            }

                                            //20 line
                                            //When the remainder is longer than the length of transition
                                            Line line = new Line(new Point3d(), newEndPoint);
                                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 20);
                                            line.AddEntityToDbModelSpace(localDb);
                                        }
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                }
                catch (System.Exception ex)
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                    tx.Abort();
                    prdDbg(ex.ExceptionInfo());
                    prdDbg(ex);
                    return;
                }
                alTx.Abort();
                alTx.Dispose();
                alDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("NUMBERALDESCRIPTION")]
        public void numberaldescription()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var als = localDb.HashSetOfType<Alignment>(tx);

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        string name = al.Name;
                        Regex regex = new Regex(@"(?<number>\d{2,3}\s)");

                        string number = "";
                        if (regex.IsMatch(name))
                        {
                            number = regex.Match(name).Groups["number"].Value;
                            number.Remove(number.Length - 1);

                            al.Description = number + ":";
                        }
                        else
                        {
                            prdDbg($"Name for {al.Name} does not contain a number!");
                            continue;
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

        [CommandMethod("FIXPLINEGLOBALWIDTH")]
        public void fixplineglobalwidth()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx);

                    foreach (Polyline pl in pls)
                    {
                        double constWidth;
                        try
                        {
                            constWidth = pl.ConstantWidth;
                        }
                        catch (System.Exception)
                        {
                            prdDbg($"Pline {pl.Handle} needs to fix ConstantWidth!");
                            pl.CheckOrOpenForWrite();
                            pl.ConstantWidth = pl.GetStartWidthAt(0);
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

        [CommandMethod("PLACEELBOW")]
        [CommandMethod("PE")]
        public void placeelbow()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location == Algorithms.NullPoint3d);
                    #endregion

                    #region Find nearest pline
                    Oid nearestPlId;
                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        Polyline pl = plOids.Select(x => x.Go<Polyline>(tx))
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false)))
                            .FirstOrDefault();
                        nearestPlId = pl.Id;
                        tx.Commit();
                    }

                    if (nearestPlId == default)
                    {
                        prdDbg("Nearest pipe cannot be found!");
                        return;
                    }
                    #endregion

                    #region Place preinsulated elbow
                    ElbowPreinsulated elbow = new ElbowPreinsulated(localDb, nearestPlId, location);
                    Result result = elbow.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = elbow.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }

        [CommandMethod("PLACEKEDELRØRSBØJNING")]
        [CommandMethod("PK")]
        public void placekedelrørsbøjning()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location == Algorithms.NullPoint3d);
                    #endregion

                    #region Find nearest pline
                    Oid nearestPlId;
                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        Polyline pl = plOids.Select(x => x.Go<Polyline>(tx))
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false)))
                            .FirstOrDefault();
                        nearestPlId = pl.Id;
                        tx.Commit();
                    }

                    if (nearestPlId == default)
                    {
                        prdDbg("Nearest pipe cannot be found!");
                        return;
                    }
                    #endregion

                    #region Place kedelrørsfitting
                    ElbowWeldFitting elbow = new ElbowWeldFitting(localDb, nearestPlId, location);
                    Result result = elbow.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = elbow.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }

        [CommandMethod("PLACETRANSITIONX1")]
        [CommandMethod("PT1")]
        public void placetransitionx1()
        {
            placetransition(Transition.TransitionType.X1);
        }
        [CommandMethod("PLACETRANSITIONX2")]
        [CommandMethod("PT2")]
        public void placetransitionx2()
        {
            placetransition(Transition.TransitionType.X2);
        }
        private void placetransition(Transition.TransitionType transitionType)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location == Algorithms.NullPoint3d);
                    #endregion

                    #region Find nearest pline
                    Oid nearestPlId;
                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        Polyline pl = plOids.Select(x => x.Go<Polyline>(tx))
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false)))
                            .FirstOrDefault();
                        nearestPlId = pl.Id;
                        tx.Commit();
                    }

                    if (nearestPlId == default)
                    {
                        prdDbg("Nearest pipe cannot be found!");
                        return;
                    }
                    #endregion

                    #region Place transition
                    Transition transition = new Transition(
                        localDb, nearestPlId, location, transitionType);
                    Result result = transition.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = transition.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }
        [CommandMethod("PLACEBUEROR")]
        [CommandMethod("PB")]
        public void placebueror()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location == Algorithms.NullPoint3d);
                    #endregion

                    #region Find nearest pline
                    Oid nearestPlId;
                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        Polyline pl = plOids.Select(x => x.Go<Polyline>(tx))
                            .MinByEnumerable(x => location.DistanceHorizontalTo(
                                x.GetClosestPointTo(location, false)))
                            .FirstOrDefault();
                        nearestPlId = pl.Id;
                        tx.Commit();
                    }

                    if (nearestPlId == default)
                    {
                        prdDbg("Nearest pipe cannot be found!");
                        return;
                    }
                    #endregion

                    #region Place preinsulated elbow
                    Bueror bueror = new Bueror(localDb, nearestPlId, location);
                    Result result = bueror.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = bueror.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    return;
                }
            }
        }
        //[CommandMethod("PLACEBRANCH")]
        //[CommandMethod("PA")]
        public void placebranch()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                try
                {
                    #region Get pipes
                    HashSet<Oid> plOids = localDb.HashSetOfFjvPipeIds(true);
                    if (plOids.Count == 0)
                    {
                        prdDbg("No DH pipes in drawing!");
                        return;
                    }
                    #endregion

                    #region Ask for point
                    //message for the ask for point prompt
                    string message = "Select location to place pipe fitting: ";
                    var opt = new PromptPointOptions(message);

                    Point3d location = Algorithms.NullPoint3d;
                    do
                    {
                        var res = ed.GetPoint(opt);
                        if (res.Status == PromptStatus.Cancel)
                        {
                            bringallblockstofront();
                            return;
                        }
                        if (res.Status == PromptStatus.OK) location = res.Value;
                    }
                    while (location == Algorithms.NullPoint3d);
                    #endregion

                    #region Place branch
                    Branch branch = new Branch(localDb, Oid.Null, location);
                    Result result = branch.Validate();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    result = branch.Place();
                    if (result.Status != ResultStatus.OK)
                    {
                        prdDbg(result.ErrorMsg);
                        continue;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    AbortGracefully(localDb);
                    return;
                }
            }
        }

        [CommandMethod("CORRECTTRANSISIONSIZES")]
        public void correcttransitionsizes()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<BlockReference> allbrs = localDb.HashSetOfType<BlockReference>(tx);
                    HashSet<Curve> curves = localDb.ListOfType<Curve>(tx).ToHashSet();

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

                    var reducers = allbrs.Where(x => x.ReadDynamicCsvProperty(DynamicProperty.Type) == "Reduktion");

                    foreach (BlockReference r in reducers)
                    {
                        string type = r.ReadDynamicPropertyValue("Type");

                        if (type != "Custom") continue;

                        var endPoints = r.GetAllEndPoints();

                        if (endPoints.Count > 2 || endPoints.Count < 2)
                            throw new System.Exception($"Reducer {r.Handle} has unexpected number of endpoints: {endPoints.Count}!");

                        List<int> dns = new List<int>();

                        bool failed = false;
                        foreach (Point3d end in endPoints)
                        {
                            var query = curves.Where(x => end.IsOnCurve(x, 0.05));
                            if (query.Count() != 1)
                            {
                                prdDbg($"Reducer {r.Handle} cannot find connecting or finds multiple curve(s) at point {end}!");
                                failed = true;
                            }

                            if (!failed)
                            {
                                Curve curve = query.First();
                                dns.Add(GetPipeDN(curve));
                            }
                        }

                        type = "";
                        if (dns.Count > 0) type = $"{dns.Max()}x{dns.Min()}";

                        prdDbg($"Failed: {failed}; {r.Handle} -> {type}");

                        if (!failed)
                        { 
                            SetDynBlockProperty(r, "Type", type);
                            r.AttSync();
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
    }
}
