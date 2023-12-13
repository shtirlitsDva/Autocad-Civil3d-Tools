using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using MoreLinq;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.ComponentSchedule;
using static IntersectUtilities.DynamicBlocks.PropertyReader;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using GroupByCluster;
using QuikGraph;
using QuikGraph.Graphviz;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using System.IO;
using System.Diagnostics;
using QuikGraph.Algorithms.Search;
using QuikGraph.Algorithms;

namespace IntersectUtilities
{
    public class PipelineSizeArray
    {
        public SizeEntry[] SizeArray;
        public BidirectionalGraph<Entity, Edge<Entity>> Graph;
        public int Length { get => SizeArray.Length; }
        public PipelineSizesArrangement Arrangement { get; }
        public bool HasFYModel { get; private set; } = false;
        public int StartingDn { get; }
        public SizeEntry this[int index] { get => SizeArray[index]; }
        public int MaxDn { get => SizeArray.MaxBy(x => x.DN).FirstOrDefault().DN; }
        public int MinDn { get => SizeArray.MinBy(x => x.DN).FirstOrDefault().DN; }
        private System.Data.DataTable dynamicBlocks { get; }
        private static readonly HashSet<string> unwantedTypes = new HashSet<string>()
        {
            "Svejsning",
            "Stikafgrening",
            "Muffetee"
        };
        private static readonly HashSet<string> graphUnwantedTypes = new HashSet<string>()
        {
            "Svejsning",
        };
        private static readonly HashSet<string> directionDefiningTypes = new HashSet<string>()
        {
            "Reduktion"
        };
        private static readonly HashSet<string> systemTransitionTypes = new HashSet<string>()
        {
            "F-Model",
            "Y-Model"
        };
        /// <summary>
        /// SizeArray listing sizes, station ranges and jacket diameters.
        /// Use empty brs collection or omit it to force size table based on curves.
        /// </summary>
        /// <param name="al">Current alignment.</param>
        /// <param name="brs">All transitions belonging to the current alignment.</param>
        /// <param name="curves">All pipline curves belonging to the current alignment.</param>
        public PipelineSizeArray(Alignment al, HashSet<Curve> curves, HashSet<BlockReference> brs = default)
        {
            dynamicBlocks = CsvData.FK;

            #region Create graph
            var entities = new HashSet<Entity>(curves);
            if (brs != default) entities.UnionWith(
                brs.Where(x => !graphUnwantedTypes.Contains(
                    x.ReadDynamicCsvProperty(DynamicProperty.Type, false))));

            HashSet<POI> POIs = new HashSet<POI>();
            foreach (Entity ent in entities) AddEntityToPOIs(ent, POIs);

            IEnumerable<IGrouping<POI, POI>> clusters
                = POIs.GroupByCluster((x, y) => x.Point.GetDistanceTo(y.Point), 0.005);

            foreach (IGrouping<POI, POI> cluster in clusters)
            {
                //Create unique pairs
                var pairs = cluster.SelectMany((value, index) => cluster.Skip(index + 1),
                                               (first, second) => new { first, second });
                //Create reference to each other for each pair
                foreach (var pair in pairs)
                {
                    if (pair.first.Owner.Handle == pair.second.Owner.Handle) continue;
                    pair.first.AddReference(pair.second);
                    pair.second.AddReference(pair.first);
                }
            }

            //First crate a graph that start from a random entity
            var startingGraph = new BidirectionalGraph<Entity, Edge<Entity>>();
            var groups = POIs.GroupBy(x => x.Owner.Handle);

            foreach (var group in groups)
                startingGraph.AddVertex(group.First().Owner);

            foreach (var group in groups)
            {
                Entity owner = group.First().Owner;

                foreach (var poi in group)
                {
                    foreach (var neighbour in poi.Neighbours)
                    {
                        startingGraph.AddEdge(new Edge<Entity>(owner, neighbour));
                    }
                }
            }

            BidirectionalGraph<Entity, Edge<Entity>> sortedGraph = default;
            if (curves.Count + brs.Count != 1) //Handle an edge case of single pipe in alignment
            {
                //Now find the ends, choose one and rearrange graph to start from that node
                var endNodes = startingGraph.Vertices.Where(
                    v => startingGraph.OutDegree(v) == 1 && startingGraph.InDegree(v) == 1);
                var startingNode = endNodes.OrderBy(x => GetStation(al, x)).First();

                var dfs = new DepthFirstSearchAlgorithm<Entity, Edge<Entity>>(startingGraph);
                var verticesInNewOrder = new List<Entity>();
                dfs.FinishVertex += verticesInNewOrder.Add;
                dfs.Compute(startingNode);

                sortedGraph = new BidirectionalGraph<Entity, Edge<Entity>>();
                sortedGraph.AddVertexRange(verticesInNewOrder);

                foreach (var edge in startingGraph.Edges)
                {
                    try
                    {
                        sortedGraph.AddEdge(edge);
                    }
                    catch (Exception ex)
                    {
                        prdDbg($"Failed to add edge: {edge.Source.Handle} -> {edge.Target.Handle}.");
                        if (sortedGraph.Vertices.Any(x => x.Handle == edge.Source.Handle))
                            prdDbg($"Source {edge.Source.Handle} is in SORTED graph.");
                        else prdDbg($"Source {edge.Source.Handle} is NOT in SORTED graph.");
                        if (sortedGraph.Vertices.Any(x => x.Handle == edge.Target.Handle))
                            prdDbg($"Target {edge.Target.Handle} is in SORTED graph.");
                        else prdDbg($"Target {edge.Target.Handle} is NOT in SORTED graph.");
                        if (startingGraph.Vertices.Any(x => x.Handle == edge.Source.Handle))
                            prdDbg($"Source {edge.Source.Handle} is in STARTING graph.");
                        else prdDbg($"Source {edge.Source.Handle} is NOT in STARTING graph.");
                        if (startingGraph.Vertices.Any(x => x.Handle == edge.Target.Handle))
                            prdDbg($"Target {edge.Target.Handle} is in STARTING graph.");
                        else prdDbg($"Target {edge.Target.Handle} is NOT in STARTING graph.");
                        prdDbg(ex);
                        throw;
                    }

                }

                Graph = sortedGraph;
            }
            else Graph = startingGraph;
            #endregion

            #region Direction
            ////Determine pipe size direction
            #region Old direction method
            ////This is a flawed method using only curves, see below
            //int maxDn = GetPipeDN(curves.MaxBy(x => GetPipeDN(x)).FirstOrDefault());
            //int minDn = GetPipeDN(curves.MinBy(x => GetPipeDN(x)).FirstOrDefault());

            //HashSet<(Curve curve, double dist)> curveDistTuples =
            //                new HashSet<(Curve curve, double dist)>();

            //Point3d samplePoint = al.GetPointAtDist(0);

            //foreach (Curve curve in curves)
            //{
            //    if (curve.GetDistanceAtParameter(curve.EndParam) < 0.0001) continue;
            //    Point3d closestPoint = curve.GetClosestPointTo(samplePoint, false);
            //    if (closestPoint != default)
            //        curveDistTuples.Add(
            //            (curve, samplePoint.DistanceHorizontalTo(closestPoint)));
            //}

            //Curve closestCurve = curveDistTuples.MinBy(x => x.dist).FirstOrDefault().curve;

            //StartingDn = GetPipeDN(closestCurve); 
            #endregion

            //2023.04.12: A case discovered where there's a reducer after which there's only blocks
            //till the alignment's end. This confuses the code to think that the last size
            //don't exists, as it looks only at polylines present.
            //So, we need to check for presence of reducers to definitely rule out one size case.
            var reducersOrdered = brs?.Where(
                x => x.ReadDynamicCsvProperty(DynamicProperty.Type, false) == "Reduktion")
                .OrderBy(x => al.StationAtPoint(x))
                .ToArray();

            List<int> dnsAlongAlignment = default;
            if (reducersOrdered != null && reducersOrdered.Count() != 0)
            {
                dnsAlongAlignment = new List<int>();

                for (int i = 0; i < reducersOrdered.Count(); i++)
                {
                    var reducer = reducersOrdered[i];

                    if (i == 0) dnsAlongAlignment.Add(
                        GetDirectionallyCorrectReducerDnWithGraph(al, reducer, Side.Left));

                    dnsAlongAlignment.Add(
                        GetDirectionallyCorrectReducerDnWithGraph(al, reducer, Side.Right));
                }
                Arrangement = DetectArrangement(dnsAlongAlignment);
                prdDbg(string.Join(", ", dnsAlongAlignment) + " -> " + Arrangement);
            }
            else
            {
                Arrangement = PipelineSizesArrangement.OneSize;
                prdDbg(Arrangement);
            }

            if (Arrangement == PipelineSizesArrangement.Unknown)
                throw new System.Exception($"Alignment {al.Name} could not determine pipeline sizes direction!");
            #endregion

            //Check blockreferences version
            if (brs != default && brs.Count != 0)
            {
                var distinctBlocks = brs.DistinctBy(x => x.RealName());
                foreach (var block in distinctBlocks)
                {
                    if (!block.CheckIfBlockIsLatestVersion()) prdDbg(
                        $"WRN: Block {block.RealName()}, is not latest version! " +
                        $"Consider updating to latest version. Program functionality is not guaranteed.");
                }
            }

            //Determine if the pipeline contains F- or Y-model
            //To catch the case where the pipeline has F- or Y-model
            //where we need to construct with blocks and not reducers
            if (brs.Any(x => systemTransitionTypes.Contains(
                x.ReadDynamicCsvProperty(DynamicProperty.Type, false))))
                HasFYModel = true;

            #region Construct Sizes Array
            if (brs == default || brs.Count == 0 || (Arrangement == PipelineSizesArrangement.OneSize && !HasFYModel))
                SizeArray = ConstructWithCurves(al, curves);
            //else SizeArray = ConstructWithBlocks(al, curves, brs, dynamicBlocks);
            else
            {
                brs = brs.Where(x =>
                    IsTransition(x, dynamicBlocks) ||
                    IsXModel(x, dynamicBlocks)).ToHashSet();

                BlockReference[] brsArray =
                    brs.OrderBy(x => al.StationAtPoint(x)).ToArray();

                List<SizeEntry> sizes = new List<SizeEntry>();

                int dn = 0;
                double start = 0.0;
                double end = 0.0;
                double kod = 0.0;
                PipeSystemEnum ps = default;
                PipeTypeEnum pt = default;
                PipeSeriesEnum series = default;

                for (int i = 0; i < brsArray.Count(); i++)
                {
                    var curBr = brsArray[i];

                    //First iteration case
                    if (i == 0)
                    {
                        start = 0.0; end = al.StationAtPoint(curBr);

                        if (IsTransition(curBr, dynamicBlocks))
                        {
                            dn = GetDirectionallyCorrectReducerDnWithGraph(al, curBr, Side.Left);
                            ps = PipeSystemEnum.Stål;
                            pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                                curBr.ReadDynamicCsvProperty(DynamicProperty.System), true);
                            series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                                curBr.ReadDynamicCsvProperty(DynamicProperty.Serie), true);
                            kod = GetPipeKOd(ps, dn, pt, series);
                        }
                        else
                        {//F-Model og Y-Model
                            dn = int.Parse(curBr.ReadDynamicCsvProperty(DynamicProperty.DN1));
                            ps = PipeSystemEnum.Stål;
                            pt = GetDirectionallyCorrectPropertyWithGraph<PipeTypeEnum>(al, curBr, Side.Left);
                            series = GetDirectionallyCorrectPropertyWithGraph<PipeSeriesEnum>(al, curBr, Side.Left);
                            kod = GetPipeKOd(ps, dn, pt, series);
                        }

                        //Adding first entry
                        sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));

