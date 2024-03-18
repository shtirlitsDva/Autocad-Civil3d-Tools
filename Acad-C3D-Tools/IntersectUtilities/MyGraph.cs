using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Autodesk.Aec.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
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
using Autodesk.Gis.Map.Constants;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.ComponentSchedule;

using AcRx = Autodesk.AutoCAD.Runtime;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.DynamicBlocks.PropertyReader;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using ObjectId = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using ErrorStatus = Autodesk.AutoCAD.Runtime.ErrorStatus;
using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities
{
    public partial class Graph
    {
        private static Regex regex = new Regex(@"(?<OwnEndType>\d):(?<ConEndType>\d):(?<Handle>\w*);");

        public HashSet<POI> POIs = new HashSet<POI>();
        public static PSetDefs.DriGraph DriGraph { get; } = new PSetDefs.DriGraph();
        public static PropertySetManager PSM { get; set; }
        Database dB { get; }
        private HashSet<Polyline> allPipes;
        private System.Data.DataTable ComponentTable { get; }
        public Graph(Database database, PropertySetManager psm, System.Data.DataTable componentTable)
        {
            PSM = psm;
            ComponentTable = componentTable;
            dB = database;
            allPipes = dB.GetFjvPipes(dB.TransactionManager.TopTransaction);
        }
        public class POI
        {
            public Entity Owner { get; }
            public Point2d Point { get; }
            public EndType EndType { get; }
            private PropertySetManager PSM { get; }
            private PSetDefs.DriGraph DriGraph { get; }
            public POI(Entity owner, Point2d point, EndType endType, PropertySetManager psm, PSetDefs.DriGraph driGraph)
            { Owner = owner; Point = point; EndType = endType; PSM = psm; DriGraph = driGraph; }
            public bool IsSameOwner(POI toCompare) => Owner.Id == toCompare.Owner.Id;
            internal void AddReference(POI connectedEntity)
            {
                string value = PSM.ReadPropertyString(Owner, DriGraph.ConnectedEntities);

                //Avoid duplicate connections on con strings
                //Or filter the connections by their type
                //if (regex.IsMatch(value))
                //{
                //    var matches = regex.Matches(value);
                //    foreach (Match match in matches)
                //        if (match.Groups["Handle"].Value == connectedEntity.Owner.Handle.ToString())
                //            //Do not add a reference if it already exists in the connection string
                //            return;
                //}

                value += $"{(int)EndType}:{(int)connectedEntity.EndType}:{connectedEntity.Owner.Handle};";
                PSM.WritePropertyString(Owner, DriGraph.ConnectedEntities, value);
            }
        }
        public void AddEntityToPOIs(Entity ent)
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
                                POIs.Add(new POI(parent, parent.GetClosestPointTo(pt, false).To2D(), EndType.StikAfgrening, PSM, DriGraph));
                            }

                            pt = pline.EndPoint;
                            if (query.FirstOrDefault() != default)
                            {
                                //This shouldn't happen now, because AssignPlinesAndBlocksToAlignments
                                //guarantees that the end point is never on a supply pipe
                                Polyline parent = query.FirstOrDefault();
                                POIs.Add(new POI(parent, parent.GetClosestPointTo(pt, false).To2D(), EndType.StikAfgrening, PSM, DriGraph));
                            }
                            #endregion

                            //Tilføj almindelige ender til POIs
                            POIs.Add(new POI(pline, pline.StartPoint.To2D(), EndType.StikStart, PSM, DriGraph));
                            POIs.Add(new POI(pline, pline.EndPoint.To2D(), EndType.StikEnd, PSM, DriGraph));
                            break;
                        default:
                            POIs.Add(new POI(pline, pline.StartPoint.To2D(), EndType.Start, PSM, DriGraph));
                            POIs.Add(new POI(pline, pline.EndPoint.To2D(), EndType.End, PSM, DriGraph));
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
                                    new PropertySetManager(dB, PSetDefs.DefinedSets.DriPipelineData);
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
                            if (br.RealName() == "AFGRSTUDS" || br.RealName() == "SH LIGE" || br.RealName() == "SH VINKLET")
                            {
                                PropertySetManager psmPipeline =
                                    new PropertySetManager(dB, PSetDefs.DefinedSets.DriPipelineData);
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
                                        POIs.Add(new POI(polyline, nearest.To2D(), EndType.WeldOn, PSM, DriGraph));
                                        break;
                                    }
                                }
                            }
                        }
                        POIs.Add(new POI(br, wPt.To2D(), endType, PSM, DriGraph));
                    }
                    break;
                default:
                    throw new System.Exception("Wrong type of object supplied!");
            }
        }

        public Dictionary<string, bool> allowedCombinations =
            new Dictionary<string, bool>()
            {
                { "Start-Start", false },
                { "Start-End", false },
                { "Start-Main", true },
                { "Start-Branch", true },
                { "Start-StikAfgrening", false },
                { "Start-StikStart", false },
                { "Start-StikEnd", false },
                { "End-Start", true },
                { "End-End", true },
                { "End-Main", true },
                { "End-Branch", true },
                { "End-StikAfgrening", false },
                { "End-StikStart", true },
                { "End-StikEnd", false },
                { "Main-Start", true },
                { "Main-End", true },
                { "Main-Main", true },
                { "Main-Branch", true },
                { "Main-StikAfgrening", false },
                { "Main-StikStart", true },
                { "Main-StikEnd", false },
                { "Branch-Start", true },
                { "Branch-End", true },
                { "Branch-Main", true },
                { "Branch-Branch", true },
                { "Branch-StikAfgrening", false },
                { "Branch-StikStart", true },
                { "Branch-StikEnd", true },
                { "StikAfgrening-Start", false },
                { "StikAfgrening-End", false },
                { "StikAfgrening-Main", true },
                { "StikAfgrening-Branch", false },
                { "StikAfgrening-StikAfgrening", false },
                { "StikAfgrening-StikStart", true },
                { "StikAfgrening-StikEnd", false },
                { "StikStart-Start", false },
                { "StikStart-End", false },
                { "StikStart-Main", false },
                { "StikStart-Branch", false },
                { "StikStart-StikAfgrening", false },
                { "StikStart-StikStart", false },
                { "StikStart-StikEnd", false },
                { "StikEnd-Start", false },
                { "StikEnd-End", false },
                { "StikEnd-Main", false },
                { "StikEnd-Branch", false },
                { "StikEnd-StikAfgrening", false },
                { "StikEnd-StikStart", true },
                { "StikEnd-StikEnd", false },
                { "WeldOn-Main", true },
                { "Main-WeldOn", true }
            };
        public class GraphEntity
        {
            public Entity Owner { get; }
            public Handle OwnerHandle { get; }
            public Con[] Cons { get; }
            private System.Data.DataTable componentTable;
            public GraphEntity(Entity entity, System.Data.DataTable ComponentTable,
                               PropertySetManager psm, PSetDefs.DriGraph driGraph)
            {
                Owner = entity;
                OwnerHandle = Owner.Handle;
                componentTable = ComponentTable;
                string conString = psm.ReadPropertyString(entity, driGraph.ConnectedEntities);
                if (conString.IsNoE()) throw new System.Exception($"Malformend constring: {conString}, entity: {Owner.Handle}.");
                Cons = parseConString(conString);
            }
            public int LargestDn()
            {
                switch (Owner)
                {
                    case Polyline pline:
                        return GetPipeDN(pline);
                    case BlockReference br:
                        return Convert.ToInt32(ReadComponentDN1Str(br, componentTable));
                }
                return 0;
            }

            internal static Con[] parseConString(string conString)
            {
                Con[] cons;
                if (regex.IsMatch(conString))
                {
                    var matches = regex.Matches(conString);
                    cons = new Con[matches.Count];
                    int i = 0;
                    foreach (Match match in matches)
                    {
                        string ownEndTypeString = match.Groups["OwnEndType"].Value;
                        string conEndTypeString = match.Groups["ConEndType"].Value;
                        string handleString = match.Groups["Handle"].Value;
                        cons[i] = new Con(ownEndTypeString, conEndTypeString, handleString);
                        i++;
                    }
                }
                else
                {
                    throw new System.Exception($"Malforfmed string: {conString}!");
                }

                return cons;
            }
        }
        public class Con
        {
            public EndType OwnEndType { get; }
            public EndType ConEndType { get; }
            public Handle ConHandle { get; }
            public Handle OwnHandle { get; set; }
            public Con(string ownEndType, string conEndType, string handle)
            {
                int ownEndTypeInt = Convert.ToInt32(ownEndType);
                OwnEndType = (EndType)ownEndTypeInt;
                int conEndTypeInt = Convert.ToInt32(conEndType);
                ConEndType = (EndType)conEndTypeInt;
                ConHandle = new Handle(Convert.ToInt64(handle, 16));
            }
        }
        private HashSet<GraphEntity> GraphEntities { get; set; } = new HashSet<GraphEntity>();
        public void AddEntityToGraphEntities(Entity entity)
        {
            GraphEntities.Add(new GraphEntity(entity, ComponentTable, PSM, DriGraph));
        }
        public void CreateAndWriteGraph()
        {
            //Instantiate property set manager necessary to gather alignment names
            PropertySetManager psmPipeline = new PropertySetManager(
                Application.DocumentManager.MdiActiveDocument.Database,
                PSetDefs.DefinedSets.DriPipelineData);
            PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();

            //Counter to count all disjoined graphs
            int graphCount = 0;
            //Flag to signal the entry point subgraph
            bool isEntryPoint = false;
            //Stringbuilder to collect all disjoined graphs
            StringBuilder sbAll = new StringBuilder();
            sbAll.AppendLine("digraph G {");

            while (GraphEntities.Count > 0)
            {
                //Increment graph counter
                graphCount++;

                //Collection to keep track of visited nodes
                //To prevent looping for ever
                //And to be able to handle disjoined piping networks
                HashSet<Handle> visitedHandles = new HashSet<Handle>();

                //Determine starting entity
                //Criteria: Only one child -> means an end node AND largest DN of all not visited
                //Currently only for one network
                //Disjoined networks are not handled yet

                //Warning: this won't work if there's a connection to first pipe in system
                //Warning: afgreningsstuds or stik pipe
                //2022.09.08: Attempt to fix this by adding a new EndType: WeldOn
                GraphEntity ge = GraphEntities
                    .Where(x =>
                    //Exclude WeldOn and StikAfgrening EndTypes from count
                        (x.Cons.Count(y => y.OwnEndType != EndType.StikAfgrening && y.OwnEndType != EndType.WeldOn) == 1) ||
                        (x.Cons.Count(y => y.OwnEndType != EndType.WeldOn) == 0 && x.Cons.Count(y => y.OwnEndType == EndType.WeldOn) > 0
                            ))
                    .MaxBy(x => x.LargestDn()).FirstOrDefault();

                if (ge == null)
                {
                    //throw new System.Exception("No entity found!");
                    prdDbg("ERROR: Graph not complete!!!");
                    prdDbg(
                        "Check if entry pipe has a connection (afgreningsstuds or stik.)" +
                        "\nThis could prevent it from registering as entry element.");
                    foreach (var item in GraphEntities)
                    {
                        Entity owner = item.Owner;
                        Line line = new Line();
                        line.Layer = "0";
                        line.StartPoint = new Point3d();
                        switch (owner)
                        {
                            case Polyline pline:
                                line.EndPoint = pline.GetPointAtDist(pline.Length / 2);
                                break;
                            case BlockReference br:
                                line.EndPoint = br.Position;
                                break;
                            default:
                                break;
                        }
                        line.AddEntityToDbModelSpace(Application.DocumentManager.MdiActiveDocument.Database);
                    }
                    prdDbg("Created lines that mark faulty entities in the drawing!");
                    break;
                }
                prdDbg("Entry: " + ge.OwnerHandle.ToString());

#if DEBUG
                //{
                //    Entity owner = ge.Owner;
                //    Line line = new Line();
                //    line.Layer = "0";
                //    line.StartPoint = new Point3d();
                //    switch (owner)
                //    {
                //        case Polyline pline:
                //            line.EndPoint = pline.GetPointAtDist(pline.Length / 2);
                //            break;
                //        case BlockReference br:
                //            line.EndPoint = br.Position;
                //            break;
                //        default:
                //            break;
                //    }
                //    line.AddEntityToDbModelSpace(Application.DocumentManager.MdiActiveDocument.Database);
                //}   
#endif

                //Flag the entry point subgraph
                isEntryPoint = true;

                //Variable to cache previous handle to avoid backreferences
                Handle previousHandle = default;

                //Collection to collect the edges
                HashSet<Edge> edges = new HashSet<Edge>();

                //Collection to collect the subgraphs
                Dictionary<string, Subgraph> subgraphs = new Dictionary<string, Subgraph>();

                //Using a stack traversing strategy
                Stack<GraphEntity> stack = new Stack<GraphEntity>();
                //Put the first element on to the stack manually
                stack.Push(ge);
                //Iterate the stack until no connected nodes left
                while (stack.Count > 0)
                {
                    //Fetch the topmost entity on stack
                    GraphEntity current = stack.Pop();

                    //Determine the subgraph it is part of
                    string alName = psmPipeline.ReadPropertyString(current.Owner, driPipelineData.BelongsToAlignment);
                    //Fetch or create new subgraph object
                    Subgraph subgraph;
                    if (subgraphs.ContainsKey(alName)) subgraph = subgraphs[alName];
                    else
                    {
                        subgraph = new Subgraph(dB, ComponentTable, alName);
                        subgraphs.Add(alName, subgraph);
                    }
                    subgraph.Nodes.Add(current.OwnerHandle);

                    if (isEntryPoint)
                    {
                        subgraph.isEntryPoint = isEntryPoint;
                        isEntryPoint = false;
                    }

                    //Iterate over current node's children
                    foreach (Con con in current.Cons)
                    {
                        //Find the child the con is referencing to
                        GraphEntity child = GraphEntities.Where(x => x.OwnerHandle == con.ConHandle).FirstOrDefault();
                        //if it is the con refering back to the parent -> skip it
                        if (child == default || child.OwnerHandle == current.OwnerHandle) continue;
                        //Also skip if child has already been visited
                        //This prevents from making circular graphs I think
                        //Comment next line out to test circular graphs
                        //if (visitedHandles.Contains(child.OwnerHandle)) continue; <-- First solution
                        //Solution with caching of previous handle, it I don't think it works when backtracking to a branch -> there will be a double arrow
                        if (previousHandle != null && previousHandle == child.OwnerHandle) continue;
                        //Try to control which cons get written by their type
                        //Build string
                        string ownEnd = con.OwnEndType.ToString();
                        string conEnd = con.ConEndType.ToString();
                        string key = ownEnd + "-" + conEnd;
                        //if (key == "StikAfgrening-Main") prdDbg(key);
                        if (allowedCombinations.ContainsKey(key) && !allowedCombinations[key]) continue;
                        //if (key == "StikAfgrening-Main") prdDbg("Passed!");

                        //Tries to prevent duplicate Main-Main edges by eliminating upstream Main-Main instance
                        //Doesn't work if recursion just returned from a branch, because previous is set the the
                        //Last node on the branch
                        if (key == "Main-Main" && con.ConHandle == previousHandle) continue;

                        //Record the edge between nodes
                        edges.Add(new Edge(
                            current.OwnerHandle, con.OwnEndType,
                            child.OwnerHandle, con.ConEndType
                            ));
                        //edges.Add(new Edge(current.OwnerHandle, child.OwnerHandle, key));
                        //If this child node is in visited collection -> skip, so we don't ger circular referencing
                        if (visitedHandles.Contains(child.OwnerHandle)) continue;
                        //If the node has not been visited yet, then put it on the stack
                        stack.Push(child);
                    }
                    //When current iteration completes, put the current node handle in the visited collection
                    visitedHandles.Add(current.OwnerHandle);
                    //Cache current node handle to avoid backreference
                    previousHandle = current.OwnerHandle;
                }

                //Write collected data
                //Stringbuilder to contain the overall file
                //This must be refactored when working with disjoined networks
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"subgraph G_{graphCount} {{"); //First line of file stating a graph
                                                              //Set the shape of the nodes for whole graph
                sb.AppendLine("node [shape=record];");

                //Write edges
                foreach (Edge edge in edges)
                {
                    QA.QualityAssurance(edge, dB, ComponentTable);
                    sb.AppendLine(edge.ToString("->"));
                }

                //Write subgraphs
                int i = 0;
                foreach (var sg in subgraphs)
                {
                    i++;
                    sb.Append(sg.Value.WriteSubgraph(i));
                }

                //Add closing curly brace correspoinding to the first line
                sb.AppendLine("}");
                //Append current disjoined graph to all collector
                sbAll.Append(sb.ToString());

                //using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\MyGraph_{graphCount}.dot"))
                //{
                //    file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
                //}

                //Modify the GraphEntities to remove visited entities
                GraphEntities = GraphEntities.ExceptWhere(x => visitedHandles.Contains(x.OwnerHandle)).ToHashSet();
            }

            //Closing brace of the main graph
            sbAll.AppendLine("}");

            //Check or create directory
            if (!Directory.Exists(@"C:\Temp\"))
                Directory.CreateDirectory(@"C:\Temp\");

            //Write the collected graphs to one file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\MyGraph.dot"))
            {
                file.WriteLine(sbAll.ToString()); // "sb" is the StringBuilder
            }
        }
        internal class Subgraph
        {
            private Database Database { get; }
            private System.Data.DataTable Table { get; }
            internal string Alignment { get; }
            internal bool isEntryPoint { get; set; } = false;
            internal HashSet<Handle> Nodes { get; } = new HashSet<Handle>();
            internal Subgraph(Database database, System.Data.DataTable table, string alignment)
            { Alignment = alignment; Database = database; Table = table; }
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
                            int dn = GetPipeDN(pline);
                            string system = GetPipeType(pline).ToString();
                            var psys = GetPipeSystem(pline).ToString();
                            sb.AppendLine($"[label=\"{{{handle}|Rør L{pline.Length.ToString("0.##")}}}|{psys} {system}\\n{dn}\"];");
                            break;
                        case BlockReference br:
                            string dn1 = ReadComponentDN1Str(br, Table);
                            string dn2 = ReadComponentDN2Str(br, Table);
                            string dnStr = dn2 == "0" ? dn1 : dn1 + "/" + dn2;
                            system = ComponentSchedule.ReadComponentSystem(br, Table);
                            string type = ComponentSchedule.ReadDynamicCsvProperty(br, DynamicProperty.Type);
                            string color = "";
                            if (type == "Reduktion") color = "color=\"red\"";
                            sb.AppendLine($"[label=\"{{{handle}|{type}}}|{system}\\n{dnStr}\"{color}];");
                            break;
                        default:
                            continue;
                    }
                }
                //sb.AppendLine(string.Join(" ", Nodes) + ";");
                if (subGraphsOn)
                {
                    sb.AppendLine($"label = \"{Alignment}\";");
                    sb.AppendLine("color=red;");
                    if (isEntryPoint) sb.AppendLine("penwidth=2.5;");
                    sb.AppendLine("}");
                }
                return sb.ToString();
            }
        }
        internal class Edge
        {
            internal Handle Id1 { get; }
            internal EndType EndType1 { get; }
            internal Handle Id2 { get; }
            internal EndType EndType2 { get; }
            internal string Label { get; set; }
            internal Edge(Handle id1, Handle id2)
            {
                Id1 = id1; Id2 = id2;
            }
            internal Edge(
                Handle id1, EndType endType1,
                Handle id2, EndType endType2)
            {
                Id1 = id1; Id2 = id2;
                EndType1 = endType1; EndType2 = endType2;
            }
            internal Edge(Handle id1, Handle id2, string label)
            {
                Id1 = id1; Id2 = id2; Label = label;
            }
            internal string ToString(string edgeSymbol)
            {
                if (Label.IsNoE()) return $"\"{Id1}\" {edgeSymbol} \"{Id2}\"";
                else return $"\"{Id1}\" {edgeSymbol} \"{Id2}\"{Label}";
            }
        }
    }
}
