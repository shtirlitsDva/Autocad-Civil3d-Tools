#if BRICSCAD
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Application = Bricscad.ApplicationServices.Application;
using BlockReference = Teigha.DatabaseServices.BlockReference;
using Entity = Teigha.DatabaseServices.Entity;
using Oid = Teigha.DatabaseServices.ObjectId;
#else
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
#endif

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

namespace IntersectUtilities.GraphWrite
{
    public partial class Graph
    {
        public HashSet<POI> POIs = new HashSet<POI>();
        public static PSetDefs.DriGraph DriGraph { get; } = new PSetDefs.DriGraph();
        public static IPropertySetAccess PSM { get; set; }
        Database dB { get; }
        private HashSet<Polyline> allPipes;
        private FjvDynamicComponents ComponentTable { get; }
        private readonly IPropertySetAccess psmPipeline;
        public Graph(Database database, PropertySetHelper psh, FjvDynamicComponents componentTable)
        {
            PSM = psh.Graph;
            psmPipeline = psh.Pipeline;
            ComponentTable = componentTable;
            dB = database;
            allPipes = dB.GetFjvPipes(dB.TransactionManager.TopTransaction);
        }
        public void AddEntityToPOIs(Entity ent)
        {
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

                    foreach (ComponentPort port in ComponentPorts.Read(br, tx))
                    {
                        Point3d wPt = port.Position;
                        EndType endType;
                        if (port.Role == ComponentPortRole.Branch) { endType = EndType.Branch; }
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
    }
}
