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
using System.Windows.Documents;
using static IntersectUtilities.Graph;
using IntersectUtilities.PipeScheduleV2;
using Autodesk.AutoCAD.Internal;
using System.DirectoryServices.ActiveDirectory;
using IntersectUtilities.UtilsCommon.DataManager;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>ASSIGNBLOCKSANDPLINESTOALIGNMENTS</command>
        /// <summary>
        /// Assigns blocks and polylines to alignments.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("ASSIGNBLOCKSANDPLINESTOALIGNMENTS")]
        public void assignblocksandplinestoalignments()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            const string kwd1 = "Ja";
            const string kwd2 = "Nej";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nOverskriv? ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd2;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            bool overwrite = pKeyRes.StringResult == kwd1;

            DataReferencesOptions dro = new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            // open the xref database
            var dm = new DataManager(dro);
            using Database alDb = dm.Alignments();
            using Transaction alTx = alDb.TransactionManager.StartTransaction();
            HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
            var alPls = als.ToDictionary(x => x, x => x.GetPolyline().Go<Polyline>(alTx));

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fk = CsvData.FK;                    
                    HashSet<Polyline> allPipes = localDb.GetFjvPipes(tx);
                    HashSet<BlockReference> brs = localDb.GetFjvBlocks(tx, fk, true, false);

                    #region Initialize property set
                    PropertySetManager psm = new PropertySetManager(
                        localDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
                    #endregion

                    #region Blocks
                    HashSet<string> unkBrs = new HashSet<string>();
                    foreach (BlockReference br in brs)
                    {
                        //Guard against unknown blocks
                        if (ReadStringParameterFromDataTable(br.RealName(), fk, "Navn", 0) == null)
                        {
                            BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                            if (btr.IsFromExternalReference) continue;
                            unkBrs.Add(br.RealName());
                            continue;
                        }

                        //Skip if record already exists
                        if (!overwrite)
                        {
                            if (psm.ReadPropertyString(br, driPipelineData.BelongsToAlignment).IsNotNoE() ||
                                psm.ReadPropertyString(br, driPipelineData.BranchesOffToAlignment).IsNotNoE())
                                continue;
                        }

                        HashSet<(BlockReference block, double dist, Alignment al)> alDistTuples = new();
                        try
                        {
                            foreach (Alignment al in als)
                            {
                                if (al.Length < 1) continue;
                                Polyline pline = al.GetPolyline().Go<Polyline>(alTx);
                                Point3d tP3d = pline.GetClosestPointTo(br.Position, false);
                                alDistTuples.Add((br, tP3d.DistanceHorizontalTo(br.Position), al));
                                pline.UpgradeOpen();
                                pline.Erase();
                            }
                        }
                        catch (System.Exception)
                        {
                            prdDbg("Error in GetClosestPointTo -> loop incomplete! (Using GetPolyline)");
                        }

                        double distThreshold = 0.15;
                        var result = alDistTuples.Where(x => x.dist < distThreshold);

                        if (result.Count() == 0)
                        {
                            //If the component cannot find an alignment
                            //Repeat with increasing threshold
                            for (int i = 0; i < 3; i++)
                            {
                                distThreshold += 0.15;
                                if (result.Count() != 0) break;
                                if (i == 2)
                                {
                                    //Red line means check result
                                    //This is caught if no result found at ALL
                                    //Line line = new Line(new Point3d(), br.Position);
                                    //line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    //line.AddEntityToDbModelSpace(localDb);
                                }
                            }

                            if (result.Count() > 0)
                            {
                                //This is caught if a result was found after some iterations
                                //So the result must be checked to see, if components
                                //Not belonging to the alignment got selected
                                //Yellow
                                //Line line = new Line(new Point3d(), br.Position);
                                //line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                //line.AddEntityToDbModelSpace(localDb);
                            }
                        }

                        if (result.Count() == 0)
                        {
                            psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, "NA");
                        }
                        else if (result.Count() == 2)
                        {
                            //Could be a branch
                            //Or with the new small components both alignments at intersions
                            //get detected

                            //I really should make a OOP model for BRs
                            PipelineElementType brType = br.GetPipelineType();
                            //Dang! I really should oop this
                            var teeTypes = new HashSet<PipelineElementType>
                            {
                                PipelineElementType.AfgreningMedSpring,
                                PipelineElementType.AfgreningParallel,
                                PipelineElementType.Afgreningsstuds,
                                PipelineElementType.LigeAfgrening,
                                PipelineElementType.AfgreningParallel,
                                PipelineElementType.Svanehals,
                                PipelineElementType.Svejsetee,
                                PipelineElementType.Stikafgrening,
                                PipelineElementType.Muffetee,
                            };

                            if (teeTypes.Contains(brType))
                            {
                                var first = result.First();
                                var second = result.Skip(1).First();

                                double rotation = br.Rotation;
                                Vector3d brDir = new Vector3d(Math.Cos(rotation), Math.Sin(rotation), 0);

                                Polyline falPline = alPls[first.al];
                                Polyline salPline = alPls[second.al];

                                double firstDotProduct = 0;
                                double secondDotProduct = 0;

                                try
                                {
                                    //First
                                    //prdDbg(first.al.Name);
                                    Point3d firstClosestPoint = falPline.GetClosestPointTo(br.Position, false);
                                    Vector3d firstDeriv = falPline.GetFirstDerivative(firstClosestPoint);
                                    firstDotProduct = Math.Abs(brDir.DotProduct(firstDeriv));
                                    //prdDbg($"Rotation: {rotation} - First: {first.al.Name}: {Math.Atan2(firstDeriv.Y, firstDeriv.X)}");
                                    //prdDbg($"Dot product: {brDir.DotProduct(firstDeriv)}");

                                    //Second
                                    //prdDbg(second.al.Name);
                                    Point3d secondClosestPoint = salPline.GetClosestPointTo(br.Position, false);
                                    Vector3d secondDeriv = salPline.GetFirstDerivative(secondClosestPoint);
                                    secondDotProduct = Math.Abs(brDir.DotProduct(secondDeriv));
                                    //prdDbg($"Rotation: {rotation} - Second: {second.al.Name}: {Math.Atan2(secondDeriv.Y, secondDeriv.X)}");
                                    //prdDbg($"Dot product: {brDir.DotProduct(secondDeriv)}");
                                }
                                catch (System.Exception)
                                {
                                    prdDbg("Error in GetClosestPointTo -> loop incomplete! (Using GetFirstDerivative)");
                                    prdDbg($"F: {first.al.Name}, S: {second.al.Name}");
                                    throw;
                                }                                

                                Alignment mainAl = null;
                                Alignment branchAl = null;

                                if (firstDotProduct > 0.75)
                                {
                                    mainAl = first.al;
                                    branchAl = second.al;
                                }
                                else if (secondDotProduct > 0.75)
                                {
                                    mainAl = second.al;
                                    branchAl = first.al;
                                }
                                else
                                {
                                    //Case: Inconclusive
                                    //When the main axis of the block
                                    //Is not aligned with one of the runs
                                    //Annotate with a line for checking
                                    //And must be manually annotated
                                    //Magenta
                                    Line line = new Line(new Point3d(), first.block.Position);
                                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                    line.AddEntityToDbModelSpace(localDb);
                                    continue;
                                }
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, mainAl.Name);
                                psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, branchAl.Name);
                            }
                            else
                            {
                                //Ordinary block which caught two alignments because
                                //the geometry of the blocks is very small
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment,
                                    result.MinBy(x => x.dist).al.Name);
                            }
                        }
                        else if (result.Count() > 2)
                        {//More alignments meeting in one place?
                         //Possible but not seen yet
                         //Cyan
                            var first = result.First();
                            Line line = new Line(new Point3d(), first.block.Position);
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 4);
                            line.AddEntityToDbModelSpace(localDb);
                        }
                        //Add handling of more elements in NAs here!!!
                        else if (result.Count() == 1)
                        {
                            if (br.RealName() == "AFGRSTUDS" ||
                                br.RealName() == "SH LIGE" ||
                                br.RealName() == "SH VINKLET")
                            {
                                //This codeblock finds if AS is perpendicular to the al or not.
                                //This is to catch cases where it branches off to NA OR is in a standalone alignment.
                                var transform = br.BlockTransform;
                                var vx = transform.CoordinateSystem3d.Xaxis;

                                var samplePoly = alPls[result.First().al];

                                Point3d firstClosestPoint = samplePoly.GetClosestPointTo(br.Position, false);
                                Vector3d firstDeriv = samplePoly.GetFirstDerivative(firstClosestPoint);
                                double firstDotProduct = Math.Abs(vx.DotProduct(firstDeriv));

                                double angle = Math.Acos(firstDotProduct) * 180 / Math.PI;

                                if (Math.Abs(angle) <= 15)
                                {
                                    //DebugHelper.CreateDebugLine(br.Position, ColorByName("green"));
                                    psm.WritePropertyString(
                                        br, driPipelineData.BelongsToAlignment, result.First().al.Name);
                                    psm.WritePropertyString(
                                        br, driPipelineData.BranchesOffToAlignment, result.First().al.Name);
                                }
                                else
                                {
                                    //DebugHelper.CreateDebugLine(br.Position, ColorByName("red"));
                                    psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, "NA");
                                    psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, result.First().al.Name);
                                }
                            }
                            else
                            {
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, result.First().al.Name);
                            }
                        }
                    }
                    if (unkBrs.Count > 0)
                    {
                        prdDbg("Ukendte blokke:");
                        foreach (var item in unkBrs) prdDbg(item);
                    }
                    #endregion

                    #region Curves
                    foreach (Curve curve in allPipes)
                    {
                        //Detect zero length curves
                        if (curve is Polyline pline)
                        {
                            if (pline.Length < 0.001)
                                throw new System.Exception(
                                    $"Polyline {curve.Handle} has ZERO length! Delete, please.");
                        }

                        //Skip if record already exists
                        if (!overwrite)
                        {
                            if (psm.ReadPropertyString(curve, driPipelineData.BelongsToAlignment).IsNotNoE() ||
                                psm.ReadPropertyString(curve, driPipelineData.BranchesOffToAlignment).IsNotNoE())
                                continue;
                        }

                        HashSet<(Curve curve, double dist, Alignment al)> alDistTuples =
                            new HashSet<(Curve curve, double dist, Alignment al)>();

                        try
                        {
                            foreach (Alignment al in als)
                            {
                                var alPl = alPls[al];
                                double midParam = curve.EndParam / 2.0;
                                Point3d curveMidPoint = curve.GetPointAtParameter(midParam);
                                Point3d closestPoint = alPl.GetClosestPointTo(curveMidPoint, false);
                                if (closestPoint != default)
                                    alDistTuples.Add((curve, curveMidPoint.DistanceHorizontalTo(closestPoint), al));
                            }
                        }
                        catch (System.Exception)
                        {
                            prdDbg("Error in Curves GetClosestPointTo -> loop incomplete!");                                
                        }

                        double distThreshold = 0.25;
                        var result = alDistTuples.Where(x => x.dist < distThreshold);

                        if (result.Count() == 0)
                        {
                            //If the component cannot find an alignment
                            //Repeat with increasing threshold
                            for (int i = 0; i < 3; i++)
                            {
                                distThreshold += 0.15;
                                if (result.Count() != 0) break;
                                if (i == 2)
                                {
                                    //Red line means check result
                                    //This is caught if no result found at ALL
                                    //Line line = new Line(new Point3d(), br.Position);
                                    //line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    //line.AddEntityToDbModelSpace(localDb);
                                }
                            }
                            //Red line means check result
                            //This is caught if no result found at ALL
                            //Line line = new Line(new Point3d(), curve.GetPointAtParameter(curve.EndParam / 2));
                            //line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                            //line.AddEntityToDbModelSpace(localDb);
                        }

                        if (result.Count() == 0)
                        {
                            psm.WritePropertyString(curve, driPipelineData.BelongsToAlignment, "NA");
                            DebugHelper.CreateDebugLine(curve.GetPointAtParameter(curve.EndParam / 2),
                                ColorByName("yellow"));
                        }
                        else if (result.Count() == 1)
                        {
                            psm.WritePropertyString(curve, driPipelineData.BelongsToAlignment, result.First().al.Name);
                        }
                        else if (result.Count() > 1)
                        {
                            //If multiple result
                            //Means midpoint is close to two alignments
                            //Sample more points to determine

                            double oneFourthParam = curve.EndParam / 4;
                            Point3d oneFourthPoint = curve.GetPointAtParameter(oneFourthParam);
                            double threeFourthParam = curve.EndParam / 4 * 3;
                            Point3d threeFourthPoint = curve.GetPointAtParameter(threeFourthParam);

                            var resArray = result.ToArray();

                            distThreshold = 0.1;
                            double distIncrement = 0.1;

                            bool alDetected = false;
                            Alignment detectedAl = null;
                            while (!alDetected)
                            {
                                for (int i = 0; i < resArray.Count(); i++)
                                {
                                    if (alDetected) break;
                                    detectedAl = resArray[i].al;

                                    Point3d oneFourthClosestPoint = detectedAl.GetClosestPointTo(oneFourthPoint, false);
                                    Point3d threeFourthClosestPoint = detectedAl.GetClosestPointTo(threeFourthPoint, false);

                                    if (oneFourthPoint.DistanceHorizontalTo(oneFourthClosestPoint) < distThreshold &&
                                        threeFourthPoint.DistanceHorizontalTo(threeFourthClosestPoint) < distThreshold)
                                        alDetected = true;
                                }

                                distThreshold += distIncrement;
                            }

                            psm.WritePropertyString(curve, driPipelineData.BelongsToAlignment, detectedAl?.Name ?? "NA");

                            //Yellow line means check result
                            //This is caught if multiple results
                            Line line = new Line(new Point3d(), curve.GetPointAtParameter(curve.EndParam / 2));
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                            line.AddEntityToDbModelSpace(localDb);
                        }
                    }
                    #endregion

                    #region Group NAs by spatial relation
                    graphclear();
                    graphpopulate();

                    //Get all NAs
                    var allNas =
                        allPipes.Where(x => psm.ReadPropertyString(
                            x, driPipelineData.BelongsToAlignment).StartsWith("NA"))
                        .Cast<Entity>().ToHashSet();
                    allNas.UnionWith(
                        brs.Where(x => psm.ReadPropertyString(
                            x, driPipelineData.BelongsToAlignment).StartsWith("NA"))
                        .Cast<Entity>().ToHashSet());

                    PropertySetManager psmGraph = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriGraph);
                    PSetDefs.DriGraph defGraph = new PSetDefs.DriGraph();

                    #region Group NAs into spatial groups
                    Dictionary<Handle, Entity> entities = allNas
                            .ToDictionary(x => x.Handle, x => x);
                    var visited = new HashSet<Handle>();
                    var naGroups = new HashSet<HashSet<Entity>>();

                    void DFS1(Entity entity, HashSet<Entity> group)
                    {
                        var stack = new Stack<Entity>();
                        stack.Push(entity);

                        while (stack.Count > 0)
                        {
                            var current = stack.Pop();
                            if (visited.Contains(current.Handle)) continue;

                            visited.Add(current.Handle);
                            group.Add(current);

                            string conString = psmGraph.ReadPropertyString(current, defGraph.ConnectedEntities);
                            if (conString.IsNoE())
                                throw new System.Exception(
                                    $"Malformend constring: {conString}, entity: {entity.Handle}.");
                            Con[] cons = GraphEntity.parseConString(conString);

                            foreach (var con in cons)
                                if (entities.ContainsKey(con.ConHandle))
                                    stack.Push(entities[con.ConHandle]);
                        }
                    }

                    foreach (var entity in allNas)
                    {
                        if (visited.Contains(entity.Handle)) continue;

                        HashSet<Entity> group = new();
                        DFS1(entity, group);
                        naGroups.Add(group);
                    }
                    #endregion

                    #region For each spatial group find branches and assign number
                    int naIdx = 0;
                    foreach (var group in naGroups)
                    {
                        entities = group
                            .ToDictionary(x => x.Handle, x => x);

                        HashSet<Handle> visitedEntities = new();
                        List<List<Handle>> allRoutes = new();

                        Handle rootHandle = entities.Values
                            .Select(entity => new { Entity = entity, Neighbours = GetAllNeighbours(entity) })
                            .Where(x => x.Neighbours.Any(n => !entities.ContainsKey(n)))
                            .Select(x => x.Entity.Handle)
                            .FirstOrDefault();

                        if (rootHandle == default)
                            throw new System.Exception($"No root found for " +
                                $"{entities.First().Value.Handle}!");

                        List<Handle> GetValidNeighbours(Entity entity)
                        {
                            string conString = psmGraph.ReadPropertyString(entity, defGraph.ConnectedEntities);
                            if (conString.IsNoE())
                                throw new System.Exception(
                                    $"Malformend constring: {conString}, entity: {entity.Handle}.");

                            Con[] cons = GraphEntity.parseConString(conString);
                            return cons.Select(con => con.ConHandle).Where(entities.ContainsKey).ToList();
                        }
                        List<Handle> GetAllNeighbours(Entity entity)
                        {
                            string conString = psmGraph.ReadPropertyString(entity, defGraph.ConnectedEntities);
                            if (conString.IsNoE())
                                throw new System.Exception(
                                    $"Malformend constring: {conString}, entity: {entity.Handle}.");

                            Con[] cons = GraphEntity.parseConString(conString);
                            return cons.Select(con => con.ConHandle).ToList();
                        }

                        List<Handle> path = new();
                        DFS2(rootHandle, visitedEntities, path, allRoutes);
                        void DFS2(Handle currentHandle, HashSet<Handle> visited, List<Handle> path, List<List<Handle>> allRoutes)
                        {
                            // Add the current node to the path and mark it as visited
                            visited.Add(currentHandle);
                            path.Add(currentHandle);

                            // Get the neighbours of the current entity
                            Entity currentEntity = entities[currentHandle];
                            List<Handle> neighbours = GetValidNeighbours(currentEntity);

                            // Determine if this is a leaf, member, or branch node
                            if (neighbours.Count == 0)
                            {
                                // if no neighbours, it's a standalone node
                                allRoutes.Add(new List<Handle>(path));
                            }
                            if (neighbours.Count == 1 && path.Count > 1)
                            {
                                // Leaf node (but not the starting node), store the route
                                allRoutes.Add(new List<Handle>(path));
                            }
                            else if (neighbours.Count > 2)
                            {
                                // Branch node, store the route up to this point
                                allRoutes.Add(new List<Handle>(path));

                                // Continue traversing each branch
                                foreach (var neighbourHandle in neighbours)
                                {
                                    if (!visited.Contains(neighbourHandle))
                                    {
                                        DFS2(neighbourHandle, new HashSet<Handle>(visited), new List<Handle>(path), allRoutes);
                                    }
                                }

                                // Stop further traversal after storing branch routes
                                return;
                            }
                            else
                            {
                                // Member node, continue traversal
                                foreach (var neighbourHandle in neighbours)
                                {
                                    if (!visited.Contains(neighbourHandle))
                                    {
                                        DFS2(neighbourHandle, visited, path, allRoutes);
                                    }
                                }
                            }

                            // Backtrack by removing the current node from the path
                            path.RemoveAt(path.Count - 1);
                        }

                        foreach (var route in allRoutes)
                        {
                            naIdx++;
                            foreach (Handle item in route)
                            {
                                psm.WritePropertyString(
                                    entities[item], driPipelineData.BelongsToAlignment,
                                    $"NA {naIdx.ToString("00")}");
                            }
                        }
                    }
                    #endregion
                    #endregion
                }
                catch (System.Exception ex)
                {
                    foreach (var alPl in alPls.Values)
                    {
                        alPl.CheckOrOpenForWrite();
                        alPl.Erase(true);
                    }
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

        //[CommandMethod("TESTDOTPRODUCT")]
        public void testdotproduct()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    Oid oid1 = Interaction.GetEntity("Select first: ", typeof(Line));
                    Oid oid2 = Interaction.GetEntity("Select second: ", typeof(Line));
                    if (oid1 == Oid.Null || oid2 == Oid.Null) { AbortGracefully("Aborted!", localDb); return; }

                    Line line1 = oid1.Go<Line>(tx);
                    Line line2 = oid2.Go<Line>(tx);

                    var v1 = line1.GetFirstDerivative(line1.EndPoint);
                    var v2 = line2.GetFirstDerivative(line2.EndPoint);

                    prdDbg(v1.DotProduct(v2));
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

        /// <command>LABELPIPE, LB</command>
        /// <summary>
        /// Labels pipes with dimension annotation.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("LABELPIPE")]
        [CommandMethod("LB")]
        public void labelpipe()
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
                    PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                        "\nSelect pipe (polyline) to label: ");
                    promptEntityOptions1.SetRejectMessage("\nNot a polyline!");
                    promptEntityOptions1.AddAllowedClass(typeof(Polyline), false);
                    PromptEntityResult entity1 = ed.GetEntity(promptEntityOptions1);
                    if (((PromptResult)entity1).Status != PromptStatus.OK) { tx.Abort(); return; }
                    Oid plineId = entity1.ObjectId;
                    Entity ent = plineId.Go<Entity>(tx);
                    string labelText = GetLabel(ent);
                    PromptPointOptions pPtOpts = new PromptPointOptions("\nChoose location of label: ");
                    PromptPointResult pPtRes = ed.GetPoint(pPtOpts);
                    Point3d selectedPoint = pPtRes.Value;
                    if (pPtRes.Status != PromptStatus.OK) { tx.Abort(); return; }
                    //Create new text
                    string layerName = "FJV-DIM";
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (!lt.Has(layerName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = layerName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                        lt.CheckOrOpenForWrite();
                        lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }
                    DBText label = new DBText();
                    label.Layer = layerName;
                    label.TextString = labelText;
                    label.Height = 1.2;
                    label.HorizontalMode = TextHorizontalMode.TextMid;
                    label.VerticalMode = TextVerticalMode.TextVerticalMid;
                    label.Position = new Point3d(selectedPoint.X, selectedPoint.Y, 0);
                    label.AlignmentPoint = selectedPoint;
                    //Find rotation
                    Polyline pline = (Polyline)ent;
                    Point3d closestPoint = pline.GetClosestPointTo(selectedPoint, true);
                    Vector3d derivative = pline.GetFirstDerivative(closestPoint);
                    double rotation = Math.Atan2(derivative.Y, derivative.X);
                    label.Rotation = rotation;
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace =
                        tx.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    Oid labelId = modelSpace.AppendEntity(label);
                    tx.AddNewlyCreatedDBObject(label, true);
                    label.Draw();
                    System.Windows.Forms.Application.DoEvents();
                    //Enable flipping of label
                    const string kwd1 = "Yes";
                    const string kwd2 = "No";
                    PromptKeywordOptions pkos = new PromptKeywordOptions("\nFlip label? ");
                    pkos.Keywords.Add(kwd1);
                    pkos.Keywords.Add(kwd2);
                    pkos.AllowNone = true;
                    pkos.Keywords.Default = kwd2;
                    PromptResult pkwdres = ed.GetKeywords(pkos);
                    string result = pkwdres.StringResult;
                    if (result == kwd1) label.Rotation += Math.PI;
                    #region Attach id data

                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriSourceReference);
                    PSetDefs.DriSourceReference driSourceReference = new PSetDefs.DriSourceReference();
                    psm.GetOrAttachPropertySet(label);
                    string handle = ent.Handle.ToString();
                    psm.WritePropertyString(driSourceReference.SourceEntityHandle, handle);
                    #endregion

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

        /// <command>LABELSUPDATE</command>
        /// <summary>
        /// Updates the labels of pipes.
        /// Only works if the pipe hasn't been recreated (have gotten new handle).
        /// The command takes all labels, looks up the handle of the source entity.
        /// If it finds the source entity, it updates the label with the new label.
        /// If the entity is not found, then manual intervention is needed.
        /// Those labels are selected for easier identification.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("LABELSUPDATE")]
        public void updatepipelabels()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager psm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriSourceReference);
                    PSetDefs.DriSourceReference dsr = new PSetDefs.DriSourceReference();

                    HashSet<Oid> ids = new HashSet<Oid>();
                    HashSet<MText> labels = localDb.HashSetOfType<MText>(tx);

                    foreach (MText label in labels)
                    {
                        string handle = psm.ReadPropertyString(label, dsr.SourceEntityHandle);
                        if (handle.IsNoE()) continue;
                        try
                        {
                            var ent = localDb.Go<Entity>(handle);
                            if (ent is Polyline pline)
                            {
                                string newLabel = GetLabel(ent);
                                if (newLabel.IsNoE()) continue;

                                label.CheckOrOpenForWrite();
                                label.Contents = newLabel;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            prdDbg($"Handle {handle} does not exist!");
                            ids.Add(label.Id);
                            continue;
                        }
                    }

                    if (ids.Count > 0)
                    {
                        prdDbg($"Fandt gamle labels!");
                        docCol.MdiActiveDocument.Editor.SetImpliedSelection(ids.ToArray());
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

        /// <command>DECORATEPOLYLINES</command>
        /// <summary>
        /// Is used when creaing alignments.
        /// In a new drawing, fjernvarme fremtidig must be xrefed in.
        /// Then the command places special blocks at vertices and arc middles
        /// for easier alignment creation.
        /// </summary>
        /// <category>Alignments</category>
        [CommandMethod("DECORATEPOLYLINEs")]
        public void decoratepolylines()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //////////////////////////////////////
            string verticeArcName = "VerticeArc";
            string verticeLineName = "VerticeLine";
            //////////////////////////////////////

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                    #region Import vertice blocks
                    if (!bt.Has(verticeArcName) || !bt.Has(verticeLineName))
                    {
                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\Projection_styles.dwg",
                            //blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Dev.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        ObjectIdCollection idsToClone = new ObjectIdCollection();

                        if (!bt.Has(verticeArcName))
                            if (sourceBt.Has(verticeArcName))
                            {
                                prdDbg("Block for stik branch is missing! Importing...");
                                idsToClone.Add(sourceBt[verticeArcName]);
                            }

                        if (!bt.Has(verticeLineName))
                            if (sourceBt.Has(verticeLineName))
                            {
                                prdDbg("Block for stik tee is missing! Importing...");
                                idsToClone.Add(sourceBt[verticeLineName]);
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

            DataReferencesOptions dro = new DataReferencesOptions();
            var dm = new DataManager(dro);

            // open the LER dwg database
            using var xRefFjvDB = dm.Fremtid();
            using var xRefFjvTx = xRefFjvDB.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {                
                try
                {
                    #region Load linework

                    HashSet<Line> lines = xRefFjvDB.HashSetOfType<Line>(xRefFjvTx, true);
                    //HashSet<Spline> splines = xRefFjvDB.HashSetOfType<Spline>(xRefLerTx);
                    HashSet<Polyline> plines = xRefFjvDB.HashSetOfType<Polyline>(xRefFjvTx, true);
                    //HashSet<Polyline3d> plines3d = xRefFjvDB.HashSetOfType<Polyline3d>(xRefLerTx);
                    HashSet<Arc> arcs = xRefFjvDB.HashSetOfType<Arc>(xRefFjvTx, true);
                    editor.WriteMessage($"\nNr. of lines: {lines.Count}");
                    //editor.WriteMessage($"\nNr. of splines: {splines.Count}");
                    editor.WriteMessage($"\nNr. of plines: {plines.Count}");
                    //editor.WriteMessage($"\nNr. of plines3d: {plines3d.Count}");
                    editor.WriteMessage($"\nNr. of arcs: {arcs.Count}");

                    HashSet<Entity> allLinework = new HashSet<Entity>();
                    allLinework.UnionWith(lines.Cast<Entity>().ToHashSet());
                    //allLinework.UnionWith(splines.Cast<Entity>().ToHashSet());
                    allLinework.UnionWith(plines.Cast<Entity>().ToHashSet());
                    //allLinework.UnionWith(plines3d.Cast<Entity>().ToHashSet());
                    allLinework.UnionWith(arcs.Cast<Entity>().ToHashSet());
                    #endregion

                    #region Layer handling
                    string localLayerName = "0-PLDECORATOR";
                    bool localLayerExists = false;

                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (lt.Has(localLayerName))
                    {
                        localLayerExists = true;
                    }
                    else
                    {
                        //Create layer if it doesn't exist
                        try
                        {
                            //Validate the name of layer
                            //It throws an exception if not, so need to catch it
                            SymbolUtilityServices.ValidateSymbolName(localLayerName, false);

                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = localLayerName;

                            //Make layertable writable
                            lt.UpgradeOpen();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            tx.AddNewlyCreatedDBObject(ltr, true);

                            //Flag that the layer exists now
                            localLayerExists = true;

                        }
                        catch (System.Exception)
                        {
                            //Eat the exception and continue
                            //localLayerExists must remain false
                        }
                    }
                    #endregion

                    #region Decorate polyline vertices
                    BlockTableRecord space = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForWrite) as BlockTable;

                    #region Delete previous blocks

                    List<string> blockNameList = new List<string>() { "VerticeLine", "VerticeArc" };

                    foreach (string name in blockNameList)
                    {
                        var existingBlocks = localDb.GetBlockReferenceByName(name)
                            .Where(x => x.Layer == localLayerName)
                            .ToList();
                        editor.WriteMessage($"\n{existingBlocks.Count} existing blocks found of name {name}.");
                        foreach (Autodesk.AutoCAD.DatabaseServices.BlockReference br in existingBlocks)
                        {
                            br.CheckOrOpenForWrite();
                            br.Erase(true);
                        }
                    }

                    #endregion

                    string blockName = "";

                    foreach (Entity ent in allLinework)
                    {
                        switch (ent)
                        {
                            case Polyline pline:
                                int numOfVerts = pline.NumberOfVertices - 1;
                                for (int i = 0; i < numOfVerts; i++)
                                {
                                    switch (pline.GetSegmentType(i))
                                    {
                                        case SegmentType.Line:

                                            blockName = "VerticeLine";
                                            if (bt.Has(blockName))
                                            {
                                                LineSegment2d lineSegment2dAt = pline.GetLineSegment2dAt(i);

                                                Point2d point2d1 = lineSegment2dAt.StartPoint;
                                                using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                    new Point3d(point2d1.X, point2d1.Y, 0), bt[blockName]))
                                                {
                                                    space.AppendEntity(br);
                                                    tx.AddNewlyCreatedDBObject(br, true);
                                                    br.Layer = localLayerName;
                                                }

                                                Point2d point2d2 = lineSegment2dAt.EndPoint;
                                                using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                    new Point3d(point2d2.X, point2d2.Y, 0), bt[blockName]))
                                                {
                                                    space.AppendEntity(br);
                                                    tx.AddNewlyCreatedDBObject(br, true);
                                                    br.Layer = localLayerName;
                                                }
                                            }

                                            break;
                                        case SegmentType.Arc:

                                            blockName = "VerticeArc";
                                            if (bt.Has(blockName))
                                            {
                                                CircularArc2d arcSegment2dAt = pline.GetArcSegment2dAt(i);

                                                Point2d point2d1 = arcSegment2dAt.StartPoint;
                                                using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                    new Point3d(point2d1.X, point2d1.Y, 0), bt[blockName]))
                                                {
                                                    space.AppendEntity(br);
                                                    tx.AddNewlyCreatedDBObject(br, true);
                                                    br.Layer = localLayerName;
                                                }

                                                Point2d point2d2 = arcSegment2dAt.EndPoint;
                                                using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                    new Point3d(point2d2.X, point2d2.Y, 0), bt[blockName]))
                                                {
                                                    space.AppendEntity(br);
                                                    tx.AddNewlyCreatedDBObject(br, true);
                                                    br.Layer = localLayerName;
                                                }
                                                Point2d samplePoint = ((Curve2d)arcSegment2dAt).GetSamplePoints(11)[5];
                                                using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                                    new Point3d(samplePoint.X, samplePoint.Y, 0), bt[blockName]))
                                                {
                                                    space.AppendEntity(br);
                                                    tx.AddNewlyCreatedDBObject(br, true);
                                                    br.Layer = localLayerName;
                                                }
                                            }
                                            break;
                                        case SegmentType.Coincident:
                                            break;
                                        case SegmentType.Point:
                                            break;
                                        case SegmentType.Empty:
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                break;
                            case Line line:
                                blockName = "VerticeLine";
                                if (bt.Has(blockName))
                                {
                                    Point3d point3d1 = line.StartPoint;
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(point3d1.X, point3d1.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }

                                    Point3d point3d2 = line.EndPoint;
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(point3d2.X, point3d2.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }
                                }
                                break;
                            case Arc arc:
                                blockName = "VerticeArc";
                                if (bt.Has(blockName))
                                {
                                    Point3d point3d1 = arc.StartPoint;
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(point3d1.X, point3d1.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }

                                    Point3d point3d2 = arc.EndPoint;
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(point3d2.X, point3d2.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }
                                    Point3d samplePoint = arc.GetPointAtDist(arc.Length / 2);
                                    using (var br = new Autodesk.AutoCAD.DatabaseServices.BlockReference(
                                        new Point3d(samplePoint.X, samplePoint.Y, 0), bt[blockName]))
                                    {
                                        space.AppendEntity(br);
                                        tx.AddNewlyCreatedDBObject(br, true);
                                        br.Layer = localLayerName;
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    xRefFjvTx?.Abort();
                    xRefFjvDB?.Dispose();
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                xRefFjvTx?.Abort();
                xRefFjvDB?.Dispose();
                tx.Commit();
            }
        }
    }
}