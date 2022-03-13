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
using Microsoft.Office.Interop.Excel;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;

namespace IntersectUtilities.Dimensionering
{
    /// <summary>
    /// Class for intersection tools.
    /// </summary>
    public class DimensioneringExtension : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\n-> Dump all addresses to file: DIMADRESSERDUMP");
            doc.Editor.WriteMessage("\n-> Populate property data based on geometry: DIMPOPULATEGRAPH");
            doc.Editor.WriteMessage("\n-> Dump graph to text file: DIMDUMPGRAPH");
            doc.Editor.WriteMessage("\n-> Write graph to DOT (Graphviz) file: DIMWRITEGRAPH");
            doc.Editor.WriteMessage("\n-> Write data to excel: -> DIMWRITEEXCEL");
        }

        public void Terminate()
        {
        }
        #endregion

        [CommandMethod("DIMADRESSERDUMP")]
        public void dimadresserdump()
        {
            Dimensionering.dimadressedump(Dimensionering.CurrentEtapeName);
        }

        [CommandMethod("DIMPOPULATEGRAPH")]
        public void dimpopulategraph()
        {
            Dimensionering.dimpopulategraph(Dimensionering.CurrentEtapeName);
        }

        [CommandMethod("DIMDUMPGRAPH")]
        public void dimdumpgraph()
        {
            Dimensionering.dimdumpgraph(Dimensionering.CurrentEtapeName);
        }

        [CommandMethod("DIMWRITEGRAPH")]
        public void dimwritegraph()
        {
            Dimensionering.dimwritegraph(Dimensionering.CurrentEtapeName);
        }

        [CommandMethod("DIMWRITEEXCEL")]
        public void dimwriteexcel()
        {
            Dimensionering.dimwriteexcel(Dimensionering.CurrentEtapeName);
        }
    }

    internal class Stik
    {
        internal double Dist;
        internal Oid ParentId;
        internal Oid ChildId;
        internal Point3d NearestPoint;

        internal Stik(double dist, Oid parentId, Oid childId, Point3d nearestPoint)
        {
            Dist = dist;
            ParentId = parentId;
            ChildId = childId;
            NearestPoint = nearestPoint;
        }
    }
    internal class POI
    {
        internal Oid OwnerId { get; }
        internal Point3d Point { get; }
        internal EndTypeEnum EndType { get; }
        internal POI(Oid ownerId, Point3d point, EndTypeEnum endType)
        { OwnerId = ownerId; Point = point; EndType = endType; }
        internal bool IsSameOwner(POI toCompare) => OwnerId == toCompare.OwnerId;

        internal enum EndTypeEnum
        {
            Start,
            End
        }
    }
    internal static class Dimensionering
    {
        internal static string CurrentEtapeName = "Etape 9.3";
        internal static GlobalSheetCount GlobalSheetCount { get; set; }
        internal static readonly string PRef = "Pref:";
        internal static void dimadressedump(string curEtapeName)
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
                    #region dump af adresser
                    HashSet<string> acceptedTypes = new HashSet<string>() { "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet" };

                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => acceptedTypes.Contains(PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .ToHashSet();

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Adresse;Energiforbrug;Antal ejendomme;Antal boliger med varmtvandsforbrug;Stik længde (tracé) [m]");

                    foreach (BlockReference br in brs)
                    {
                        string vejnavn = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                        string husnummer = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                        string estVarmeForbrug = (PropertySetManager.ReadNonDefinedPropertySetDouble(br, "BBR", "EstimeretVarmeForbrug") * 1000).ToString("0.##");
                        string antalEjendomme = "1";
                        string antalBoligerOsv = "1";

                        string handleString = PropertySetManager.ReadNonDefinedPropertySetString(br, "DriDimGraph", "Parent");
                        Handle parent = new Handle(Convert.ToInt64(handleString, 16));
                        Line line = parent.Go<Line>(localDb);
                        string stikLængde = line.Length.ToString("0.##");

                        sb.AppendLine($"{vejnavn} {husnummer};{estVarmeForbrug};{antalEjendomme};{antalBoligerOsv};{stikLængde}");
                    }

                    //Build file name
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string dumpExportFileName = path + "\\dimaddressdump.csv";

                    Utils.ClrFile(dumpExportFileName);
                    Utils.OutputWriter(dumpExportFileName, sb.ToString());

                    System.Windows.Forms.Application.DoEvents();

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void dimpopulategraph(string curEtapeName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Gather elements
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    HashSet<string> acceptedTypes = new HashSet<string>() { "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet" };
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => acceptedTypes.Contains(PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .ToHashSet();
                    prdDbg("Nr. of blocks " + brs.Count().ToString());

                    HashSet<Line> lines = localDb.HashSetOfType<Line>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of lines " + lines.Count().ToString());

                    HashSet<Line> noCross = localDb.HashSetOfType<Line>(tx, true)
                        .Where(x => x.Layer == "0-NOCROSS_LINE")
                        .ToHashSet();
                    #endregion

                    #region Clear previous data
                    HashSet<Entity> entities = new HashSet<Entity>();
                    entities.UnionWith(plines);
                    entities.UnionWith(brs);
                    entities.UnionWith(lines);
                    #endregion

                    #region Clear previous data
                    foreach (Entity entity in entities)
                    {
                        graphPsm.GetOrAttachPropertySet(entity);
                        graphPsm.WritePropertyString(graphDef.Parent, "");
                        graphPsm.WritePropertyString(graphDef.Children, "");
                    }
                    #endregion

                    #region Delete old labels
                    var labels = localDb
                        .HashSetOfType<DBText>(tx)
                        .Where(x => x.Layer == "0-FJV_Strækning_label");

                    foreach (DBText label in labels)
                    {
                        label.CheckOrOpenForWrite();
                        label.Erase(true);
                    }
                    #endregion

                    #region Delete old debug plines
                    foreach (Polyline pline in localDb.ListOfType<Polyline>(tx))
                    {
                        if (pline.Layer != "0-FJV_Debug") continue;
                        pline.CheckOrOpenForWrite();
                        pline.Erase(true);
                    }
                    #endregion

                    #region Manage layer to contain connection lines
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                    string conLayName = "0-CONNECTION_LINE";
                    if (!lt.Has(conLayName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = conLayName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);

                        //Make layertable writable
                        lt.CheckOrOpenForWrite();

                        //Add the new layer to layer table
                        Oid ltId = lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }

                    string noCrossLayName = "0-NOCROSS_LINE";
                    if (!lt.Has(noCrossLayName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = noCrossLayName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                        ltr.LineWeight = LineWeight.LineWeight030;

                        //Make layertable writable
                        lt.CheckOrOpenForWrite();

                        //Add the new layer to layer table
                        Oid ltId = lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }

                    #endregion

                    #region Delete previous stiks
                    HashSet<Line> eksStik = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == conLayName)
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName)
                        .ToHashSet();
                    foreach (Entity entity in eksStik)
                    {
                        entity.CheckOrOpenForWrite();
                        entity.Erase(true);
                    }
                    #endregion

                    #region Traverse system and build graph
                    #region Determine entry points
                    HashSet<POI> allPoiCol = new HashSet<POI>();
                    foreach (Polyline item in plines)
                    {
                        allPoiCol.Add(new POI(item.Id, item.StartPoint, POI.EndTypeEnum.Start));
                        allPoiCol.Add(new POI(item.Id, item.EndPoint, POI.EndTypeEnum.End));
                    }

                    var entryPoints = allPoiCol
                            .GroupByCluster((x, y) => x.Point.DistanceHorizontalTo(y.Point), 0.005)
                            .Where(x => x.Count() == 1 && x.Key.EndType == POI.EndTypeEnum.Start);
                    prdDbg($"Entry points count: {entryPoints.Count()}");
                    #endregion

                    int groupCounter = 0;
                    foreach (IGrouping<POI, POI> entryPoint in entryPoints)
                    {
                        //Debug
                        HashSet<Node> nodes = new HashSet<Node>();

                        groupCounter++;

                        //Get the entrypoint element
                        Polyline entryPline = entryPoint.Key.OwnerId.Go<Polyline>(tx);

                        //Mark the element as entry point
                        graphPsm.GetOrAttachPropertySet(entryPline);
                        graphPsm.WritePropertyString(graphDef.Parent, $"Entry");

                        #region Traverse system using stack
                        //Using stack traversing strategy
                        Stack<Node> stack = new Stack<Node>();
                        Node startNode = new Node();
                        startNode.Self = entryPline;
                        stack.Push(startNode);
                        int subGroupCounter = 0;
                        while (stack.Count > 0)
                        {
                            subGroupCounter++;

                            Node curNode = stack.Pop();
                            nodes.Add(curNode);

                            //Write group and subgroup numbers
                            curNode.GroupNumber = groupCounter;
                            curNode.PartNumber = subGroupCounter;

                            //Find next connections
                            var query = allPoiCol.Where(
                                x => x.Point.DistanceHorizontalTo(curNode.Self.EndPoint) < 0.005 &&
                                x.EndType == POI.EndTypeEnum.Start);

                            foreach (POI poi in query)
                            {
                                Polyline connectedItem = poi.OwnerId.Go<Polyline>(tx);

                                Node childNode = new Node();
                                childNode.Self = connectedItem;
                                childNode.Parent = curNode;
                                curNode.ConnectionChildren.Add(childNode);

                                //Push the child to stack for further processing
                                stack.Push(childNode);
                            }
                        }
                        #endregion

                        #region Find clients for this strækning
                        foreach (BlockReference br in brs)
                        {
                            List<Stik> res = new List<Stik>();
                            foreach (Polyline pline in plines)
                            {
                                Point3d closestPoint = pline.GetClosestPointTo(br.Position, false);

                                if (noCross.Count == 0)
                                {
                                    res.Add(new Stik(br.Position.DistanceHorizontalTo(closestPoint),
                                        pline.Id, br.Id, closestPoint));
                                }
                                else
                                {
                                    int intersectionsCounter = 0;
                                    foreach (Line noCrossLine in noCross)
                                    {
                                        using (Line testLine = new Line(br.Position, closestPoint))
                                        using (Point3dCollection p3dcol = new Point3dCollection())
                                        {
                                            noCrossLine.IntersectWith(
                                                testLine, 0, new Plane(), p3dcol, new IntPtr(0), new IntPtr(0));
                                            if (p3dcol.Count > 0) intersectionsCounter += p3dcol.Count;
                                        }
                                    }
                                    if (intersectionsCounter == 0)
                                        res.Add(new Stik(br.Position.DistanceHorizontalTo(
                                            closestPoint), pline.Id, br.Id, closestPoint));
                                }
                            }

                            //Find and add the client blocks to node collection
                            var nearest = res.MinBy(x => x.Dist).FirstOrDefault();
                            if (nearest == default) continue;

                            if (nodes.Any(x => x.Self.Id == nearest.ParentId))
                            {
                                nodes.Where(x => x.Self.Id == nearest.ParentId)
                                    .ToList().ForEach(x => x.ClientChildren
                                    .Add(nearest.ChildId.Go<BlockReference>(tx)));
                            }
                        }
                        #endregion

                        var ordered = nodes.OrderBy(x => x.PartNumber);

                        #region Reorder nodes, doesn't make sense
                        //int newNumber = 0;
                        //foreach (var node in ordered)
                        //{
                        //    newNumber++;
                        //    node.PartNumber = newNumber;
                        //}
                        #endregion

                        #region Write node data
                        foreach (Node nd in ordered)
                        {
                            Polyline curPline = nd.Self;

                            //Write parent data if it is not entry
                            graphPsm.GetOrAttachPropertySet(curPline);

                            if (graphPsm.ReadPropertyString(graphDef.Parent) != "Entry")
                                graphPsm.WritePropertyString(
                                    graphDef.Parent, nd.Parent.Self.Handle.ToString());

                            //Write strækning id to property
                            fjvFremPsm.GetOrAttachPropertySet(curPline);
                            fjvFremPsm.WritePropertyString(fjvFremDef.Bemærkninger,
                                $"Strækning {nd.GroupNumber}.{nd.PartNumber}");

                            //Write the children data
                            foreach (Node child in nd.ConnectionChildren)
                            {
                                string curChildrenString = graphPsm.ReadPropertyString(graphDef.Children);
                                string childHandle = child.Self.Handle.ToString() + ";";
                                if (!curChildrenString.Contains(childHandle)) curChildrenString += childHandle;
                                graphPsm.WritePropertyString(graphDef.Children, curChildrenString);
                            }

                            foreach (BlockReference client in nd.ClientChildren)
                            {
                                //Create connection line
                                Line connection = new Line();
                                connection.SetDatabaseDefaults();
                                connection.Layer = conLayName;
                                connection.StartPoint = curPline
                                    .GetClosestPointTo(client.Position, false);
                                connection.EndPoint = client.Position;
                                connection.AddEntityToDbModelSpace(localDb);

                                //Add connection as parent to client
                                graphPsm.GetOrAttachPropertySet(client);
                                graphPsm.WritePropertyString(graphDef.Parent, connection.Handle.ToString());

                                //Populate connection props
                                graphPsm.GetOrAttachPropertySet(connection);
                                graphPsm.WritePropertyString(graphDef.Children, client.Handle.ToString() + ";");
                                graphPsm.WritePropertyString(graphDef.Parent, curPline.Handle.ToString());

                                //Write area data to connection
                                fjvFremPsm.GetOrAttachPropertySet(connection);
                                fjvFremPsm.WritePropertyString(fjvFremDef.Distriktets_navn, curEtapeName);
                                fjvFremPsm.WritePropertyString(fjvFremDef.Bemærkninger, "Stik");

                                //Add connection to pline's children
                                graphPsm.GetOrAttachPropertySet(curPline);
                                string curChildrenString = graphPsm.ReadPropertyString(graphDef.Children);
                                string childHandle = connection.Handle.ToString() + ";";
                                if (!curChildrenString.Contains(childHandle)) curChildrenString += childHandle;
                                graphPsm.WritePropertyString(graphDef.Children, curChildrenString);
                            }

                            //Write label
                            string strækningsNr = $"{nd.GroupNumber}.{nd.PartNumber}";

                            //Place label to mark the strækning
                            Point3d midPoint = curPline.GetPointAtDist(curPline.Length / 2);
                            DBText text = new DBText();
                            text.SetDatabaseDefaults();
                            text.TextString = strækningsNr;
                            text.Height = 2.5;
                            text.Position = midPoint;
                            localDb.CheckOrCreateLayer("0-FJV_Strækning_label");
                            text.Layer = "0-FJV_Strækning_label";
                            text.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion

                        #region Debug
                        //Debug
                        List<Point2d> pts = new List<Point2d>();
                        foreach (Node nd in nodes)
                        {
                            for (int i = 0; i < nd.Self.NumberOfVertices; i++)
                            {
                                pts.Add(nd.Self.GetPoint3dAt(i).To2D());
                            }
                        }

                        Polyline circum = PolylineFromConvexHull(pts);
                        circum.AddEntityToDbModelSpace(localDb);
                        localDb.CheckOrCreateLayer("0-FJV_Debug");
                        circum.Layer = "0-FJV_Debug";
                        #endregion
                    }

                    System.Windows.Forms.Application.DoEvents();
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void dimdumpgraph(string curEtapeName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                HashSet<Strækning> strækninger = new HashSet<Strækning>();

                try
                {
                    #region Traverse system and build graph
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    var entryElements = plines.Where(x => graphPsm.FilterPropetyString(x, graphDef.Parent, "Entry"));
                    prdDbg($"Nr. of entry elements: {entryElements.Count()}");

                    //StringBuilder to dump the text
                    StringBuilder sb = new StringBuilder();

                    foreach (Polyline entryElement in entryElements)
                    {
                        //Write group number
                        fjvFremPsm.GetOrAttachPropertySet(entryElement);
                        string strNr = fjvFremPsm.ReadPropertyString(
                            fjvFremDef.Bemærkninger).Replace("Strækning ", "");

                        sb.AppendLine($"****** Rørgruppe nr.: {strNr.Split('.')[0]} ******");
                        sb.AppendLine();

                        //Using stack traversing strategy
                        Stack<Polyline> stack = new Stack<Polyline>();
                        stack.Push(entryElement);
                        while (stack.Count > 0)
                        {
                            Polyline curItem = stack.Pop();

                            //Write group and subgroup numbers
                            fjvFremPsm.GetOrAttachPropertySet(curItem);
                            string strNrString = fjvFremPsm.ReadPropertyString(fjvFremDef.Bemærkninger);
                            sb.AppendLine($"--> {strNrString} <--");

                            strNr = strNrString.Replace("Strækning ", "");

                            //Get the children
                            HashSet<Entity> children = new HashSet<Entity>();
                            Dimensionering.GatherChildren(curItem, localDb, graphPsm, children);

                            //Populate strækning with data
                            Strækning strækning = new Strækning();
                            strækninger.Add(strækning);
                            strækning.GroupNumber = Convert.ToInt32(strNr.Split('.')[0]);
                            strækning.PartNumber = Convert.ToInt32(strNr.Split('.')[1]);
                            strækning.Self = curItem;

                            graphPsm.GetOrAttachPropertySet(curItem);
                            string parentHandleString = graphPsm.ReadPropertyString(graphDef.Parent);
                            try
                            {
                                if (parentHandleString != "Entry")
                                {
                                    strækning.Parent = localDb.Go<Entity>(parentHandleString);
                                }
                            }
                            catch (System.Exception)
                            {
                                prdDbg(parentHandleString);
                                throw;
                            }

                            //First print connections
                            foreach (Entity child in children)
                            {
                                if (child is Polyline pline)
                                {
                                    fjvFremPsm.GetOrAttachPropertySet(pline);
                                    string childStrNr = fjvFremPsm.ReadPropertyString(fjvFremDef.Bemærkninger).Replace("Strækning ", "");
                                    sb.AppendLine($"{strNr} -> {childStrNr}");

                                    //Push the polyline in to stack to continue iterating
                                    stack.Push(pline);

                                    //Populate strækning with child data
                                    strækning.ConnectionChildren.Add(pline);
                                }
                            }

                            foreach (Entity child in children)
                            {
                                if (child is BlockReference br)
                                {
                                    string vejnavn = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                                    string husnummer = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");

                                    Point3d np = curItem.GetClosestPointTo(br.Position, false);
                                    double st = curItem.GetDistAtPoint(np);

                                    sb.AppendLine($"{vejnavn} {husnummer} - {st.ToString("0.##")}");

                                    //Populate strækning with client data
                                    strækning.ClientChildren.Add(br);
                                }
                            }

                            sb.AppendLine();
                        }
                    }

                    System.Windows.Forms.Application.DoEvents();

                    //Build file name
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string dumpExportFileName = path + "\\dimgraphdump.txt";

                    Utils.ClrFile(dumpExportFileName);
                    Utils.OutputWriter(dumpExportFileName, sb.ToString());
                    #endregion

                    #region Write graph to csv
                    var groups = strækninger.GroupBy(x => x.GroupNumber);
                    foreach (var group in groups)
                    {
                        var ordered = group.OrderBy(x => x.PartNumber);
                        //int newCount = 0;
                        //foreach (var item in ordered)
                        //{
                        //    newCount++;
                        //    item.PartNumber = newCount;
                        //}

                        foreach (var item in ordered) item.PopulateData();

                        int maxSize = ordered.MaxBy(x => x.Data.Count).FirstOrDefault().Data.Count;
                        prdDbg(maxSize.ToString());
                        foreach (var item in ordered) item.PadLists(maxSize);

                        StringBuilder sbG = new StringBuilder();

                        for (int i = 0; i < maxSize; i++)
                        {
                            foreach (var item in ordered)
                            {
                                sbG.Append(item.Data[i] + ";");
                                sbG.Append(item.Distances[i] + ";");
                            }
                            sbG.AppendLine();
                        }

                        dumpExportFileName = path + $"\\Strækning {group.Key}.csv";

                        Utils.ClrFile(dumpExportFileName);
                        Utils.OutputWriter(dumpExportFileName, sbG.ToString());
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void dimwritegraph(string curEtapeName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //Settings
                string arrowShape = " -> ";

                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Traverse system and build graph
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    var entryElements = plines.Where(x => graphPsm.FilterPropetyString(x, graphDef.Parent, "Entry"));
                    prdDbg($"Nr. of entry elements: {entryElements.Count()}");

                    //StringBuilder to dump the text
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("digraph G {");

                    foreach (Polyline entryElement in entryElements)
                    {
                        //Collection to store subgraph clusters
                        HashSet<Subgraph> subgraphs = new HashSet<Subgraph>();

                        //Write group number
                        fjvFremPsm.GetOrAttachPropertySet(entryElement);
                        string strNr = fjvFremPsm.ReadPropertyString(
                            fjvFremDef.Bemærkninger).Replace("Strækning ", "");

                        string subGraphNr = strNr.Split('.')[0];

                        sb.AppendLine($"subgraph G_{strNr.Split('.')[0]} {{");
                        //sb.AppendLine($"label = \"Delstrækning nr. {subGraphNr}\"");
                        sb.AppendLine("node [shape=record];");

                        //Using stack traversing strategy
                        Stack<Polyline> stack = new Stack<Polyline>();
                        stack.Push(entryElement);
                        while (stack.Count > 0)
                        {
                            Polyline curItem = stack.Pop();

                            //Write group and subgroup numbers
                            fjvFremPsm.GetOrAttachPropertySet(curItem);
                            string strNrString = fjvFremPsm.ReadPropertyString(fjvFremDef.Bemærkninger);
                            strNr = strNrString.Replace("Strækning ", "");

                            string curNodeHandle = curItem.Handle.ToString();

                            //Write label
                            sb.AppendLine(
                                $"\"{curNodeHandle}\" " +
                                $"[label = \"{{{strNrString}|{curNodeHandle}}}\"];");

                            //Get the children
                            HashSet<Entity> children = new HashSet<Entity>();
                            Dimensionering.GatherChildren(curItem, localDb, graphPsm, children);

                            //First print connections
                            foreach (Entity child in children)
                            {
                                if (child is Polyline pline)
                                {
                                    string childHandle = child.Handle.ToString();
                                    //psManFjvFremtid.GetOrAttachPropertySet(pline);
                                    //string childStrNr = psManFjvFremtid.ReadPropertyString
                                    //      (fjvFremtidDef.Bemærkninger).Replace("Strækning ", "");
                                    sb.AppendLine($"\"{curNodeHandle}\"{arrowShape}\"{childHandle}\"");

                                    //Push the polyline in to stack to continue iterating
                                    stack.Push(pline);
                                }
                            }

                            //Invoke only subgraph if clients are present
                            if (children.Any(x => x is BlockReference))
                            {
                                Subgraph subgraph = new Subgraph(localDb, curItem, strNrString);
                                subgraphs.Add(subgraph);

                                foreach (Entity child in children)
                                {
                                    if (child is BlockReference br)
                                    {
                                        string childHandle = child.Handle.ToString();
                                        sb.AppendLine($"\"{curNodeHandle}\"{arrowShape}\"{childHandle}\"");
                                        //sb.AppendLine($"{vejnavn} {husnummer} - {st.ToString("0.##")}");

                                        subgraph.Nodes.Add(child.Handle);
                                    }
                                }
                            }
                        }

                        //Write subgraphs
                        int subGraphCount = 0;
                        foreach (Subgraph sg in subgraphs)
                        {
                            subGraphCount++;
                            sb.Append(sg.WriteSubgraph(subGraphCount));
                        }

                        //Close subgraph curly braces
                        sb.AppendLine("}");
                    }

                    //Close graph curly braces
                    sb.AppendLine("}");

                    System.Windows.Forms.Application.DoEvents();

                    //Build file name
                    if (!Directory.Exists(@"C:\Temp\"))
                        Directory.CreateDirectory(@"C:\Temp\");

                    //Write the collected graphs to one file
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\DimGraph.dot"))
                    {
                        file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
                    }

                    System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    cmd.StartInfo.Arguments = @"/c ""dot -Tpdf DimGraph.dot > DimGraph.pdf""";
                    cmd.Start();
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        private static void TraversePath(ExcelNode node, int pathId)
        {
            node.PathId = pathId;
            if (node.Parent != null && node.Parent.PathId == 0) TraversePath(node.Parent, pathId);
        }
        private static IEnumerable<string> BFS(ExcelNode root)
        {
            var queue = new Queue<Tuple<string, ExcelNode>>();
            queue.Enqueue(new Tuple<string, ExcelNode>(root.Name, root));

            while (queue.Any())
            {
                var node = queue.Dequeue();
                if (node.Item2.ConnectionChildren.Any())
                {
                    foreach (var child in node.Item2.ConnectionChildren)
                    {
                        queue.Enqueue(new Tuple<string, ExcelNode>(node.Item1 + "-" + child.Name, child));
                    }
                }
                else
                {
                    yield return node.Item1;
                }
            }
        }
        /// <summary>
        /// Obsolete
        /// </summary>
        internal static void dimstikautogen(string curEtapeName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Manage layer to contain connection lines
                LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                string conLayName = "0-CONNECTION_LINE";
                if (!lt.Has(conLayName))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = conLayName;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);

                    //Make layertable writable
                    lt.CheckOrOpenForWrite();

                    //Add the new layer to layer table
                    Oid ltId = lt.Add(ltr);
                    tx.AddNewlyCreatedDBObject(ltr, true);
                }

                string noCrossLayName = "0-NOCROSS_LINE";
                if (!lt.Has(noCrossLayName))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = noCrossLayName;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                    ltr.LineWeight = LineWeight.LineWeight030;

                    //Make layertable writable
                    lt.CheckOrOpenForWrite();

                    //Add the new layer to layer table
                    Oid ltId = lt.Add(ltr);
                    tx.AddNewlyCreatedDBObject(ltr, true);
                }

                #endregion

                #region Delete previous stiks
                HashSet<Line> eksStik = localDb.HashSetOfType<Line>(tx)
                    .Where(x => x.Layer == conLayName)
                    .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName)
                    .ToHashSet();
                foreach (Entity entity in eksStik)
                {
                    entity.CheckOrOpenForWrite();
                    entity.Erase(true);
                }
                #endregion

                PropertySetManager psManFjvFremtid = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremtidDef = new PSetDefs.FJV_fremtid();

                PropertySetManager psManGraph = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph driDimGraphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Stik counting
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg($"Number of plines: {plines.Count}");

                    HashSet<string> acceptedTypes = new HashSet<string>() { "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet" };

                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => acceptedTypes.Contains(PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .ToHashSet();
                    prdDbg($"Number of blocks: {brs.Count}");

                    //Collection to hold no cross lines
                    HashSet<Line> noCross = localDb.HashSetOfType<Line>(tx, true)
                        .Where(x => x.Layer == "0-NOCROSS_LINE")
                        .ToHashSet();

                    foreach (BlockReference br in brs)
                    {
                        List<Stik> res = new List<Stik>();
                        foreach (Polyline pline in plines)
                        {
                            Point3d closestPoint = pline.GetClosestPointTo(br.Position, false);

                            if (noCross.Count == 0)
                            {
                                res.Add(new Stik(br.Position.DistanceHorizontalTo(closestPoint), pline.Id, br.Id, closestPoint));
                            }
                            else
                            {
                                int intersectionsCounter = 0;
                                foreach (Line noCrossLine in noCross)
                                {
                                    using (Line testLine = new Line(br.Position, closestPoint))
                                    using (Point3dCollection p3dcol = new Point3dCollection())
                                    {
                                        noCrossLine.IntersectWith(testLine, 0, new Plane(), p3dcol, new IntPtr(0), new IntPtr(0));
                                        if (p3dcol.Count > 0) intersectionsCounter += p3dcol.Count;
                                    }
                                }
                                if (intersectionsCounter == 0) res.Add(new Stik(br.Position.DistanceHorizontalTo(closestPoint), pline.Id, br.Id, closestPoint));
                            }
                        }

                        var nearest = res.MinBy(x => x.Dist).FirstOrDefault();
                        if (nearest == default) continue;

                        #region Create line
                        Line connection = new Line();
                        connection.SetDatabaseDefaults();
                        connection.Layer = conLayName;
                        connection.StartPoint = br.Position;
                        connection.EndPoint = nearest.NearestPoint;
                        connection.AddEntityToDbModelSpace(localDb);
                        //Write area data
                        psManFjvFremtid.GetOrAttachPropertySet(connection);
                        psManFjvFremtid.WritePropertyString(fjvFremtidDef.Distriktets_navn, curEtapeName);
                        psManFjvFremtid.WritePropertyString(fjvFremtidDef.Bemærkninger, "Stik");
                        //Write graph data
                        psManGraph.GetOrAttachPropertySet(connection);
                        psManGraph.WritePropertyString(driDimGraphDef.Children, br.Handle.ToString());
                        psManGraph.WritePropertyString(driDimGraphDef.Parent, nearest.ParentId.Go<Entity>(tx).Handle.ToString());
                        #endregion

                        #region Write BR parent data
                        psManGraph.GetOrAttachPropertySet(br);
                        psManGraph.WritePropertyString(driDimGraphDef.Parent, connection.Handle.ToString());
                        #endregion

                        #region Write PL children data
                        {
                            Polyline pline = nearest.ParentId.Go<Polyline>(tx);
                            psManGraph.GetOrAttachPropertySet(pline);
                            string currentChildren = psManGraph.ReadPropertyString(driDimGraphDef.Children);
                            currentChildren += connection.Handle.ToString() + ";";
                            psManGraph.WritePropertyString(driDimGraphDef.Children, currentChildren);
                        }
                        #endregion
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
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void GatherChildren(
            Entity ent, Database db, PropertySetManager psmGraph, HashSet<Entity> children)
        {
            PSetDefs.DriDimGraph defGraph = new PSetDefs.DriDimGraph();

            psmGraph.GetOrAttachPropertySet(ent);
            string childrenString = psmGraph.ReadPropertyString(defGraph.Children);

            var splitArray = childrenString.Split(';');

            foreach (var childString in splitArray)
            {
                if (childString.IsNoE()) continue;

                Entity child = db.Go<Entity>(childString);

                switch (child)
                {
                    case Polyline pline:
                        children.Add(child);
                        break;
                    case BlockReference br:
                        children.Add(child);
                        break;
                    case Line line:
                        GatherChildren(child, db, psmGraph, children);
                        break;
                    default:
                        throw new System.Exception($"Unexpected type {child.GetType().Name}!");
                }
            }
        }
        internal static void GatherChildren(
            Entity ent, Database db, PropertySetManager psmGraph,
            HashSet<ExcelNode> plineChildren, HashSet<BlockReference> blockChildren)
        {
            HashSet<Entity> children = new HashSet<Entity>();
            GatherChildren(ent, db, psmGraph, children);

            foreach (Entity child in children)
            {
                switch (child)
                {
                    case Polyline pline:
                        ExcelNode newNode = new ExcelNode();
                        newNode.Self = pline;
                        plineChildren.Add(newNode);
                        break;
                    case BlockReference br:
                        blockChildren.Add(br);
                        break;
                    default:
                        throw new System.Exception($"Unexpected type {child.GetType().Name}!");
                }
            }
        }
        internal static void GatherChildren(ExcelNode node, Database db)
        {
            string childrenString = 
                PropertySetManager.ReadNonDefinedPropertySetString(node.Self, "DriDimGraph", "Children");

            var splitArray = childrenString.Split(';');

            foreach (var childString in splitArray)
            {
                if (childString.IsNoE()) continue;

                Entity child = db.Go<Entity>(childString);
                
                switch (child)
                {
                    case Polyline pline:
                        ExcelNode childNode = new ExcelNode();
                        childNode.Self = pline;
                        node.ConnectionChildren.Add(childNode);
                        break;
                    case BlockReference br:
                        node.ClientChildren.Add(br);
                        break;
                    case Line line:
                        GatherChildren(line, node);
                        break;
                    default:
                        throw new System.Exception($"Unexpected type {child.GetType().Name}!");
                }
            }
        }
        internal static void GatherChildren(Line line, ExcelNode node)
        {
            string childrenString =
                PropertySetManager.ReadNonDefinedPropertySetString(line, "DriDimGraph", "Children");

            var splitArray = childrenString.Split(';');

            foreach (var childString in splitArray)
            {
                if (childString.IsNoE()) continue;

                BlockReference child = line.Database.Go<BlockReference>(childString);

                node.ClientChildren.Add(child);
            }
        }
        internal static void dimwriteexcel(string curEtapeName)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //Settings
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Init excel objects
                    #region Dialog box for selecting the excel file
                    string fileName = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose excel file:",
                        DefaultExt = "xlsm",
                        Filter = "Excel files (*.xlsm)|*.xlsm|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        fileName = dialog.FileName;
                    }
                    else { tx.Abort(); return; }
                    #endregion

                    Workbook wb;
                    Sheets wss;
                    Worksheet ws;
                    Microsoft.Office.Interop.Excel.Application oXL;
                    object misValue = System.Reflection.Missing.Value;
                    oXL = new Microsoft.Office.Interop.Excel.Application();
                    oXL.Visible = false;
                    oXL.DisplayAlerts = false;
                    wb = oXL.Workbooks.Open(fileName,
                        0, false, 5, "", "", false, XlPlatform.xlWindows, "", true, false,
                        0, false, false, XlCorruptLoad.xlNormalLoad);
                    #endregion

                    #region Traverse system and build graph
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    var entryElements = plines.Where(x => graphPsm.FilterPropetyString(
                        x, graphDef.Parent, "Entry"));
                    prdDbg($"Nr. of entry elements: {entryElements.Count()}");

                    Dimensionering.GlobalSheetCount = new GlobalSheetCount();

                    //Write graph docs
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("digraph G {");

                    foreach (Polyline entryElement in entryElements)
                    {
                        #region Build graph nodes
                        HashSet<ExcelNode> nodes = new HashSet<ExcelNode>();
                        ExcelNode seedNode = new ExcelNode();
                        seedNode.Self = entryElement;
                        seedNode.NodeLevel = 1;

                        //Using stack traversing strategy
                        Stack<ExcelNode> stack = new Stack<ExcelNode>();
                        stack.Push(seedNode);
                        while (stack.Count > 0)
                        {
                            ExcelNode node = stack.Pop();
                            nodes.Add(node);

                            //Write group and subgroup numbers
                            fjvFremPsm.GetOrAttachPropertySet(node.Self);
                            string strNrString = fjvFremPsm.ReadPropertyString(fjvFremDef.Bemærkninger);
                            node.SetGroupAndPartNumbers(strNrString);

                            //Get the children
                            Dimensionering.GatherChildren(node, localDb);

                            //First print connections
                            foreach (ExcelNode child in node.ConnectionChildren)
                            {
                                child.Parent = node;
                                child.NodeLevel = node.NodeLevel + 1;

                                //Push the childExcelNode in to stack to continue iterating
                                stack.Push(child);
                            }
                        }
                        #endregion

                        #region Find paths
                        int pathId = 0;
                        while (nodes.Any(x => x.PathId == 0))
                        {
                            pathId++;
                            var curNode = nodes
                                .Where(x => x.PathId == 0)
                                .MaxBy(x => x.NodeLevel)
                                .FirstOrDefault();
                            TraversePath(curNode, pathId);
                        }

                        //Organize paths
                        //Order is important -> using a list
                        List<Path> paths = new List<Path>();
                        var groups = nodes.GroupBy(x => x.PathId).OrderBy(x => x.Key);
                        foreach (var group in groups)
                        {
                            var ordered = group.OrderByDescending(x => x.NodeLevel);
                            prdDbg(string.Join("->", ordered.Select(x => x.Name.Replace("ækning", ""))));
                            Path path = new Path();
                            paths.Add(path);
                            path.PathNumber = group.Key;
                            path.NodesOnPath = ordered.ToList();
                        }
                        #endregion

                        #region Populate path with sheet data
                        foreach (Path path in paths) path.PopulateSheets();
                        #endregion

                        #region Replace path references with sheet numbers
                        //Assume only one sheet per path -> splitting not implemented yet!
                        foreach (Path path in paths)
                        {
                            foreach (ExcelSheet sheet in path.Sheets)
                            {
                                for (int i = 0; i < sheet.Adresser.Count; i++)
                                {
                                    string current = sheet.Adresser[i];
                                    if (current.Contains(PRef))
                                    {
                                        current = current.Replace(PRef, "");
                                        int pathRef = Convert.ToInt32(current);

                                        Path refPath = paths
                                            .Where(x => x.PathNumber == pathRef)
                                            .FirstOrDefault();

                                        ExcelSheet refSheet = refPath.Sheets.Last();

                                        sheet.Adresser[i] = refSheet.SheetNumber.ToString();
                                    }
                                }
                            }
                        }
                        #endregion

                        #region Populate excel file
                        List<ExcelSheet> sheets = new List<ExcelSheet>();
                        foreach (Path path in paths) sheets.AddRange(path.Sheets);
                        var orderedSheets = sheets.OrderBy(x => x.SheetNumber);

                        foreach (ExcelSheet sheet in orderedSheets)
                        {
                            prdDbg($"Writing sheet: {sheet.SheetNumber}");
                            System.Windows.Forms.Application.DoEvents();

                            ws = (Worksheet)wb.Worksheets[sheet.SheetNumber.ToString()];
                            //Write addresses
                            int row = 60; int col = 5;
                            for (int i = 0; i < sheet.Adresser.Count; i++)
                            {
                                ws.Cells[row, col] = sheet.Adresser[i];
                                row++;
                            }
                            row = 60; col = 17;
                            for (int i = 0; i < sheet.Længder.Count; i++)
                            {
                                ws.Cells[row, col] = sheet.Længder[i];
                                row++;
                            }
                        }
                        #endregion

                        #region Write graph documentation
                        int subGraphNr = paths.Select(x => x.NodesOnPath.First().GroupNumber).First();
                        sb.AppendLine(
                            $"subgraph G_{subGraphNr} {{");
                        sb.AppendLine("node [shape=record]");

                        foreach (Path path in paths)
                        {
                            string strækninger = string.Join(
                                "|", path.NodesOnPath.Select(x => x.Name).ToArray());

                            string sheetNames = string.Join(
                                "|", path.Sheets.Select(x => x.SheetNumber).ToArray());

                            Regex regex = new Regex(@"^\d{1,2}");
                            foreach (ExcelSheet sheet in path.Sheets)
                            {
                                foreach (string s in sheet.Adresser)
                                {
                                    if (regex.IsMatch(s)) sb.AppendLine(
                                        $"\"{sheetNames}\" -> \"{s}\"");
                                }
                            }

                            sb.AppendLine(
                                $"\"{sheetNames}\" " +
                                $"[label = \"{{{sheetNames}|Pnr.: {path.PathNumber}|{strækninger}}}\"]; ");
                        }

                        //close subgraph
                        sb.AppendLine("}");
                        #endregion
                    }

                    //close graph
                    sb.AppendLine("}");

                    #region Write graph to file
                    //Build file name
                    if (!Directory.Exists(@"C:\Temp\"))
                        Directory.CreateDirectory(@"C:\Temp\");

                    //Write the collected graphs to one file
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\AutoDimGraph.dot"))
                    {
                        file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
                    }

                    System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    cmd.StartInfo.Arguments = @"/c ""dot -Tpdf AutoDimGraph.dot > AutoDimGraph.pdf""";
                    cmd.Start();
                    #endregion

                    wb.Save();
                    wb.Close();

                    System.Windows.Forms.Application.DoEvents();
                    #endregion
                }
                #region Catch and finally
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                tx.Commit();
                #endregion
            }
        }
    }
    internal class Path
    {
        internal int PathNumber { get; set; } = 0;
        /// <summary>
        /// Order is important. Nodes must be ordered from max level to min level (descending).
        /// </summary>
        internal List<ExcelNode> NodesOnPath { get; set; }
        internal List<ExcelSheet> Sheets { get; } = new List<ExcelSheet>();
        internal void PopulateSheets()
        {
            string pRef = Dimensionering.PRef;
            List<string> adresser = new List<string>();
            List<double> distancer = new List<double>();

            //Write data
            //Assume nodes in list sorted descending (max to min) by node level
            foreach (ExcelNode node in NodesOnPath)
            {
                List<BlockReference> sortedClients = null;
                if (node.ClientChildren.Count > 0)
                {
                    sortedClients = node.ClientChildren
                        .OrderByDescending(x => node.Self
                        .GetDistAtPoint(node.Self.GetClosestPointTo(x.Position, false))).ToList();
                }

                //Debug
                if (node.Name == "Strækning 1.4")
                {
                    prdDbg("Strækning 1.4 has following children: ");
                    foreach (var item in node.ConnectionChildren)
                    {
                        prdDbg(item.Name);
                    }
                }

                //First write children connections to nodes NOT on path
                //Leaf nodes have no children connections
                if (node.ConnectionChildren.Count() > 0)
                {
                    var foreignChildren = node.ConnectionChildren
                        .Where(x => x.PathId != this.PathNumber).ToList();

                    //For loop is to account for possibility of two or more children at a node
                    for (int i = 0; i < foreignChildren.Count; i++)
                    {
                        adresser.Add($"{pRef}{foreignChildren[i].PathId}");

                        //On last iteration
                        if (i == foreignChildren.Count - 1)
                        {
                            //two cases: client children present or not
                            if (sortedClients == null)
                            {
                                distancer.Add(node.Self.Length);
                            }
                            else
                            {
                                distancer.Add(
                                    node.Self.EndPoint.DistanceHorizontalTo(
                                        node.Self.GetClosestPointTo(
                                            sortedClients.First().Position, false)));
                            }
                        }
                        else distancer.Add(0.0);
                    }
                }

                //Then write client nodes if any
                if (sortedClients != null)
                {
                    if (sortedClients.Count > 1)
                    {
                        for (int i = 0; i < sortedClients.Count - 1; i++)
                        {
                            BlockReference br = sortedClients[i];
                            string vejnavn = PropertySetManager
                                .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                            string husnummer = PropertySetManager
                                .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                            adresser.Add($"{vejnavn} {husnummer}");

                            double currentDist = node.Self.GetDistAtPoint(
                                node.Self.GetClosestPointTo(br.Position, false));

                            double previousDist = node.Self.GetDistAtPoint(
                                node.Self.GetClosestPointTo(sortedClients[i + 1].Position, false));
                            distancer.Add(currentDist - previousDist);

                            //Handle the last case
                            if (i == sortedClients.Count - 2)
                            {
                                br = sortedClients[i + 1];
                                vejnavn = PropertySetManager
                                .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                                husnummer = PropertySetManager
                                    .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                                adresser.Add($"{vejnavn} {husnummer}");

                                currentDist = node.Self.GetDistAtPoint(
                                    node.Self.GetClosestPointTo(br.Position, false));

                                distancer.Add(currentDist);
                            }
                        }
                    }
                    else
                    {
                        BlockReference br = sortedClients.First();
                        string vejnavn = PropertySetManager
                            .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                        string husnummer = PropertySetManager
                            .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                        adresser.Add($"{vejnavn} {husnummer}");

                        double currentDist = node.Self.GetDistAtPoint(
                            node.Self.GetClosestPointTo(br.Position, false));

                        distancer.Add(currentDist);
                    }
                }
            }

            //Split sheets and write them
            while (adresser.Count > 50)
            {
                //When implementing splitting logic
                //Remember to take into account when replacing path references
                throw new System.NotImplementedException("Splitting of sheets not implemented yet!");
            }

            ExcelSheet excelSheet = new ExcelSheet();
            excelSheet.SheetNumber = Dimensionering.GlobalSheetCount.GetNextNumber();
            excelSheet.Adresser = adresser;
            excelSheet.Længder = distancer;
            this.Sheets.Add(excelSheet);
        }
    }
    internal class ExcelSheet
    {
        internal int SheetNumber { get; set; } = 0;
        internal List<string> Adresser { get; set; } = new List<string>();
        internal List<double> Længder { get; set; } = new List<double>();
    }
    internal class ExcelNode
    {
        internal int GroupNumber { get; set; } = 0;
        internal int PartNumber { get; set; } = 0;
        internal string Name { get => $"Strækning {GroupNumber}.{PartNumber}"; }
        internal int NodeLevel { get; set; } = 0;
        internal int PathId { get; set; } = 0;
        internal ExcelNode Parent { get; set; }
        internal Polyline Self { get; set; }
        internal List<ExcelNode> ConnectionChildren { get; set; } = new List<ExcelNode>();
        internal HashSet<BlockReference> ClientChildren { get; set; }
            = new HashSet<BlockReference>();
        internal void SetGroupAndPartNumbers(string input)
        {
            input = input.Replace("Strækning ", "");
            GroupNumber = Convert.ToInt32(input.Split('.')[0]);
            PartNumber = Convert.ToInt32(input.Split('.')[1]);
        }
        public override bool Equals(object a)
        {
            if (ReferenceEquals(null, a)) return false;
            ExcelNode excelNode = a as ExcelNode;
            return excelNode.GroupNumber == this.GroupNumber && excelNode.PartNumber == this.PartNumber;
        }
        public override int GetHashCode()
        {
            return this.Name.GetHashCode() ^ this.GroupNumber.GetHashCode() ^ this.PartNumber.GetHashCode();
        }
    }
    internal class GlobalSheetCount
    {
        internal int CurrentSheetNumber { get; private set; } = 0;
        internal int GetNextNumber() { CurrentSheetNumber++; return CurrentSheetNumber; }
    }
    internal class Node
    {
        internal int GroupNumber { get; set; }
        internal int PartNumber { get; set; }
        internal Node Parent { get; set; }
        internal Polyline Self { get; set; }
        internal HashSet<Node> ConnectionChildren { get; set; } = new HashSet<Node>();
        internal HashSet<BlockReference> ClientChildren { get; set; }
            = new HashSet<BlockReference>();
        internal void SetGroupAndPartNumbers(string input)
        {
            input = input.Replace("Strækning ", "");
            GroupNumber = Convert.ToInt32(input.Split('.')[0]);
            PartNumber = Convert.ToInt32(input.Split('.')[1]);
        }
    }
    /// <summary>
    /// Creates subgraphs from blockreferenes
    /// </summary>
    internal class Subgraph
    {
        private Database Database { get; }
        internal string StrækningsNummer { get; }
        internal Polyline Parent { get; }
        internal HashSet<Handle> Nodes { get; } = new HashSet<Handle>();
        internal Subgraph(Database database, Polyline parent, string strækningsNummer)
        {
            Database = database; Parent = parent; StrækningsNummer = strækningsNummer;
        }

        internal string WriteSubgraph(int subgraphIndex, bool subGraphsOn = true)
        {
            StringBuilder sb = new StringBuilder();
            if (subGraphsOn) sb.AppendLine($"subgraph cluster_{subgraphIndex} {{");
            foreach (Handle handle in Nodes)
            {
                //Gather information about element
                DBObject obj = handle.Go<DBObject>(Database);
                if (obj == null) continue;
                //Write the reference to the node
                sb.Append($"\"{handle}\" ");

                switch (obj)
                {
                    case Polyline pline:
                        //int dn = PipeSchedule.GetPipeDN(pline);
                        //string system = GetPipeSystem(pline);
                        //sb.AppendLine($"[label=\"{{{handle}|Rør L{pline.Length.ToString("0.##")}}}|{system}\\n{dn}\"];");
                        break;
                    case BlockReference br:
                        string vejnavn = PropertySetManager
                            .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                        string husnummer = PropertySetManager
                            .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                        Point3d np = Parent.GetClosestPointTo(br.Position, false);
                        double st = Parent.GetDistAtPoint(np);
                        sb.AppendLine(
                            $"[label=\"{{{handle}|{vejnavn} {husnummer}}}|{st.ToString("0.00")}\"];");
                        break;
                    default:
                        continue;
                }
            }
            //sb.AppendLine(string.Join(" ", Nodes) + ";");
            if (subGraphsOn)
            {
                sb.AppendLine($"label = \"{StrækningsNummer}\";");
                sb.AppendLine("color=red;");
                //if (isEntryPoint) sb.AppendLine("penwidth=2.5;");
                sb.AppendLine("}");
            }
            return sb.ToString();
        }
    }
    internal class Strækning
    {
        internal int GroupNumber { get; set; }
        internal int PartNumber { get; set; }
        internal Entity Parent { get; set; }
        internal Polyline Self { get; set; }
        internal HashSet<Polyline> ConnectionChildren { get; set; } = new HashSet<Polyline>();
        internal HashSet<BlockReference> ClientChildren { get; set; }
            = new HashSet<BlockReference>();
        internal List<string> Data { get; } = new List<string>();
        internal List<string> Distances { get; } = new List<string>();
        /// <summary>
        /// Do any renumbering BEFORE populating data
        /// </summary>
        internal void PopulateData()
        {
            //Write name
            Data.Add($"Strækning {GroupNumber}.{PartNumber}");
            Distances.Add("Distancer");
            //Write connections
            foreach (Polyline pline in ConnectionChildren)
            {
                string strStr = PropertySetManager.ReadNonDefinedPropertySetString(
                    pline, "FJV_fremtid", "Bemærkninger").Replace("Strækning ", "");
                Data.Add($"-> {strStr}");
                //Pad distances at the same time
                Distances.Add($"{Self.GetDistanceAtParameter(Self.EndParam).ToString("0.00")}");
            }

            //Write kundedata
            var sortedClients = ClientChildren
                .OrderByDescending(x => Self.GetDistAtPoint(
                    Self.GetClosestPointTo(x.Position, false))).ToList();

            for (int i = 0; i < sortedClients.Count - 1; i++)
            {
                BlockReference br = sortedClients[i];
                string vejnavn = PropertySetManager
                    .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                string husnummer = PropertySetManager
                    .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                Data.Add($"{vejnavn} {husnummer}");

                double currentDist = Self.GetDistAtPoint(
                    Self.GetClosestPointTo(br.Position, false));

                double previousDist = Self.GetDistAtPoint(
                    Self.GetClosestPointTo(sortedClients[i + 1].Position, false));
                Distances.Add($"{(currentDist - previousDist).ToString("0.00")}");

                //Handle the last case
                if (i == sortedClients.Count - 2)
                {
                    br = sortedClients[i + 1];
                    vejnavn = PropertySetManager
                    .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                    husnummer = PropertySetManager
                        .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                    Data.Add($"{vejnavn} {husnummer}");

                    currentDist = Self.GetDistAtPoint(
                        Self.GetClosestPointTo(br.Position, false));

                    Distances.Add($"{(currentDist).ToString("0.00")}");
                }
            }

            //Write parent connection
            if (Parent != default)
            {
                string strStr2 = PropertySetManager.ReadNonDefinedPropertySetString(
                            Parent, "FJV_fremtid", "Bemærkninger").Replace("Strækning ", "");
                Data.Add($"{strStr2} ->");
                //Pad distances at the same time
                Distances.Add("0");
            }
        }
        internal void PadLists(int maxSize)
        {
            int missing = maxSize - Data.Count;
            if (missing > 0)
            {
                for (int i = 0; i < missing; i++)
                {
                    Data.Add("");
                    Distances.Add("");
                }
            }
        }
    }
}
