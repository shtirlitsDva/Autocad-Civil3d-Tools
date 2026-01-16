using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
//using MoreLinq;
using System.Text;

using static IntersectUtilities.ComponentSchedule;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities.GraphWrite
{
    public partial class Graph
    {
        public HashSet<POI> POIs = new HashSet<POI>();
        public static PSetDefs.DriGraph DriGraph { get; } = new PSetDefs.DriGraph();
        public static PropertySetManager PSM { get; set; }
        Database dB { get; }
        private HashSet<Polyline> allPipes;
        private FjvDynamicComponents ComponentTable { get; }
        public Graph(Database database, PropertySetManager psm, FjvDynamicComponents componentTable)
        {
            PSM = psm;
            ComponentTable = componentTable;
            dB = database;
            allPipes = dB.GetFjvPipes(dB.TransactionManager.TopTransaction);
        }
        public void AddEntityToPOIs(Entity ent)
        {
            PropertySetManager psmPipeline = 
                new PropertySetManager(dB, PSetDefs.DefinedSets.DriPipelineData);
            PSetDefs.DriPipelineData driPipelineData = new PSetDefs.DriPipelineData();

            switch (ent)
            {
                case Polyline pline:
                    switch (GetPipeSystem(pline))
                    {
                        case PipeSystemEnum.Ukendt:
                            prdDbg($"Wrong type of pline supplied: {pline.Handle}");
                            throw new System.Exception("Supplied a new PipeSystemEnum! Add to code kthxbai.");                        
                        default:
                            POIs.Add(new POI(pline, pline.StartPoint.To2d(), EndType.Start, PSM));
                            POIs.Add(new POI(pline, pline.EndPoint.To2d(), EndType.End, PSM));
                            break;

                    }
                    break;
                case BlockReference br:
                    Transaction tx = br.Database.TransactionManager.TopTransaction;
                    BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

                    //Quick and dirty fix for missing data
                    if (br.RealName() == "SH LIGE" || br.RealName() == "SH VINKLET")
                    {
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
                            //Handle special case of AFGRSTUDS, SH LIGE and SH VINKLET
                            //which does not coincide with an end on polyline
                            //but is situated somewhere along the polyline
                            if (br.RealName() == "AFGRSTUDS" || br.RealName() == "SH LIGE" || br.RealName() == "SH VINKLET")
                            {
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
                                        POIs.Add(new POI(polyline, nearest.To2d(), EndType.WeldOn, PSM));
                                        break;
                                    }
                                }
                            }
                        }
                        POIs.Add(new POI(br, wPt.To2d(), endType, PSM));
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
        private HashSet<GraphEntity> GraphEntities { get; set; } = new HashSet<GraphEntity>();
        public void AddEntityToGraphEntities(Entity entity)
        {
            GraphEntities.Add(new GraphEntity(entity, PSM));
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

                //Warning: this won't work if there's a connection to first pipe in system
                //Warning: afgreningsstuds or stik pipe
                //2022.09.08: Attempt to fix this by adding a new EndType: WeldOn
                GraphEntity? ge = GraphEntities
                    .Where(x =>
                    //Exclude WeldOn and StikAfgrening EndTypes from count
                        (x.Cons.Count(y => y.OwnEndType != EndType.StikAfgrening && y.OwnEndType != EndType.WeldOn) == 1) ||
                        (x.Cons.Count(y => y.OwnEndType != EndType.WeldOn) == 0 && x.Cons.Count(y => y.OwnEndType == EndType.WeldOn) > 0
                            ))
                    .MaxBy(x => x.LargestDn());

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
                        GraphEntity? child = GraphEntities.Where(
                            x => x.OwnerHandle == con.ConHandle).FirstOrDefault();
                        //if it is the con refering back to the parent -> skip it
                        if (child == default || child.OwnerHandle == current.OwnerHandle) continue;
                        //Also skip if child has already been visited
                        //This prevents from making circular graphs I think
                        //Comment next line out to test circular graphs
                        //if (visitedHandles.Contains(child.OwnerHandle)) continue; <-- First solution
                        //Solution with caching of previous handle, it I don't think it works when backtracking to a branch -> there will be a double arrow
                        if (previousHandle != default && previousHandle == child.OwnerHandle) continue;
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
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"subgraph G_{graphCount} {{");

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

                //Modify the GraphEntities to remove visited entities
                GraphEntities = GraphEntities.Where(
                    x => !visitedHandles.Contains(x.OwnerHandle)).ToHashSet();
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
    }
}
