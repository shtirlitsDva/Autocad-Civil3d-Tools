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
using csdot;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.PipeSchedule;
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
    public class Graph
    {
        public HashSet<POI> POIs = new HashSet<POI>();
        PSetDefs.DriGraph DriGraph { get; } = new PSetDefs.DriGraph();
        PropertySetManager PSM { get; }
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
                value += $"{(int)EndType}:{(int)connectedEntity.EndType}:{connectedEntity.Owner.Handle};";
                PSM.WritePropertyString(DriGraph.ConnectedEntities, value);
            }
        }
        public void AddEntityToPOIs(Entity ent)
        {
            switch (ent)
            {
                case Polyline pline:
                    POIs.Add(new POI(pline, pline.StartPoint.To2D(), EndType.End, PSM, DriGraph));
                    POIs.Add(new POI(pline, pline.EndPoint.To2D(), EndType.End, PSM, DriGraph));
                    #region STIK
                    if (GetPipeSystem(pline) == PipeSystemEnum.AluPex ||
                        GetPipeSystem(pline) == PipeSystemEnum.Kobberflex)
                    {
                        Point3d pt = pline.StartPoint;
                        var query = allPipes.Where(x => pt.IsOnCurve(x, 0.025) && pline.Handle != x.Handle);
                        
                        if (query.FirstOrDefault() != default)
                        {
                            Polyline parent = query.FirstOrDefault();
                            POIs.Add(new POI(parent, parent.GetClosestPointTo(pt, false).To2D(), EndType.Stik, PSM, DriGraph));
                        }

                        pt = pline.EndPoint;
                        if (query.FirstOrDefault() != default)
                        {
                            Polyline parent = query.FirstOrDefault();
                            POIs.Add(new POI(parent, parent.GetClosestPointTo(pt, false).To2D(), EndType.Stik, PSM, DriGraph));
                        }
                    }
                    #endregion
                    break;
                case BlockReference br:
                    Transaction tx = br.Database.TransactionManager.TopTransaction;
                    BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
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
                            if (br.RealName() == "AFGRSTUDS")
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
                                        POIs.Add(new POI(polyline, nearest.To2D(), EndType.Branch, PSM, DriGraph));
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
        public enum EndType
        {
            None,   //0:
            End,    //1: For ends of pipes
            Main,   //2: For main run in components
            Branch, //3: For branches in components
            Stik    //4: For stik directly connected to piperuns
        }
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
                        return Convert.ToInt32(ReadComponentDN1(br, componentTable));
                }
                return 0;
            }

            private Con[] parseConString(string conString)
            {
                Regex regex = new Regex(@"(?<OwnEndType>\d):(?<ConEndType>\d):(?<Handle>\w*);");

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
                GraphEntity ge = GraphEntities.Where(x => x.Cons.Length == 1).MaxBy(x => x.LargestDn()).FirstOrDefault();
                prdDbg(
                    $"{GraphEntities.Count}"
                    );
                //prdDbg(ge.OwnerHandle.ToString());
                if (ge == null) throw new System.Exception("No entity found!");

                //Flag the entry point subgraph
                isEntryPoint = true;

                //Variable to cache previous handle to avoid backreferences
                Handle previousHandle = default;

                //Collection to collect the edges
                HashSet<Edge> edges = new HashSet<Edge>();

                //Collection to collect the subgraphs
                Dictionary<string, Subgraph> subgraphs = new Dictionary<string, Subgraph>();
                //Instantiate property set manager necessary to gather alignment names
                //It must be done after an element is found to get at the database
                PropertySetManager psmPipeline = new PropertySetManager(ge.Owner.Database, PSetDefs.DefinedSets.DriPipelineData);
                PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();

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
                        if (child.OwnerHandle == current.OwnerHandle) continue;
                        //Also skip if child has already been visited
                        //This prevents from making circular graphs I think
                        //Comment next line out to test circular graphs
                        //if (visitedHandles.Contains(child.OwnerHandle)) continue; <-- First solution
                        //Solution with caching of previous handle, it I don't think it works when backtracking to a branch -> there will be a double arrow
                        if (previousHandle != null && previousHandle == child.OwnerHandle) continue;
                        //Record the edge between nodes
                        edges.Add(new Edge(current.OwnerHandle, child.OwnerHandle));
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
                            int dn = PipeSchedule.GetPipeDN(pline);
                            string system = GetPipeType(pline).ToString();
                            sb.AppendLine($"[label=\"{{{handle}|Rør L{pline.Length.ToString("0.##")}}}|{system}\\n{dn}\"];");
                            break;
                        case BlockReference br:
                            string dn1 = ReadComponentDN1(br, Table);
                            string dn2 = ReadComponentDN2(br, Table);
                            string dnStr = dn2 == "0" ? dn1 : dn1 + "/" + dn2;
                            system = ComponentSchedule.ReadComponentSystem(br, Table);
                            string type = ComponentSchedule.ReadComponentType(br, Table);
                            sb.AppendLine($"[label=\"{{{handle}|{type}}}|{system}\\n{dnStr}\"];");
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
            internal Handle Id2 { get; }
            internal Edge(Handle id1, Handle id2)
            {
                Id1 = id1; Id2 = id2;
            }
            internal string ToString(string edgeSymbol)
            {
                return $"\"{Id1}\" {edgeSymbol} \"{Id2}\"";
            }
        }
    }
}
