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

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeSchedule;

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

namespace IntersectUtilities
{
    public partial class Intersect
    {
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

            prdDbg("Kører med antagelse, at alle flex rør (CU, ALUPEX) er stikledninger.");

            // open the xref database
            Database alDb = new Database(false, true);
            alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                    HashSet<Polyline> allPipes = localDb.GetFjvPipes(tx);
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);

                    #region Initialize property set
                    PropertySetManager psm = new PropertySetManager(
                        localDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
                    #endregion

                    #region Blocks
                    foreach (BlockReference br in brs)
                    {
                        //Guard against unknown blocks
                        if (ReadStringParameterFromDataTable(
                            br.RealName(), fjvKomponenter, "Navn", 0) == null)
                            continue;

                        //Skip if record already exists
                        if (!overwrite)
                        {
                            if (psm.ReadPropertyString(br, driPipelineData.BelongsToAlignment).IsNotNoE() ||
                                psm.ReadPropertyString(br, driPipelineData.BranchesOffToAlignment).IsNotNoE())
                                continue;
                        }

                        HashSet<(BlockReference block, double dist, Alignment al)> alDistTuples =
                            new HashSet<(BlockReference, double, Alignment)>();
                        try
                        {
                            foreach (Alignment al in als)
                            {
                                if (al.Length < 1) continue;
                                Polyline pline = al.GetPolyline().Go<Polyline>(alTx);
                                Point3d tP3d = pline.GetClosestPointTo(br.Position, false);
                                alDistTuples.Add((br, tP3d.DistanceHorizontalTo(br.Position), al));
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
                            for (int i = 0; i < 4; i++)
                            {
                                distThreshold += 0.1;
                                if (result.Count() != 0) break;
                                if (i == 3)
                                {
                                    //Red line means check result
                                    //This is caught if no result found at ALL
                                    Line line = new Line(new Point3d(), br.Position);
                                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    line.AddEntityToDbModelSpace(localDb);
                                }
                            }

                            if (result.Count() > 0)
                            {
                                //This is caught if a result was found after some iterations
                                //So the result must be checked to see, if components
                                //Not belonging to the alignment got selected
                                //Magenta
                                Line line = new Line(new Point3d(), br.Position);
                                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                line.AddEntityToDbModelSpace(localDb);
                            }
                        }

                        if (result.Count() == 0)
                        {
                            psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, "NA");
                        }
                        else if (result.Count() == 2)
                        {//Should be ordinary branch
                            var first = result.First();
                            var second = result.Skip(1).First();

                            double rotation = br.Rotation;
                            Vector3d brDir = new Vector3d(Math.Cos(rotation), Math.Sin(rotation), 0);

                            //First
                            prdDbg(first.al.Name);
                            Point3d firstClosestPoint = first.al.GetClosestPointTo(br.Position, false);
                            Vector3d firstDeriv = first.al.GetFirstDerivative(firstClosestPoint);
                            double firstDotProduct = Math.Abs(brDir.DotProduct(firstDeriv));
                            //prdDbg($"Rotation: {rotation} - First: {first.al.Name}: {Math.Atan2(firstDeriv.Y, firstDeriv.X)}");
                            //prdDbg($"Dot product: {brDir.DotProduct(firstDeriv)}");

                            //Second
                            prdDbg(second.al.Name);
                            Point3d secondClosestPoint = second.al.GetClosestPointTo(br.Position, false);
                            Vector3d secondDeriv = second.al.GetFirstDerivative(secondClosestPoint);
                            double secondDotProduct = Math.Abs(brDir.DotProduct(secondDeriv));
                            //prdDbg($"Rotation: {rotation} - Second: {second.al.Name}: {Math.Atan2(secondDeriv.Y, secondDeriv.X)}");
                            //prdDbg($"Dot product: {brDir.DotProduct(secondDeriv)}");

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
                                //Yellow
                                Line line = new Line(new Point3d(), first.block.Position);
                                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                line.AddEntityToDbModelSpace(localDb);
                                continue;
                            }

                            if (
                                //br.RealName() == "AFGRSTUDS" ||
                                br.RealName() == "SH LIGE")
                            {
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, branchAl.Name);
                                psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, mainAl.Name);
                            }
                            else
                            {
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, mainAl.Name);
                                psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, branchAl.Name);
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
                        else if (result.Count() == 1)
                        {
                            if (br.RealName() == "AFGRSTUDS" ||
                                br.RealName() == "SH LIGE")
                            {
                                psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, result.First().al.Name);
                            }
                            else
                            {
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, result.First().al.Name);
                            }
                        }
                    }
                    #endregion

                    #region Connection pipes
                    HashSet<Polyline> mainPipes = allPipes
                        .Where(x => GetPipeSystem(x) == PipeSystemEnum.Stål)
                        .ToHashSet();
                    HashSet<Polyline> conPipes = allPipes
                        .Where(x =>
                            GetPipeSystem(x) == PipeSystemEnum.AluPex ||
                            GetPipeSystem(x) == PipeSystemEnum.Kobberflex
                            )
                        .ToHashSet();
                    prdDbg($"Conpipes: {conPipes.Count}");
                    #endregion

                    #region Curves
                    foreach (Curve curve in mainPipes)
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
                                if (al.Length < 1) continue;
                                double midParam = curve.EndParam / 2.0;
                                Point3d curveMidPoint = curve.GetPointAtParameter(midParam);
                                Point3d closestPoint = al.GetClosestPointTo(curveMidPoint, false);
                                if (closestPoint != null)
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
                            psm.WritePropertyString(curve, driPipelineData.BelongsToAlignment, "NA");
                            //Yellow line means check result
                            //This is caught if no result found at ALL
                            Line line = new Line(new Point3d(), curve.GetPointAtParameter(curve.EndParam / 2));
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                            line.AddEntityToDbModelSpace(localDb);
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

                                    //DBPoint p1 = new DBPoint(oneFourthClosestPoint);
                                    //if (i == 0) p1.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    //else p1.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    //p1.AddEntityToDbModelSpace(localDb);
                                    //DBPoint p2 = new DBPoint(threeFourthClosestPoint);
                                    //if (i == 0) p2.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    //else p2.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                    //p2.AddEntityToDbModelSpace(localDb);

                                    if (oneFourthPoint.DistanceHorizontalTo(oneFourthClosestPoint) < distThreshold &&
                                        threeFourthPoint.DistanceHorizontalTo(threeFourthClosestPoint) < distThreshold)
                                        alDetected = true;
                                }

                                distThreshold += distIncrement;
                            }

                            psm.WritePropertyString(curve, driPipelineData.BelongsToAlignment, detectedAl?.Name ?? "NA");

                            //Red line means check result
                            //This is caught if multiple results
                            Line line = new Line(new Point3d(), curve.GetPointAtParameter(curve.EndParam / 2));
                            line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                            line.AddEntityToDbModelSpace(localDb);
                        }
                    }
                    #endregion

                    #region Group NAs by spatial relation
                    graphclear();
                    graphpopulate();

                    //Get all NAs
                    List<Entity> allNas =
                        allPipes.Where(x => psm.ReadPropertyString(
                            x, driPipelineData.BelongsToAlignment).StartsWith("NA"))
                        .Cast<Entity>().ToList();
                    allNas.AddRange(
                        brs.Where(x => psm.ReadPropertyString(
                            x, driPipelineData.BelongsToAlignment).StartsWith("NA"))
                        .Cast<Entity>().ToList());

                    PropertySetManager psmGraph = new PropertySetManager(
                        localDb, PSetDefs.DefinedSets.DriGraph);
                    PSetDefs.DriGraph defGraph = new PSetDefs.DriGraph();

                    //Group NAs by spatial relation
                    var naGroups = allNas.GroupByCluster((x, y) => isASpatialGroup(x, y), 0.5);
                    double isASpatialGroup(Entity x, Entity y)
                    {
                        string conString1 = psmGraph.ReadPropertyString(
                                    x, defGraph.ConnectedEntities);
                        string conString2 = psmGraph.ReadPropertyString(
                                    y, defGraph.ConnectedEntities);

                        if (conString1.IsNoE())
                            throw new System.Exception(
                                $"Malformend constring: {conString1}, entity: {x.Handle}.");
                        if (conString2.IsNoE())
                            throw new System.Exception(
                                $"Malformend constring: {conString2}, entity: {y.Handle}.");

                        Con[] cons1 = GraphEntity.parseConString(conString1);
                        Con[] cons2 = GraphEntity.parseConString(conString2);

                        if (cons1.Any(l => l.ConHandle == y.Handle) ||
                            cons2.Any(m => m.ConHandle == x.Handle)) return 0.0;
                        else return 1.0;
                    }

                    int idx = 0;
                    foreach (IGrouping<Entity, Entity> group in naGroups)
                    {
                        idx++;
                        foreach (Entity item in group)
                            psm.WritePropertyString(
                                item, driPipelineData.BelongsToAlignment,
                                $"NA {idx.ToString("00")}");
                    }
                    #endregion

                    #region Take care of stik
                    //Exit gracefully if no stiks
                    if (conPipes.Count == 0)
                    {
                        alTx.Abort();
                        alTx.Dispose();
                        alDb.Dispose();
                        tx.Commit();
                        return;
                    }

                    prdDbg(
                        "**************************************************************" +
                        "Projektet indeholder stikledninger!\n" +
                        "ADVARSEL: Stikledninger SKAL være forbundet i et TRÆ-struktur!\n" +
                        "Dvs. der må ikke være gennemgående polylinjer ved afgreninger.\n" +
                        "Stikledninger forbundet til komponenter skal afklares manuelt.\n" +
                        "**************************************************************");

                    var grouping = conPipes.GroupByCluster((x, y) => areConnected(x, y), 0.5);

                    int stikGruppeCount = 0;
                    foreach (var group in grouping)
                    {
                        stikGruppeCount++;
                        #region Debug connection of polylines
                        ////Determine maximum and minimum points
                        //HashSet<double> Xs = new HashSet<double>();
                        //HashSet<double> Ys = new HashSet<double>();

                        //foreach (var pl in group)
                        //{
                        //    Extents3d bbox = pl.GeometricExtents;
                        //    Xs.Add(bbox.MaxPoint.X);
                        //    Xs.Add(bbox.MinPoint.X);
                        //    Ys.Add(bbox.MaxPoint.Y);
                        //    Ys.Add(bbox.MinPoint.Y);
                        //}

                        //double maxX = Xs.Max();
                        //double maxY = Ys.Max();
                        //double minX = Xs.Min();
                        //double minY = Ys.Min();

                        //List<Point2d> pts = new List<Point2d>();
                        //pts.Add(new Point2d(minX, minY));
                        //pts.Add(new Point2d(minX, maxY));
                        //pts.Add(new Point2d(maxX, maxY));
                        //pts.Add(new Point2d(maxX, minY));

                        //Polyline bboxPl = new Polyline();
                        //foreach (Point2d p2d in pts)
                        //    bboxPl.AddVertexAt(bboxPl.NumberOfVertices, p2d, 0.0, 0.0, 0.0);
                        //bboxPl.Closed = true;
                        //bboxPl.AddEntityToDbModelSpace(localDb);
                        #endregion
                        //prdDbg($"Gruppe: {stikGruppeCount}, Antal: {group.Count()}");
                        //Write stikgruppe to the ps
                        foreach (Polyline pl in group)
                            psm.WritePropertyString(pl, driPipelineData.BelongsToAlignment, $"Stik {stikGruppeCount}");

                        #region Rearrange stik so that polylines always start at source
                        #region Find the one (root) connected to supply line
                        Polyline root = default;
                        Polyline supply = default;
                        foreach (Polyline pline in group)
                        {
                            supply = mainPipes.Where(x => pline.IsConnectedTo(x)).FirstOrDefault();
                            if (supply != default)
                            {
                                root = pline;
                                break;
                            }
                        }
                        #endregion
                        #region Recursive run through all connected pipes
                        //Catch case where no connection exists between stik and supply pipe
                        //Warn user about this to fix it and skip iteration
                        if (root == default)
                        {
                            prdDbg($"Stikgruppe {stikGruppeCount} is not connected to a supply pipe!\nFix this before continuing!");
                            continue;
                        }
                        //Case: supply pipe is found
                        //Go through the polylines reversing those that are against the "flow"
                        //The flow is from the supply line to end connections (clients)
                        else
                        {
                            //Give root preferential treatment because it is the first one
                            if (root.EndPoint.IsOnCurve(supply, 0.022))
                            {
                                root.CheckOrOpenForWrite();
                                root.ReverseCurve();
                            }

                            HashSet<Oid> seen = new HashSet<Oid>();
                            Stack<Polyline> stack = new Stack<Polyline>();
                            //Seed the stack with first element
                            stack.Push(root);
                            int loopGuard = 0;
                            while (stack.Count > 0)
                            {
                                loopGuard++;
                                Polyline parent = stack.Pop();
                                //Add parent to seen collection to avoid double processing
                                //AND to be able to check connectivity afterwards
                                seen.Add(parent.Id);
                                //Find connected lines
                                var query = group.Where(x => parent.EndIsConnectedTo(x) && !seen.Contains(x.Id));

                                //Check the direction and reverse if needed
                                foreach (Polyline child in query)
                                {
                                    //Reverse polyline if it is reversed
                                    if (child.EndPoint.IsOnCurve(parent, 0.025))
                                    {
                                        child.CheckOrOpenForWrite();
                                        child.ReverseCurve();
                                    }

                                    stack.Push(child);
                                }

                                if (loopGuard > 500)
                                {
                                    prdDbg("Possibly infinite loop detected!");
                                    prdDbg($"Check entity {parent.Handle}!");
                                    break;
                                }
                            }

                            //Check connectivity inside a stik group
                            if (group.Any(x => !seen.Contains(x.Id)))
                            {
                                prdDbg($"Stikgruppe {stikGruppeCount} er ikke forbundet komplet!\nKontroller følgende handles:");
                                foreach (var item in group.Where(x => !seen.Contains(x.Id))) prdDbg(item.Handle.ToString());
                            }
                        }

                        #endregion
                        #endregion
                    }

                    double areConnected(Polyline pl1, Polyline pl2)
                    {
                        if (IsPointOnCurve(pl2, pl1.StartPoint)) return 0.0;
                        if (IsPointOnCurve(pl2, pl1.EndPoint)) return 0.0;
                        if (IsPointOnCurve(pl1, pl2.StartPoint)) return 0.0;
                        if (IsPointOnCurve(pl1, pl2.EndPoint)) return 0.0;
                        return 1.0;
                    }
                    bool IsPointOnCurve(Curve cv, Point3d pt)
                    {
                        try
                        {
                            // Return true if operation succeeds
                            Point3d p = cv.GetClosestPointTo(pt, false);
                            //return (p - pt).Length <= Tolerance.Global.EqualPoint;
                            return (p - pt).Length <= 0.025;
                        }
                        catch { }
                        // Otherwise we return false
                        return false;
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    alTx.Abort();
                    alTx.Dispose();
                    alDb.Dispose();
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                alTx.Abort();
                alTx.Dispose();
                alDb.Dispose();
                tx.Commit();
            }
        }

        [CommandMethod("CHECKALIGNMENTSCONNECTIVITY")]
        public void checkalignmentsconnectivity()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);

                    var spgroups = als.GroupConnected((x, y) => x.IsConnectedTo(y, 0.05));

                    string layer = "0-Alignment-connectivity-check";
                    localDb.CheckOrCreateLayer(layer, 2, false);

                    int i = 0;
                    foreach (var group in spgroups)
                    {
                        List<Point2d> pts = new List<Point2d>();

                        i++;
                        prdDbg($"Spatial group {i} - {group.Count} alignment(s):");
                        foreach (var item in group)
                        {
                            prdDbg(item.Name);
                            var pl = item.GetPolyline().Go<Polyline>(tx);
                            pts.AddRange(pl.GetSamplePoints());
                            pl.UpgradeOpen();
                            pl.Erase();
                        }
                        prdDbg("");

                        var hull = ConvexHull.Compute(pts);
                        Polyline polyline = new Polyline();
                        polyline.Layer = layer;
                        foreach (var item in hull) polyline.AddVertexAt(polyline.NumberOfVertices, item, 0, 0, 0);
                        polyline.Closed = true;
                        polyline.ConstantWidth = 0.5;
                        polyline.AddEntityToDbModelSpace(localDb);
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

        [CommandMethod("GRAPHWRITEV2")]
        public void graphwritev2()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataReferencesOptions dro = new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            // open the xref database
            Database alDb = new Database(false, true);
            alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    graphclear();
                    graphpopulate();

                    HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                    var ents = localDb.GetFjvEntities(tx, fjvKomponenter, true, false);

                    #region Initialize property set
                    PropertySetManager psmPipeline = new PropertySetManager(localDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineDef = new PSetDefs.DriPipelineData();
                    PropertySetManager psmGraph = new PropertySetManager(localDb,
                        PSetDefs.DefinedSets.DriGraph);
                    PSetDefs.DriGraph driGraphDef = new PSetDefs.DriGraph();
                    #endregion

                    List<Pipeline> pipelines = new List<Pipeline>();
                    int pipelineCount = 0;
                    foreach (var al in als)
                    {
                        pipelineCount++;
                        var alEnts = ents.Where(
                            x => psmPipeline.ReadPropertyString(
                                x, driPipelineDef.BelongsToAlignment) == al.Name);

                        if (alEnts.Count() == 0) continue;

                        pipelines.Add(new Pipeline(al, alEnts, fjvKomponenter, pipelineCount));
                    }

                    var spgroups = pipelines.GroupConnected((x, y) => x.IsConnectedTo(y, 0.05));

                    List<GraphNodeV2> rootNodes = new List<GraphNodeV2>();

                    foreach (var group in spgroups)
                    {
                        if (group.Select(x => x.Sizes.MaxDn).Distinct().Count() == 1)
                        {
                            prdDbg($"Group has same DN! {group.First().Sizes.MaxDn}");
                            prdDbg(string.Join(", ", group.Select(x => x.Alignment.Name)));
                        }
                        else
                        {
                            rootNodes.Add(GraphNodeV2.CreateGraph(group, 0.05));
                        }
                    }

                    GraphNodeV2.ToDot(rootNodes);
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

        [CommandMethod("AUTOREVERSEPOLYLINES")]
        public void autoreversepolylines()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataReferencesOptions dro = new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            // open the xref database
            Database alDb = new Database(false, true);
            alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    graphclear();
                    graphpopulate();

                    HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                    var ents = localDb.GetFjvEntities(tx, fjvKomponenter, true, false);

                    #region Initialize property set
                    PropertySetManager psmPipeline = new PropertySetManager(localDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineDef = new PSetDefs.DriPipelineData();
                    PropertySetManager psmGraph = new PropertySetManager(localDb,
                        PSetDefs.DefinedSets.DriGraph);
                    PSetDefs.DriGraph driGraphDef = new PSetDefs.DriGraph();
                    #endregion

                    List<Pipeline> pipelines = new List<Pipeline>();
                    int pipelineCount = 0;
                    foreach (var al in als)
                    {
                        pipelineCount++;
                        var alEnts = ents.Where(
                            x => psmPipeline.ReadPropertyString(
                                x, driPipelineDef.BelongsToAlignment) == al.Name);

                        if (alEnts.Count() == 0) continue;

                        pipelines.Add(new Pipeline(al, alEnts, fjvKomponenter, pipelineCount));
                    }

                    var spgroups = pipelines.GroupConnected((x, y) => x.IsConnectedTo(y, 0.05));

                    List<GraphNodeV2> rootNodes = new List<GraphNodeV2>();

                    foreach (var group in spgroups)
                    {
                        if (group.Select(x => x.Sizes.MaxDn).Distinct().Count() == 1)
                        {
                            prdDbg($"Group has same DN! {group.First().Sizes.MaxDn}");
                            prdDbg(string.Join(", ", group.Select(x => x.Alignment.Name)));
                        }
                        else
                        {
                            rootNodes.Add(GraphNodeV2.CreateGraph(group, 0.05));
                        }
                    }

                    foreach (var rootNode in rootNodes)
                    {
                        rootNode.TraverseGraphAndReversePolylines();
                    }
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

        //[CommandMethod("TESTASSIGNMENT")]
        public void testassignment()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            DataReferencesOptions dro = new DataReferencesOptions();
            string projectName = dro.ProjectName;
            string etapeName = dro.EtapeName;

            prdDbg("Kører med antagelse, at alle flex rør (CU, ALUPEX) er stikledninger.");

            // open the xref database
            Database alDb = new Database(false, true);
            alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                FileOpenMode.OpenForReadAndAllShare, false, null);
            Transaction alTx = alDb.TransactionManager.StartTransaction();

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Alignment> als = alDb.HashSetOfType<Alignment>(alTx);
                    HashSet<Polyline> allPipes = localDb.GetFjvPipes(tx);
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);

                    #region Initialize property set
                    PropertySetManager psm = new PropertySetManager(
                        localDb,
                        PSetDefs.DefinedSets.DriPipelineData);
                    PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();
                    #endregion

                    #region Blocks
                    foreach (BlockReference br in brs)
                    {
                        //Guard against unknown blocks
                        if (ReadStringParameterFromDataTable(
                            br.RealName(), fjvKomponenter, "Navn", 0) == null)
                            continue;

                        HashSet<(BlockReference block, double dist, Alignment al)> alDistTuples =
                            new HashSet<(BlockReference, double, Alignment)>();
                        try
                        {
                            foreach (Alignment al in als)
                            {
                                if (al.Length < 1) continue;
                                Polyline pline = al.GetPolyline().Go<Polyline>(alTx);
                                Point3d tP3d = pline.GetClosestPointTo(br.Position, false);
                                alDistTuples.Add((br, tP3d.DistanceHorizontalTo(br.Position), al));
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
                            for (int i = 0; i < 4; i++)
                            {
                                distThreshold += 0.1;
                                if (result.Count() != 0) break;
                                if (i == 3)
                                {
                                    //Red line means check result
                                    //This is caught if no result found at ALL
                                    Line line = new Line(new Point3d(), br.Position);
                                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                    line.AddEntityToDbModelSpace(localDb);
                                }
                            }

                            if (result.Count() > 0)
                            {
                                //This is caught if a result was found after some iterations
                                //So the result must be checked to see, if components
                                //Not belonging to the alignment got selected
                                //Magenta
                                Line line = new Line(new Point3d(), br.Position);
                                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                line.AddEntityToDbModelSpace(localDb);
                            }
                        }

                        if (result.Count() == 0)
                        {
                            psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, "NA");
                        }
                        else if (result.Count() == 2)
                        {//Should be ordinary branch
                            var first = result.First();
                            var second = result.Skip(1).First();

                            double rotation = br.Rotation;
                            Vector3d brDir = new Vector3d(Math.Cos(rotation), Math.Sin(rotation), 0);

                            //First
                            Point3d firstClosestPoint = first.al.GetClosestPointTo(br.Position, false);
                            Vector3d firstDeriv = first.al.GetFirstDerivative(firstClosestPoint);
                            double firstDotProduct = Math.Abs(brDir.DotProduct(firstDeriv));
                            prdDbg("Ent: " + br.Handle);
                            prdDbg("First");
                            prdDbg($"Rotation: {rotation} - First: {first.al.Name}: {Math.Atan2(firstDeriv.Y, firstDeriv.X)}");
                            prdDbg($"Dot product: {firstDotProduct}");

                            //Second
                            Point3d secondClosestPoint = second.al.GetClosestPointTo(br.Position, false);
                            Vector3d secondDeriv = second.al.GetFirstDerivative(secondClosestPoint);
                            double secondDotProduct = Math.Abs(brDir.DotProduct(secondDeriv));
                            prdDbg("Second");
                            prdDbg($"Rotation: {rotation} - Second: {second.al.Name}: {Math.Atan2(secondDeriv.Y, secondDeriv.X)}");
                            prdDbg($"Dot product: {secondDotProduct}");

                            Alignment mainAl = null;
                            Alignment branchAl = null;

                            if (firstDotProduct > 0.9)
                            {
                                mainAl = first.al;
                                branchAl = second.al;
                            }
                            else if (secondDotProduct > 0.9)
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
                                //Yellow
                                Line line = new Line(new Point3d(), first.block.Position);
                                line.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);
                                line.AddEntityToDbModelSpace(localDb);
                                continue;
                            }

                            if (
                                //br.RealName() == "AFGRSTUDS" ||
                                br.RealName() == "SH LIGE")
                            {
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, branchAl.Name);
                                psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, mainAl.Name);
                            }
                            else
                            {
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, mainAl.Name);
                                psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, branchAl.Name);
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
                        else if (result.Count() == 1)
                        {
                            if (br.RealName() == "AFGRSTUDS" ||
                                br.RealName() == "SH LIGE")
                            {
                                psm.WritePropertyString(br, driPipelineData.BranchesOffToAlignment, result.First().al.Name);
                            }
                            else
                            {
                                psm.WritePropertyString(br, driPipelineData.BelongsToAlignment, result.First().al.Name);
                            }
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
                    editor.WriteMessage("\n" + ex.ToString());
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

        [CommandMethod("HIGHLIGHTNAS")]
        public void highlightnas()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Entity> ents = localDb.GetFjvEntities(tx, fjvKomponenter, true, true);

                    foreach (var ent in ents)
                    {
                        if (PropertySetManager.IsPropertySetAttached(ent, "DriPipelineData"))
                        {
                            if (PropertySetManager.ReadNonDefinedPropertySetString(
                                ent, "DriPipelineData", "BelongsToAlignment") == "NA")
                            {
                                if (ent is BlockReference br)
                                {
                                    ColorAllEntsInBr(br);
                                }
                                else if (ent is Polyline pline)
                                {
                                    ent.CheckOrOpenForWrite();
                                    ent.Color = ColorByName("cyan");
                                }
                            }
                        }
                    }

                    void ColorAllEntsInBr(BlockReference bref)
                    {
                        BlockTableRecord btr = bref.BlockTableRecord.Go<BlockTableRecord>(tx);
                        foreach (Oid id in btr)
                        {
                            if (id.IsDerivedFrom<BlockReference>())
                            {
                                //ColorAllEntsInBr(id.Go<BlockReference>(tx));
                            }
                            else
                            {
                                Entity ent = id.Go<Entity>(tx);
                                ent.CheckOrOpenForWrite();
                                ent.Color = ColorByName("cyan");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }

            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        [CommandMethod("HIGHLIGHTRESET")]
        public void highlightreset()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Entity> ents = localDb.GetFjvEntities(tx, fjvKomponenter, true, true);

                    foreach (var ent in ents)
                    {
                        if (PropertySetManager.IsPropertySetAttached(ent, "DriPipelineData"))
                        {
                            if (PropertySetManager.ReadNonDefinedPropertySetString(
                                ent, "DriPipelineData", "BelongsToAlignment") == "NA")
                            {
                                if (ent is BlockReference br)
                                {
                                    ResetColorAllEntsInBr(br);
                                }
                                else if (ent is Polyline pline)
                                {
                                    ent.CheckOrOpenForWrite();
                                    ent.Color = ColorByName("bylayer");
                                }
                            }
                        }
                    }

                    void ResetColorAllEntsInBr(BlockReference bref)
                    {
                        BlockTableRecord btr = bref.BlockTableRecord.Go<BlockTableRecord>(tx);
                        foreach (Oid id in btr)
                        {
                            if (id.IsDerivedFrom<BlockReference>())
                            {
                                //ResetColorAllEntsInBr(id.Go<BlockReference>(tx));
                            }
                            else
                            {
                                Entity ent = id.Go<Entity>(tx);
                                ent.CheckOrOpenForWrite();
                                ent.Color = ColorByName("byblock");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }

            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        [CommandMethod("CREATEALIGNMENT")]
        public void createalignment()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable fjvKomponenter = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");

                    HashSet<Entity> ents = localDb.GetFjvEntities(tx, fjvKomponenter, true, true);

                    graphclear();
                    graphpopulate();



                    
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }

            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }

        [CommandMethod("decoratepolylines")]
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
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                Database xRefFjvDB = null;
                Transaction xRefFjvTx = null;
                try
                {
                    #region Load linework
                    DataReferencesOptions dro = new DataReferencesOptions();
                    string projectName = dro.ProjectName;
                    string etapeName = dro.EtapeName;
                    editor.WriteMessage("\n" + GetPathToDataFiles(projectName, etapeName, "Fremtid"));

                    // open the LER dwg database
                    xRefFjvDB = new Database(false, true);

                    xRefFjvDB.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                        FileOpenMode.OpenForReadAndAllShare, false, null);
                    xRefFjvTx = xRefFjvDB.TransactionManager.StartTransaction();

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