                        //Only one member array case
                        //This is an edge case of first iteration
                        if (brsArray.Length == 1)
                        {
                            start = end;
                            end = al.Length;

                            if (PipelineSizeArray.IsTransition(curBr, dynamicBlocks))
                            {
                                dn = GetDirectionallyCorrectReducerDnWithGraph(al, curBr, Side.Right);
                                ps = PipeSystemEnum.Stål;
                                pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                                    curBr.ReadDynamicCsvProperty(DynamicProperty.System), true);
                                series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                                    curBr.ReadDynamicCsvProperty(DynamicProperty.Serie), true);
                                kod = GetPipeKOd(ps, dn, pt, series);
                            }
                            else
                            {//F-Model og Y-Model
                                dn = int.Parse(curBr.ReadDynamicCsvProperty(DynamicProperty.DN1));
                                ps = PipeSystemEnum.Stål;
                                pt = GetDirectionallyCorrectPropertyWithGraph<PipeTypeEnum>(al, curBr, Side.Right);
                                series = GetDirectionallyCorrectPropertyWithGraph<PipeSeriesEnum>(al, curBr, Side.Right);
                                kod = GetPipeKOd(ps, dn, pt, series);
                            }

                            sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
                            //This guards against executing further code
                            continue;
                        }
                    }

                    //General case
                    if (i != brsArray.Length - 1)
                    {
                        BlockReference nextBr = brsArray[i + 1];
                        start = end;
                        end = al.StationAtPoint(nextBr);

                        if (PipelineSizeArray.IsTransition(curBr, dynamicBlocks))
                        {
                            dn = GetDirectionallyCorrectReducerDnWithGraph(al, curBr, Side.Right);
                            ps = PipeSystemEnum.Stål;
                            pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                                curBr.ReadDynamicCsvProperty(DynamicProperty.System), true);
                            series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                                curBr.ReadDynamicCsvProperty(DynamicProperty.Serie), true);
                            kod = GetPipeKOd(ps, dn, pt, series);
                        }
                        else
                        {//F-Model og Y-Model
                            dn = int.Parse(curBr.ReadDynamicCsvProperty(DynamicProperty.DN1));
                            ps = PipeSystemEnum.Stål;
                            pt = GetDirectionallyCorrectPropertyWithGraph<PipeTypeEnum>(al, curBr, Side.Right);
                            series = GetDirectionallyCorrectPropertyWithGraph<PipeSeriesEnum>(al, curBr, Side.Right);
                            kod = GetPipeKOd(ps, dn, pt, series);
                        }

                        sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
                        //This guards against executing further code
                        continue;
                    }

                    //And here ends the last iteration
                    start = end;
                    end = al.Length;

                    if (PipelineSizeArray.IsTransition(curBr, dynamicBlocks))
                    {
                        dn = GetDirectionallyCorrectReducerDnWithGraph(al, curBr, Side.Right);
                        ps = PipeSystemEnum.Stål;
                        pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.System), true);
                        series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.Serie), true);
                        kod = GetPipeKOd(ps, dn, pt, series);
                    }
                    else
                    {//F-Model og Y-Model
                        dn = int.Parse(curBr.ReadDynamicCsvProperty(DynamicProperty.DN1));
                        ps = PipeSystemEnum.Stål;
                        pt = GetDirectionallyCorrectPropertyWithGraph<PipeTypeEnum>(al, curBr, Side.Right);
                        series = GetDirectionallyCorrectPropertyWithGraph<PipeSeriesEnum>(al, curBr, Side.Right);
                        kod = GetPipeKOd(ps, dn, pt, series);
                    }

                    sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
                }

                SizeArray = sizes.ToArray();
            }
            #endregion

            #region Consolidate sizes
            //Fix for doubling of sizes
            //This particular fix prompted by single pipe
            //where two reducers at same station value (supply, return)
            //gave two size entries with same size and on of them
            //with zero length

            //Consolidate sizes
            if (SizeArray.Length == 1) return;
            List<int> idxsToRemove = new List<int>();
            for (int i = 0; i < SizeArray.Length - 1; i++)
            {
                SizeEntry curSize = SizeArray[i];
                SizeEntry nextSize = SizeArray[i + 1];

                if (curSize.DN == nextSize.DN &&
                    curSize.System == nextSize.System &&
                    curSize.Type == nextSize.Type &&
                    curSize.Series == nextSize.Series)
                {
                    idxsToRemove.Add(i + 1);

                    //guard for more than 2 consecutive sizes
                    bool baseNotFound = true;
                    int curI = i;
                    while (baseNotFound)
                    {
                        if (idxsToRemove.Contains(curI)) curI--;
                        else baseNotFound = false;
                    }

                    SizeArray[curI] = new SizeEntry(
                        curSize.DN,
                        curSize.StartStation,
                        nextSize.EndStation,
                        curSize.Kod,
                        curSize.System,
                        curSize.Type,
                        curSize.Series
                        );

                    //copy values to next to guard agains more than 2 consecutive sizes
                    //Because we need to carry the start station over
                    SizeArray[i + 1] = new SizeEntry(
                        curSize.DN,
                        curSize.StartStation,
                        nextSize.EndStation,
                        curSize.Kod,
                        curSize.System,
                        curSize.Type,
                        curSize.Series
                        );
                }
            }

            if (idxsToRemove.Count < 1) return;
            var sorted = idxsToRemove.OrderByDescending(x => x);
            var tempList = SizeArray.ToList();
            foreach (int i in sorted) tempList.RemoveAt(i);
            SizeArray = tempList.ToArray();
            #endregion
        }
        private PipelineSizesArrangement DetectArrangement(List<int> list)
        {
            if (list.Count < 2) return PipelineSizesArrangement.Unknown;

            bool ascendingFlag = false;
            bool descendingFlag = false;
            bool climaxFlag = false;

            for (int i = 1; i < list.Count; ++i)
            {
                if (list[i] > list[i - 1])
                {
                    if (climaxFlag) return PipelineSizesArrangement.MiddleDescendingToEnds;
                    ascendingFlag = true;
                }
                else if (list[i] < list[i - 1])
                {
                    if (ascendingFlag) climaxFlag = true;
                    descendingFlag = true;
                }
            }

            if (ascendingFlag && !descendingFlag) return PipelineSizesArrangement.SmallToLargeAscending;
            if (!ascendingFlag && descendingFlag) return PipelineSizesArrangement.LargeToSmallDescending;
            if (ascendingFlag && descendingFlag) return PipelineSizesArrangement.MiddleDescendingToEnds;

            return PipelineSizesArrangement.Unknown;
        }
        private void AddEntityToPOIs(Entity ent, HashSet<POI> POIs)
        {
            switch (ent)
            {
                case Polyline pline:
                    switch (GetPipeSystem(pline))
                    {
                        case PipeSystemEnum.Ukendt:
                            prdDbg($"Wrong type of pline supplied: {pline.Handle}");
                            throw new System.Exception("Supplied a new PipeSystemEnum! Add to code kthxbai.");
                        default:
                            POIs.Add(new POI(pline, pline.StartPoint.To2D(), EndType.Start));
                            POIs.Add(new POI(pline, pline.EndPoint.To2D(), EndType.End));
                            break;
                    }
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
                        }
                        POIs.Add(new POI(br, wPt.To2D(), endType));
                    }
                    break;
                default:
                    throw new System.Exception("Wrong type of object supplied!");
            }
        }
        public PipelineSizeArray(SizeEntry[] sizeArray) { SizeArray = sizeArray; }
        public PipelineSizeArray GetPartialSizeArrayForPV(ProfileView pv)
        {
            var list = this.GetIndexesOfSizesAppearingInProfileView(pv.StationStart, pv.StationEnd);
            SizeEntry[] partialAr = new SizeEntry[list.Count];
            for (int i = 0; i < list.Count; i++) partialAr[i] = this[list[i]];
            return new PipelineSizeArray(partialAr);
        }
        public SizeEntry GetSizeAtStation(double station)
        {
            for (int i = 0; i < SizeArray.Length; i++)
            {
                SizeEntry curEntry = SizeArray[i];
                //(stations are END stations!)
                if (station <= curEntry.EndStation) return curEntry;
            }
            return default;
        }
        private int GetDn(Entity entity, System.Data.DataTable dynBlocks)
        {
            if (entity is Polyline pline)
                return GetPipeDN(pline);
            else if (entity is BlockReference br)
            {
                if (br.ReadDynamicCsvProperty(DynamicProperty.Type, false) == "Afgreningsstuds")
                    return ReadComponentDN2Int(br, dynBlocks);
                else return ReadComponentDN1Int(br, dynBlocks);
            }

            else throw new System.Exception("Invalid entity type");
        }
        private PipeTypeEnum GetPipeTypeLocal(Entity entity, System.Data.DataTable dynBlocks)
        {
            if (entity is Polyline pline)
                return GetPipeType(pline, true);
            else if (entity is BlockReference br)
            {
                return (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                    br.ReadDynamicCsvProperty(DynamicProperty.System));
            }

            else throw new System.Exception("Invalid entity type");
        }
        private PipeSeriesEnum GetPipeSeries(Entity entity, System.Data.DataTable dynBlocks)
        {
            if (entity is Polyline pline)
                return GetPipeSeriesV2(pline, true);
            else if (entity is BlockReference br)
            {
                return (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                    br.ReadDynamicCsvProperty(DynamicProperty.Serie));
            }

            else throw new System.Exception("Invalid entity type");
        }
        private double GetStation(Alignment alignment, Entity entity)
        {
            double station = 0;
            double offset = 0;

            switch (entity)
            {
                case Polyline pline:
                    double l = pline.Length;
                    Point3d p = pline.GetPointAtDist(l / 2);
                    alignment.StationOffset(p.X, p.Y, 5.0, ref station, ref offset);
                    break;
                case BlockReference block:
                    try
                    {
                        alignment.StationOffset(block.Position.X, block.Position.Y, 5.0, ref station, ref offset);
                    }
                    catch (Autodesk.Civil.PointNotOnEntityException ex)
                    {
                        prdDbg($"Entity {block.Handle} throws {ex.Message}!");
                        throw;
                    }
                    break;
                default:
                    throw new Exception("Invalid entity type");
            }
            return station;
        }
        public override string ToString()
        {
            //string output = "";
            //for (int i = 0; i < SizeArray.Length; i++)
            //{
            //    output +=
            //        $"{SizeArray[i].DN.ToString("D3")} || " +
            //        $"{SizeArray[i].StartStation.ToString("0000.00")} - {SizeArray[i].EndStation.ToString("0000.00")} || " +
            //        $"{SizeArray[i].Kod.ToString("0")}" +
            //        $"\n";
            //}

            // Convert the struct data to string[][] for easier processing
            string[][] stringData = new string[SizeArray.Length][];
            for (int i = 0; i < SizeArray.Length; i++)
            {
                stringData[i] = SizeArray[i].ToArray();
            }

            // Find the maximum width for each column
            int[] maxColumnWidths = new int[stringData[0].Length];
            for (int col = 0; col < stringData[0].Length; col++)
            {
                maxColumnWidths[col] = stringData.Max(row => row[col].Length);
            }

            // Convert the array to a table string
            string table = "";
            for (int row = 0; row < stringData.Length; row++)
            {
                string line = "";
                for (int col = 0; col < stringData[0].Length; col++)
                {
                    // Right-align each value and add || separator
                    line += stringData[row][col].PadLeft(maxColumnWidths[col]);
                    if (col < stringData[0].Length - 1)
                    {
                        line += " || ";
                    }
                }
                table += line + Environment.NewLine;
            }

            return table;
        }
        private List<int> GetIndexesOfSizesAppearingInProfileView(double pvStationStart, double pvStationEnd)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < SizeArray.Length; i++)
            {
                SizeEntry curEntry = SizeArray[i];
                if (pvStationStart < curEntry.EndStation &&
                    curEntry.StartStation < pvStationEnd) indexes.Add(i);
            }
            return indexes;
        }
        private SizeEntry[] ConstructWithCurves(Alignment al, HashSet<Curve> curves)
        {
            List<SizeEntry> sizes = new List<SizeEntry>();
            double stepLength = 0.1;
            double alLength = al.Length;
            int nrOfSteps = (int)(alLength / stepLength);
            int previousDn = 0;
            int currentDn = 0;
            double previousKod = 0;
            double currentKod = 0;
            for (int i = 0; i < nrOfSteps + 1; i++)
            {
                double curStationBA = stepLength * i;
                Point3d curSamplePoint = default;
                try { curSamplePoint = al.GetPointAtDist(curStationBA); }
                catch (System.Exception) { continue; }

                HashSet<(Curve curve, double dist, double kappeOd)> curveDistTuples =
                    new HashSet<(Curve curve, double dist, double kappeOd)>();

                foreach (Curve curve in curves)
                {
                    //if (curve.GetDistanceAtParameter(curve.EndParam) < 1.0) continue;
                    Point3d closestPoint = curve.GetClosestPointTo(curSamplePoint, false);
                    if (closestPoint != default)
                        curveDistTuples.Add(
                            (curve, curSamplePoint.DistanceHorizontalTo(closestPoint),
                                GetPipeKOd(curve)));
                }
                var result = curveDistTuples.MinBy(x => x.dist).FirstOrDefault();
                //Detect current dn and kod
                currentDn = GetPipeDN(result.curve);
                currentKod = result.kappeOd;
                if (currentDn != previousDn || !currentKod.Equalz(previousKod, 1e-6))
                {
                    //Set the previous segment end station unless there's 0 segments
                    if (sizes.Count != 0)
                    {
                        SizeEntry toUpdate = sizes[sizes.Count - 1];
                        sizes[sizes.Count - 1] = new SizeEntry(
                            toUpdate.DN, toUpdate.StartStation, curStationBA, toUpdate.Kod,
                            GetPipeSystem(result.curve),
                            GetPipeType(result.curve, true),
                            GetPipeSeriesV2(result.curve, true));
                    }
                    //Add the new segment; remember, 0 is because the station will be set next iteration
                    //see previous line
                    if (i == 0) sizes.Add(new SizeEntry(currentDn, 0, 0, result.kappeOd,
                        GetPipeSystem(result.curve),
                        GetPipeType(result.curve, true),
                        GetPipeSeriesV2(result.curve, true)));
                    else sizes.Add(new SizeEntry(currentDn, sizes[sizes.Count - 1].EndStation, 0, result.kappeOd,
                        GetPipeSystem(result.curve),
                        GetPipeType(result.curve, true),
                        GetPipeSeriesV2(result.curve, true)));
                }
                //Hand over DN to cache in "previous" variable
                previousDn = currentDn;
                previousKod = currentKod;
                if (i == nrOfSteps)
                {
                    SizeEntry toUpdate = sizes[sizes.Count - 1];
                    sizes[sizes.Count - 1] = new SizeEntry(toUpdate.DN, toUpdate.StartStation, al.Length, toUpdate.Kod,
                        GetPipeSystem(result.curve),
                        GetPipeType(result.curve, true),
                        GetPipeSeriesV2(result.curve, true));
                }
            }

            return sizes.ToArray();
        }
        /// <summary>
        /// Construct SizeArray based on blocks.
        /// </summary>
        /// <param name="al">Current alignment.</param>
        /// <param name="curves">Curves are only here to provide sizes to F- and Y-Models.</param>
        /// <param name="brs">Size changing blocks and transitions (F- and Y-Models).</param>
        /// <param name="dt">Dynamic block datatable.</param>
        /// <returns>SizeArray with sizes for current alignment.</returns>
        private SizeEntry[] ConstructWithBlocks(Alignment al, HashSet<Curve> curves, HashSet<BlockReference> brs, System.Data.DataTable dt)
        {
            BlockReference[] brsArray = default;

            //New ordering based on station on alignment
            //prdDbg("Using new SizeArray ordering method! Beware!");
            brsArray = brs.OrderBy(x => al.StationAtPoint(x)).ToArray();

            List<SizeEntry> sizes = new List<SizeEntry>();
            double alLength = al.Length;

            int dn = 0;
            double start = 0.0;
            double end = 0.0;
            double kod = 0.0;
            PipeSystemEnum ps = default;
            PipeTypeEnum pt = default;
            PipeSeriesEnum series = default;

            for (int i = 0; i < brsArray.Length; i++)
            {
                BlockReference curBr = brsArray[i];

                if (i == 0)
                {
                    start = 0.0;
                    end = al.StationAtPoint(curBr);

                    //First iteration case
                    if (PipelineSizeArray.IsTransition(curBr, dt))
                    {
                        dn = GetDirectionallyCorrectDn(curBr, Side.Left, dt);
                        //Point3d p3d = al.GetClosestPointTo(curBr.Position, false);
                        //al.StationOffset(p3d.X, p3d.Y, ref end, ref offset);
                        kod = GetDirectionallyCorrectKod(curBr, Side.Left, dt);
                        ps = PipeSystemEnum.Stål;
                        pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.System), true);
                        series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.Serie), true);
                    }
                    else
                    {//F-Model og Y-Model
                        var ends = curBr.GetAllEndPoints();
                        //Determine connected curves
                        var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                        //Find the curves earlier up the alignment
                        var minCurve = query.MinBy(
                            x => al.StationAtPoint(
                                x.GetPointAtDist(
                                    x.GetDistAtPoint(x.EndPoint) / 2.0)))
                            .FirstOrDefault();

                        if (minCurve == default)
                            throw new Exception($"Br {curBr.Handle} does not find minCurve!");

                        dn = GetPipeDN(minCurve);
                        kod = GetPipeKOd(minCurve);
                        ps = GetPipeSystem(minCurve);
                        pt = GetPipeType(minCurve, true);
                        series = GetPipeSeriesV2(minCurve, true);
                    }

                    sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));

                    //Only one member array case
                    //This is an edge case of first iteration
                    if (brsArray.Length == 1)
                    {
                        start = end;
                        end = alLength;

                        if (PipelineSizeArray.IsTransition(curBr, dt))
                        {
                            dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                            kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                            ps = PipeSystemEnum.Stål;
                            pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                                curBr.ReadDynamicCsvProperty(DynamicProperty.System), true);
                            series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                                curBr.ReadDynamicCsvProperty(DynamicProperty.Serie), true);
                        }
                        else
                        {//F-Model og Y-Model
                            var ends = curBr.GetAllEndPoints();
                            //Determine connected curves
                            var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                            //Find the curves further down the alignment
                            var maxCurve = query.MaxBy(
                                x => al.StationAtPoint(
                                    x.GetPointAtDist(
                                        x.GetDistAtPoint(x.EndPoint) / 2.0)))
                                .FirstOrDefault();

                            dn = GetPipeDN(maxCurve);
                            kod = GetPipeKOd(maxCurve);
                            ps = GetPipeSystem(maxCurve);
                            pt = GetPipeType(maxCurve, true);
                            series = GetPipeSeriesV2(maxCurve, true);
                        }

                        sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
                        //This guards against executing further code
                        continue;
                    }
                }

                //General case
                if (i != brsArray.Length - 1)
                {
                    BlockReference nextBr = brsArray[i + 1];
                    start = end;
                    end = al.StationAtPoint(nextBr);

                    if (PipelineSizeArray.IsTransition(curBr, dt))
                    {
                        dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                        //Point3d p3d = al.GetClosestPointTo(nextBr.Position, false);
                        //al.StationOffset(p3d.X, p3d.Y, ref end, ref offset);
                        kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                        ps = PipeSystemEnum.Stål;
                        pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.System), true);
                        series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                            curBr.ReadDynamicCsvProperty(DynamicProperty.Serie), true);
                    }
                    else
                    {
                        var ends = curBr.GetAllEndPoints();
                        //Determine connected curves
                        var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                        //Find the curves further down the alignment
                        var maxCurve = query.MaxBy(
                            x => al.StationAtPoint(
                                x.GetPointAtDist(
                                    x.GetDistAtPoint(x.EndPoint) / 2.0)))
                            .FirstOrDefault();

                        dn = GetPipeDN(maxCurve);
                        kod = GetPipeKOd(maxCurve);
                        ps = GetPipeSystem(maxCurve);
                        pt = GetPipeType(maxCurve, true);
                        series = GetPipeSeriesV2(maxCurve, true);
                    }

                    sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
                    //This guards against executing further code
                    continue;
                }

                //And here ends the last iteration
                start = end;
                end = alLength;

                if (PipelineSizeArray.IsTransition(curBr, dt))
                {
                    dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                    kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                    ps = PipeSystemEnum.Stål;
                    pt = (PipeTypeEnum)Enum.Parse(typeof(PipeTypeEnum),
                        curBr.ReadDynamicCsvProperty(DynamicProperty.System), true);
                    series = (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum),
                        curBr.ReadDynamicCsvProperty(DynamicProperty.Serie), true);
                }
                else
                {
                    var ends = curBr.GetAllEndPoints();
                    //Determine connected curves
                    var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                    //Find the curves further down the alignment
                    var maxCurve = query.MaxBy(
                        x => al.StationAtPoint(
                            x.GetPointAtDist(
                                x.GetDistAtPoint(x.EndPoint) / 2.0)))
                        .FirstOrDefault();

                    if (maxCurve == default)
                        prdDbg($"Br {curBr.Handle} does not find maxCurve!");

                    dn = GetPipeDN(maxCurve);
                    kod = GetPipeKOd(maxCurve);
                    ps = GetPipeSystem(maxCurve);
                    pt = GetPipeType(maxCurve, true);
                    series = GetPipeSeriesV2(maxCurve, true);
                }

                sizes.Add(new SizeEntry(dn, start, end, kod, ps, pt, series));
            }

            return sizes.ToArray();
        }
        /// <summary>
        /// This method should only be used with a graph that is sorted from the start of the alignment.
        /// Also assuming working only with Reduktion
        /// </summary>
        private int GetDirectionallyCorrectReducerDnWithGraph(Alignment al, BlockReference br, Side side)
        {
            if (Graph == null) throw new Exception("Graph is not initialized!");
            if (br.ReadDynamicCsvProperty(DynamicProperty.Type, false) != "Reduktion")
                throw new Exception($"Method GetDirectionallyCorrectReducerDnWithGraph can only be used with \"Reduktion\"!");

            //Left side means towards the start
            //Right side means towards the end
            double brStation = GetStation(al, br);
            var reducerSizes = new List<int>()
            {
                int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN1)),
                int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN2)),
            };

            //Gather up- and downstream vertici
            var upstreamVertices = new List<Entity>();
            var downstreamVertices = new List<Entity>();

            var dfs = new DepthFirstSearchAlgorithm<Entity, Edge<Entity>>(Graph);
            dfs.TreeEdge += edge =>
            {
                if (GetStation(al, edge.Target) > brStation) downstreamVertices.Add(edge.Target);
                else upstreamVertices.Add(edge.Target);
            };
            dfs.Compute(br);

            bool specialCaseSideSearchFailed = false;
            switch (side)
            {
                case Side.Left:
                    {
                        if (upstreamVertices.Count != 0)
                        {
                            for (int i = 0; i < upstreamVertices.Count; i++)
                            {
                                Entity cur = upstreamVertices[i];

                                int candidate = GetDn(cur, dynamicBlocks);
                                if (candidate == 0) continue;
                                else if (reducerSizes.Contains(candidate)) return candidate;
                            }
                            //If this is reached it means somehow all elements failed to deliver a DN
                            specialCaseSideSearchFailed = true;
                        }
                        else specialCaseSideSearchFailed = true;
                    }
                    break;
                case Side.Right:
                    {
                        if (downstreamVertices.Count != 0)
                        {
                            for (int i = 0; i < downstreamVertices.Count; i++)
                            {
                                Entity cur = downstreamVertices[i];

                                int candidate = GetDn(cur, dynamicBlocks);
                                if (candidate == 0) continue;
                                else if (reducerSizes.Contains(candidate)) return candidate;
                            }
                            //If this is reached it means somehow all elements failed to deliver a DN
                            specialCaseSideSearchFailed = true;
                        }
                        else specialCaseSideSearchFailed = true;
                    }
                    break;
            }

            #region Special case where the reducer is first or last element
            if (specialCaseSideSearchFailed)
            {
                //Use the other side to determine the asked for size
                List<Entity> list;
                if (side == Side.Left) list = downstreamVertices;
                else list = upstreamVertices;

                if (list.Count != 0)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Entity cur = list[i];

                        int candidate = GetDn(cur, dynamicBlocks);
                        if (candidate == 0) continue;
                        else if (reducerSizes.Contains(candidate))
                            return reducerSizes[0] == candidate ? reducerSizes[1] : reducerSizes[0];
                    }
                    //If this is reached it means somehow BOTH sides failed to deliver a DN
                    specialCaseSideSearchFailed = true;
                }
                else specialCaseSideSearchFailed = true;
            }

            //If this is reached, something is completely wrong
            throw new Exception($"Finding directionally correct sizes for reducer {br.Handle} failed!");
            #endregion
        }
        private T GetDirectionallyCorrectPropertyWithGraph<T>(Alignment al, BlockReference br, Side side) where T : Enum
        {
            if (Graph == null) throw new Exception("Graph is not initialized!");
            if (!IsXModel(br, dynamicBlocks))
                throw new Exception($"Method GetDirectionallyCorrectPropertyWithGraph can only be used with \"F- or Y-model\"!");

            Type type = typeof(T);

            //Left side means towards the start
            //Right side means towards the end
            double brStation = GetStation(al, br);

            int dn = int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN1));

            //Gather up- and downstream vertici
            var upstreamVertices = new List<Entity>();
            var downstreamVertices = new List<Entity>();

            var dfs = new DepthFirstSearchAlgorithm<Entity, Edge<Entity>>(Graph);
            dfs.TreeEdge += edge =>
            {
                if (GetStation(al, edge.Target) > brStation) downstreamVertices.Add(edge.Target);
                else upstreamVertices.Add(edge.Target);
            };
            dfs.Compute(br);

            bool specialCaseSideSearchFailed = false;
            switch (side)
            {
                case Side.Left:
                    {
                        if (upstreamVertices.Count != 0)
                        {
                            for (int i = 0; i < upstreamVertices.Count; i++)
                            {
                                Entity cur = upstreamVertices[i];

                                switch (type)
                                {
                                    case Type t when t == typeof(PipeTypeEnum):
                                        {
                                            PipeTypeEnum result = GetPipeTypeLocal(cur, dynamicBlocks);
                                            if (result != PipeTypeEnum.Ukendt) return (T)Convert.ChangeType(result, type);
                                        }
                                        break;
                                    case Type t when t == typeof(PipeSeriesEnum):
                                        {
                                            PipeSeriesEnum result = this.GetPipeSeries(cur, dynamicBlocks);
                                            if (result != PipeSeriesEnum.Undefined) return (T)Convert.ChangeType(result, type);
                                        }
                                        break;
                                    default:
                                        throw new System.Exception($"Unhandled Type {type} is not supported!");
                                }

                            }
                            //If this is reached it means somehow all elements failed to deliver a DN
                            specialCaseSideSearchFailed = true;
                        }
                        else specialCaseSideSearchFailed = true;
                    }
                    break;
                case Side.Right:
                    {
                        if (downstreamVertices.Count != 0)
                        {
                            for (int i = 0; i < downstreamVertices.Count; i++)
                            {
                                Entity cur = downstreamVertices[i];

                                switch (type)
                                {
                                    case Type t when t == typeof(PipeTypeEnum):
                                        {
                                            PipeTypeEnum result = GetPipeTypeLocal(cur, dynamicBlocks);
                                            if (result != PipeTypeEnum.Ukendt) return (T)Convert.ChangeType(result, type);
                                        }
                                        break;
                                    case Type t when t == typeof(PipeSeriesEnum):
                                        {
                                            PipeSeriesEnum result = this.GetPipeSeries(cur, dynamicBlocks);
                                            if (result != PipeSeriesEnum.Undefined) return (T)Convert.ChangeType(result, type);
                                        }
                                        break;
                                    default:
                                        throw new System.Exception($"Unhandled Type {type} is not supported!");
                                }
                            }
                            //If this is reached it means somehow all elements failed to deliver a DN
                            specialCaseSideSearchFailed = true;
                        }
                        else specialCaseSideSearchFailed = true;
                    }
                    break;
            }

            #region Special case where the reducer is first or last element
            //Skip this for now, can return back to this if it becomes a problem

            //if (specialCaseSideSearchFailed)
            //{
            //    //Use the other side to determine the asked for size
            //    List<Entity> list;
            //    if (side == Side.Left) list = downstreamVertices;
            //    else list = upstreamVertices;

            //    if (list.Count != 0)
            //    {
            //        for (int i = 0; i < list.Count; i++)
            //        {
            //            Entity cur = list[i];

            //            switch (type)
            //            {
            //                case Type t when t == typeof(PipeTypeEnum):
            //                    {
            //                        PipeTypeEnum result = GetPipeType(cur, dynamicBlocks);
            //                        if (result != PipeTypeEnum.Ukendt) return (T)Convert.ChangeType(result, type);
            //                    }
            //                    break;
            //                case Type t when t == typeof(PipeSeriesEnum):
            //                    {
            //                        PipeSeriesEnum result = this.GetPipeSeries(cur, dynamicBlocks);
            //                        if (result != PipeSeriesEnum.Undefined) return (T)Convert.ChangeType(result, type);
            //                    }
            //                    break;
            //                default:
            //                    throw new System.Exception($"Unhandled Type {type} is not supported!");
            //            }
            //        }
            //        //If this is reached it means somehow BOTH sides failed to deliver a DN
            //        specialCaseSideSearchFailed = true;
            //    }
            //    else specialCaseSideSearchFailed = true;
            //}

            //If this is reached, something is completely wrong
            throw new Exception($"Finding directionally correct properties for X-Model {br.Handle} failed!");
            #endregion
        }
        private int GetDirectionallyCorrectDn(BlockReference br, Side side, System.Data.DataTable dt)
        {
            switch (Arrangement)
            {
                case PipelineSizesArrangement.SmallToLargeAscending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN2Int(br, dt);
                        case Side.Right:
                            return ReadComponentDN1Int(br, dt);
                    }
                    break;
                case PipelineSizesArrangement.LargeToSmallDescending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN1Int(br, dt);
                        case Side.Right:
                            return ReadComponentDN2Int(br, dt);
                    }
                    break;
            }
            return 0;
        }
        private double GetDirectionallyCorrectKod(BlockReference br, Side side, System.Data.DataTable dt)
        {
            switch (Arrangement)
            {
                case PipelineSizesArrangement.SmallToLargeAscending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN2KodDouble(br, dt);
                        case Side.Right:
                            return ReadComponentDN1KodDouble(br, dt);
                    }
                    break;
                case PipelineSizesArrangement.LargeToSmallDescending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN1KodDouble(br, dt);
                        case Side.Right:
                            return ReadComponentDN2KodDouble(br, dt);
                    }
                    break;
            }
            return 0;
        }
        private static bool IsTransition(BlockReference br, System.Data.DataTable dynBlocks)
        {
            string type = ReadStringParameterFromDataTable(br.RealName(), dynBlocks, "Type", 0);

            if (type == null) throw new System.Exception($"Block with name {br.RealName()} does not exist " +
                $"in Dynamiske Komponenter!");

            return type == "Reduktion";
        }
        ///Determines whether the block is an F- or Y-Model
        private static bool IsXModel(BlockReference br, System.Data.DataTable dynBlocks)
        {
            string type = ReadStringParameterFromDataTable(br.RealName(), dynBlocks, "Type", 0);

            if (type == null) throw new System.Exception($"Block with name {br.RealName()} does not exist " +
                $"in Dynamiske Komponenter!");

            HashSet<string> transitionTypes = new HashSet<string>()
            {
                "Y-Model",
                "F-Model"
            };

            return transitionTypes.Contains(type);
        }
        internal void Reverse()
        {
            Array.Reverse(this.SizeArray);
        }
        /// <summary>
        /// Unknown - Should throw an exception
        /// OneSize - Cannot be constructed with blocks
        /// SmallToLargeAscending - Small sizes first, blocks preferred
        /// LargeToSmallAscending - Large sizes first, blocks preferred
        /// </summary>
        public enum PipelineSizesArrangement
        {
            Unknown, //Should throw an exception
            OneSize, //Cannot be constructed with blocks
            SmallToLargeAscending, //Blocks preferred
            LargeToSmallDescending, //Blocks preferred
            MiddleDescendingToEnds //When a pipe is supplied from the middle
        }
        private enum Side
        {
            //Left means towards the start of alignment
            Left,
            //Right means towards the end of alignment
            Right
        }
        public struct POI
        {
            public Entity Owner { get; }
            public List<Entity> Neighbours { get; }
            public Point2d Point { get; }
            public EndType EndType { get; }
            public POI(Entity owner, Point2d point, EndType endType)
            { Owner = owner; Point = point; EndType = endType; Neighbours = new List<Entity>(); }
            public bool IsSameOwner(POI toCompare) => Owner.Id == toCompare.Owner.Id;
            internal void AddReference(POI connectedEntity) => Neighbours.Add(connectedEntity.Owner);
        }
    }
    public struct SizeEntry
    {
        public readonly int DN;
        public readonly double StartStation;
        public readonly double EndStation;
        public readonly double Kod;
        public readonly PipeSystemEnum System;
        public readonly PipeTypeEnum Type;
        public readonly PipeSeriesEnum Series;

        public SizeEntry(
            int dn, double startStation, double endStation, double kod, PipeSystemEnum ps, PipeTypeEnum pt, PipeSeriesEnum series)
        {
            DN = dn; StartStation = startStation; EndStation = endStation; Kod = kod; System = ps; Type = pt; Series = series;
        }

        // Get all properties as strings for table conversion
        public string[] ToArray()
        {
            return new string[]
            {
                DN.ToString(),
                StartStation.ToString("F2"),
                EndStation.ToString("F2"),
                Kod.ToString("F0"),
                System.ToString(),
                Type.ToString(),
                Series.ToString()
            };
        }
    }
    public struct PipelineElement
    {
        public readonly int DN1;
        public readonly int DN2;
        public readonly PipelineElementType Type;
        public readonly double Station;
        public PipelineElement(Entity entity, Alignment al, System.Data.DataTable dt)
        {
            switch (entity)
            {
                case Polyline pline:
                    DN1 = GetPipeDN(pline);
                    DN2 = 0;
                    Type = PipelineElementType.Pipe;
                    break;
                case BlockReference br:
                    DN1 = int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN1));
                    DN2 = int.Parse(br.ReadDynamicCsvProperty(DynamicProperty.DN2));
                    if (PipelineElementTypeDict.ContainsKey(br.ReadDynamicCsvProperty(DynamicProperty.Type, false)))
                        Type = PipelineElementTypeDict[br.ReadDynamicCsvProperty(DynamicProperty.Type, false)];
                    else throw new Exception($"Unknown Type: {br.ReadDynamicCsvProperty(DynamicProperty.Type, false)}");
                    break;
                default:
                    throw new System.Exception(
                        $"Entity {entity.Handle} is not a valid pipeline element!");
            }

            Station = GetStation(al, entity);
        }
    }
}
