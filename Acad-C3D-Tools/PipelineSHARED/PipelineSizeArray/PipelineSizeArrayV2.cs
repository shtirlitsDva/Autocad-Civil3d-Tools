using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.Collections;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray
{
    public interface IPipelineSizeArrayV2
    {
        SizeEntryV2 this[int index] { get; set; }
        int Length { get; }
        IEnumerable<SizeEntryV2> Sizes { get; }
        IPipelineSizeArrayV2 GetPartialSizeArrayForPV(ProfileView pv);
        SizeEntryV2 GetSizeAtStation(double station);
        PipelineSizesArrangement Arrangement { get; }
        string ToString();
    }
    public abstract class PipelineSizeArrayV2Base : IPipelineSizeArrayV2
    {
        protected SizeEntryCollection sizes = new SizeEntryCollection();
        public IEnumerable<SizeEntryV2> Sizes { get => sizes; }
        protected BlockReference[] orderedSizeBrs;
        public SizeEntryV2 this[int index] { get => sizes[index]; set => sizes[index] = value; }
        public int Length { get => sizes.Count; }
        public PipelineSizesArrangement Arrangement { get; protected set; }
        #region Methods for partial arrays
        public SizeEntryV2 GetSizeAtStation(double station)
        {
            for (int i = 0; i < sizes.Count; i++)
            {
                SizeEntryV2 curEntry = sizes[i];
                //(stations are END stations!)
                if (station <= curEntry.EndStation) return curEntry;
            }
            return default;
        }
        private List<int> GetIndexesOfSizesAppearingInProfileView(
            double pvStationStart, double pvStationEnd)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < sizes.Count; i++)
            {
                SizeEntryV2 curEntry = sizes[i];
                if (pvStationStart < curEntry.EndStation &&
                    curEntry.StartStation < pvStationEnd) indexes.Add(i);
            }
            return indexes;
        }
        private SizeEntryV2[] GetArrayOfSizesForPv(ProfileView pv)
        {
            var list = GetIndexesOfSizesAppearingInProfileView(pv.StationStart, pv.StationEnd);
            SizeEntryV2[] partialAr = new SizeEntryV2[list.Count];
            for (int i = 0; i < list.Count; i++) partialAr[i] = this[list[i]];
            return partialAr;
        }
        public IPipelineSizeArrayV2 GetPartialSizeArrayForPV(ProfileView pv)
        {
            return new PipelineSizeArrayV2Partial(GetArrayOfSizesForPv(pv));
        }
        #endregion
        #region Methods to read properties of sizes
        protected bool TryGetDN(IPipelineV2 pipeline, double start, double end, out int dn)
        {
            IEnumerable<Entity> ents = pipeline.GetEntitiesWithinStations(start, end);
            if (orderedSizeBrs != null) ents = ents.Where(x => orderedSizeBrs.All(y => x.Id != y.Id));

            dn = 0;
            if (ents.Count() == 0) return false;

            //First try polylines
            var pline = ents.FirstOrDefault(x => x is Polyline) as Polyline;
            if (pline != null)
            {
                dn = GetPipeDN(pline);
                if (dn != 0) return true;
            }
            //Fall back on blocks
            var brs = ents.Where(x => x is BlockReference).Cast<BlockReference>();
            if (brs.Count() == 0) return false;
            foreach (var br in brs)
            {
                string type = br.ReadDynamicCsvProperty(DynamicProperty.Type, false);
                if (type == "Afgreningsstuds" || type == "Svanehals")
                    dn = Convert.ToInt32(br.ReadDynamicCsvProperty(DynamicProperty.DN2));
                else dn = Convert.ToInt32(br.ReadDynamicCsvProperty(DynamicProperty.DN1));
                if (dn != 0) return true;
            }
            return false;
        }
        protected bool TryGetPipeSystem(IPipelineV2 pipeline, double start, double end, out PipeSystemEnum ps)
        {
            IEnumerable<Entity> ents = pipeline.GetEntitiesWithinStations(start, end);
            if (orderedSizeBrs != null) ents = ents.Where(x => orderedSizeBrs.All(y => x.Id != y.Id));

            ps = PipeSystemEnum.Ukendt;
            if (ents.Count() == 0) return false;

            //First try polylines
            var pline = ents.FirstOrDefault(x => x is Polyline) as Polyline;
            if (pline != null)
            {
                ps = GetPipeSystem(pline);
                if (ps != PipeSystemEnum.Ukendt) return true;
            }
            //Fall back on blocks
            var brs = ents.Where(x => x is BlockReference).Cast<BlockReference>();
            if (brs.Count() == 0) return false;
            foreach (var br in brs)
            {
                string psStr = br.ReadDynamicCsvProperty(DynamicProperty.SysNavn);
                Enum.TryParse(psStr, out ps);
                if (ps != PipeSystemEnum.Ukendt) return true;
            }
            return false;
        }
        protected bool TryGetPipeType(IPipelineV2 pipeline, double start, double end, out PipeTypeEnum pt)
        {
            IEnumerable<Entity> ents = pipeline.GetEntitiesWithinStations(start, end);
            if (orderedSizeBrs != null) ents = ents.Where(x => orderedSizeBrs.All(y => x.Id != y.Id));

            pt = PipeTypeEnum.Ukendt;
            if (ents.Count() == 0) return false;

            //First try polylines
            var pline = ents.FirstOrDefault(x => x is Polyline) as Polyline;
            if (pline != null)
            {
                pt = GetPipeType(pline, true);
                if (pt != PipeTypeEnum.Ukendt) return true;
            }
            //Fall back on blocks
            var brs = ents.Where(x => x is BlockReference).Cast<BlockReference>();
            if (brs.Count() == 0) return false;
            foreach (var br in brs)
            {
                string psStr = br.ReadDynamicCsvProperty(DynamicProperty.System);
                Enum.TryParse(psStr, out pt);
                if (pt != PipeTypeEnum.Ukendt) return true;
            }
            return false;
        }
        protected bool TryGetPipeSeries(IPipelineV2 pipeline, double start, double end, out PipeSeriesEnum series)
        {
            IEnumerable<Entity> ents = pipeline.GetEntitiesWithinStations(start, end);
            if (orderedSizeBrs != null) ents = ents.Where(x => orderedSizeBrs.All(y => x.Id != y.Id));

            series = PipeSeriesEnum.Undefined;
            if (ents.Count() == 0) return false;

            //First try polylines
            var plines = ents.Where(x => x is Polyline);
            foreach (var pline in plines)
            {
                series = GetPipeSeriesV2(pline);
                if (series != PipeSeriesEnum.Undefined) return true;
            }
            //Fall back on blocks
            var brs = ents.Where(x => x is BlockReference).Cast<BlockReference>();
            if (brs.Count() == 0) return false;
            foreach (var br in brs)
            {
                string seriesStr = br.ReadDynamicCsvProperty(DynamicProperty.Serie);
                Enum.TryParse(seriesStr, out series);
                if (series != PipeSeriesEnum.Undefined) return true;
            }
            return false;
        }
        #endregion
        private string[] headers = ["DN", "SS", "ES", "JOD", "System", "Type", "Series", "Min. Vert R"];
        private string[] units = ["[mm]", "[m]", "[m]", "[mm]", "", "", "", "[m]"];
        public override string ToString()
        {
            // Combine headers, units, and data into one list
            List<string[]> tableData = new List<string[]>();

            // Add headers and units to the table data
            tableData.Add(headers);
            tableData.Add(units);

            // Add the data from sizes
            for (int i = 0; i < sizes.Count; i++)
            {
                tableData.Add(sizes[i].ToArray());
            }

            // Find the maximum width for each column
            int columnCount = tableData[0].Length;
            int[] maxColumnWidths = new int[columnCount];

            for (int col = 0; col < columnCount; col++)
            {
                maxColumnWidths[col] = tableData.Max(row => row[col].Length);
            }

            // Build the table string
            StringBuilder table = new StringBuilder();

            for (int row = 0; row < tableData.Count; row++)
            {
                string line = "";
                for (int col = 0; col < columnCount; col++)
                {
                    // Right-align each value and add || separator
                    line += tableData[row][col].PadLeft(maxColumnWidths[col]);
                    if (col < columnCount - 1)
                    {
                        line += " || ";
                    }
                }
                table.AppendLine(line);
            }

            return table.ToString();
        }
    }
    public class PipelineSizeArrayV2Partial : PipelineSizeArrayV2Base
    {
        public PipelineSizeArrayV2Partial(SizeEntryV2[] partial) { sizes = new SizeEntryCollection(partial); }
    }
    public class PipelineSizeArrayV2OnePolyline : PipelineSizeArrayV2Base
    {
        public PipelineSizeArrayV2OnePolyline(IPipelineV2 pipeline)
        {
            Arrangement = PipelineSizesArrangement.OneSize;

            Polyline pline = pipeline.PipelineEntities.GetPolylines().First();

            sizes.Add(new SizeEntryV2(
                GetPipeDN(pline),
                0,
                pipeline.EndStation,
                GetPipeKOd(pline),
                GetPipeSystem(pline),
                GetPipeType(pline),
                GetPipeSeriesV2(pline)
                ));
        }
    }
    public class PipelineSizeArrayV2Blocks : PipelineSizeArrayV2Base
    {
        public PipelineSizeArrayV2Blocks(IPipelineV2 pipeline)
        {
            // Get all the blocks that define the sizes
            IEnumerable<BlockReference> sizeBrs = pipeline.PipelineEntities.GetBlockReferences()
                .Where(x => x.ReadDynamicCsvProperty(
                    DynamicProperty.Function, false) == "SizeArray");

            // Order the blocks by station
            orderedSizeBrs = [.. sizeBrs.OrderBy(pipeline.GetBlockStation)];

#if DEBUG
            //foreach (var item in orderedSizeBrs)
            //{
            //    prdDbg($"Block {item.RealName()} at station {pipeline.GetBlockStation(item)}");
            //}
#endif

            // The direction is assumed to be from start to and of alignment (or NA)
            // Use a new method using stations to gather blocks
            // Build an array to represent the topology of the pipeline

            List<TopologyEntry> topology = new();

            for (int i = 0; i < orderedSizeBrs.Length; i++)
            {
                var br = orderedSizeBrs[i];

                topology.Add(new
                    (br.ReadDynamicCsvProperty(
                        DynamicProperty.Type, false),
                        pipeline.GetBlockStation(br), br));
            }

            #region Squash reducers with same size
            //!!!!THIS WON'T WORK IF THIS IS REDUCERS ON BOTH SIDES OF A TEE!!!!
            //The sizes need to be squashed after the final topology is built

            //// Handle a very specific case where there are enkelt pipes
            //// and reducers of both frem and retur are present.
            //// One of the reducers needs to be removed.
            //// It is ASSUMED that they are placed close to each other.
            //// Now iterate over the topology comparing i and i+1
            //// if they are both reducers and the same size, remove the first one
            //for (int i = topology.Count - 2; i >= 0; i--)
            //{
            //    var f = topology[i];
            //    var s = topology[i + 1];

            //    if (f.type == "Reducer" && s.type == "Reducer")
            //    {
            //        int fDn1 = Convert.ToInt32(f.br.ReadDynamicCsvProperty(
            //            DynamicProperty.DN1,));
            //        int fDn2 = Convert.ToInt32(f.br.ReadDynamicCsvProperty(
            //            DynamicProperty.DN2,));
            //        int sDn1 = Convert.ToInt32(s.br.ReadDynamicCsvProperty(
            //            DynamicProperty.DN1,));
            //        int sDn2 = Convert.ToInt32(s.br.ReadDynamicCsvProperty(
            //            DynamicProperty.DN2,));

            //        if (fDn1 == sDn1 && fDn2 == sDn2) topology.RemoveAt(i);
            //    }
            //}
            #endregion

            #region Remove MATSKIFT at start and end
            //I think this was a quick fix
            //but it throws an exception now for a particular case
            //Removing it makes the code work

            //if (topology[0].type == "Materialeskift {#M1}{#DN1}x{#M2}{#DN2}")
            //{
            //    double station = topology[0].station;
            //    if (station < 0.1) topology.RemoveAt(0);
            //}
            //if (topology[topology.Count - 1].type == "Materialeskift {#M1}{#DN1}x{#M2}{#DN2}")
            //{
            //    double station = topology[topology.Count - 1].station;
            //    if (Math.Abs(pipeline.EndStation - station) < 0.1) topology.RemoveAt(topology.Count - 1);
            //}
            #endregion

            //Prepare the ranges of stations for querying
            //This is to calculate the stations for each range before building the sizes array
            //This is to enable using a single method for all cases
            var ranges = new List<RangeEntry>();
            ranges.Add(new(0, topology[0].station, topology[0].station,
                topology.Count == 1 ? pipeline.EndStation : topology[1].station,
                topology[0].br, getNext(0)?.br, null));
            for (int i = 0; i < topology.Count; i++)
                ranges.Add(new(topology[i].station, i == topology.Count - 1 ? pipeline.EndStation : topology[i + 1].station,
                    i == 0 ? 0.0 : topology[i - 1].station, topology[i].station, topology[i].br,
                    getNext(i)?.br, getPrev(i)?.br)); //<-- this is not just prev and next, here the main is next
            //and the prev is secondary to help establish dn sequence

            TopologyEntry? getNext(int curIdx)
            {
                int count = topology.Count;
                if (curIdx + 1 < topology.Count) return topology[curIdx + 1];
                else return null;
            }
            TopologyEntry? getPrev(int curIdx)
            {
                int count = topology.Count;
                if (curIdx > 0) return topology[curIdx - 1];
                else return null;
            }

            //Build the sizes array
            for (int i = 0; i < ranges.Count; i++)
            {
                SizeEntryV2 size = GetSizeData(pipeline, ranges[i]);
                sizes.Add(size);
            }

            //Squash size entries with same size
            //Is opstår when there are single pipe reducers right next to each other

            for (int i = sizes.Count - 2; i >= 0; i--)
            {
                var f = sizes[i];
                var s = sizes[i + 1];

                if (f.DN == s.DN && f.Type == s.Type && f.Series == s.Series && f.System == s.System)
                {
                    SizeEntryV2 ns = new SizeEntryV2(
                        f.DN,
                        f.StartStation,
                        s.EndStation,
                        f.Kod,
                        f.System,
                        f.Type,
                        f.Series);

                    sizes[i] = ns;
                    sizes.RemoveAt(i + 1);
                }
            }
        }

        private SizeEntryV2 GetSizeData(IPipelineV2 pipeline, RangeEntry range)
        {
            var current = range.br;
            PipelineElementType type = current.GetPipelineType();
            double start;
            double end;

            #region TryGetDN
            start = range.mainS; end = range.mainE;

            int dn;
            switch (type)
            {
                case PipelineElementType.F_Model: //X_Model DN can be read directly
                case PipelineElementType.Y_Model:
                    //TryGetDN(pipeline, start, end, out dn); <-- this failed when a materiale skift was placed
                    //directly on one end of the Y-Model because of change in placement strategy for MSs
                    //TryGetDN tries to find the DN looking at the sides of the block
                    //But X-Models have one DN on both sides, so this is not necessary
                    //Fixed by reading the DN directly, if errors occur again need to look for a better solution
                    dn = Convert.ToInt32(current.ReadDynamicCsvProperty(
                        DynamicProperty.DN1, true));
                    break;
                case PipelineElementType.Reduktion: //Need to look at sides
                case PipelineElementType.Materialeskift:
                    if (!TryGetDN(pipeline, start, end, out dn))
                    {//If operation fails, try other side
                        int DN1 = Convert.ToInt32(current.ReadDynamicCsvProperty(
                            DynamicProperty.DN1, true));
                        int DN2 = Convert.ToInt32(current.ReadDynamicCsvProperty(
                            DynamicProperty.DN2, true));

                        //set the query params for the other side
                        start = range.secondaryS; end = range.secondaryE;

                        int otherSideDN;
                        if (!TryGetDN(pipeline, start, end, out otherSideDN))
                        {
                            prdDbg($"Could not find DN for block {current.Handle} using FIRST method! Trying SECOND method...");

                            //The following code tries to handle a case where reduktion/materialeskift
                            //is adjacent with nothing in between.
                            int otherDN1 = Convert.ToInt32(range.mainBr?.ReadDynamicCsvProperty(
                                DynamicProperty.DN1, true));
                            int otherDN2 = Convert.ToInt32(range.mainBr?.ReadDynamicCsvProperty(
                                DynamicProperty.DN2, true));

                            if (DN1 == otherDN1) dn = DN1;
                            else if (DN1 == otherDN2) dn = DN1;
                            else if (DN2 == otherDN1) dn = DN2;
                            else if (DN2 == otherDN2) dn = DN2;
                            else
                            {
                                otherDN1 = Convert.ToInt32(range.secondaryBr?.ReadDynamicCsvProperty(
                                DynamicProperty.DN1, true));
                                otherDN2 = Convert.ToInt32(range.secondaryBr?.ReadDynamicCsvProperty(
                                    DynamicProperty.DN2, true));

                                if (DN1 == otherDN1) dn = DN1;
                                else if (DN1 == otherDN2) dn = DN1;
                                else if (DN2 == otherDN1) dn = DN2;
                                else if (DN2 == otherDN2) dn = DN2;
                                else
                                {
                                    prdDbg($"Could not find DN for block {current.Handle} using SECOND method!");
                                    throw new Exception("Could not find DN for block!");
                                }
                            }
                            prdDbg("Second METHOD success!");
                        }
                        else
                        {
                            if (DN1 == otherSideDN) dn = DN2;
                            else dn = DN1;
                        }
                    }
                    break;
                default:
                    throw new Exception($"Unexpected type received {type}! Must only be SizeArray blocks.");
            }

            if (dn == 0)
            {
                prdDbg($"Could not find DN for block {current.Handle}!");
                throw new Exception("Could not find DN for block!");
            }
            #endregion

            #region TryGetPipeSystem
            //Reset query params for deferred execution
            //because they could have been changed
            start = range.mainS; end = range.mainE;

            PipeSystemEnum ps;
            switch (type)
            {
                case PipelineElementType.Materialeskift://Need to look at sides
                    if (!TryGetPipeSystem(pipeline, start, end, out ps))
                    {//If operation fails, try other side

                        start = range.secondaryS; end = range.secondaryE;

                        PipeSystemEnum otherSidePS;
                        if (!TryGetPipeSystem(pipeline, start, end, out otherSidePS))
                        {
                            var M1 = GetSystemType(current.ReadDynamicCsvProperty(DynamicProperty.M1));
                            var M2 = GetSystemType(current.ReadDynamicCsvProperty(DynamicProperty.M2));

                            var dn1 = Convert.ToInt32(current.ReadDynamicCsvProperty(DynamicProperty.DN1));
                            var dn2 = Convert.ToInt32(current.ReadDynamicCsvProperty(DynamicProperty.DN2));

                            if (dn1 == dn) ps = M1;
                            else if (dn2 == dn1) ps = M2;
                            else ps = PipeSystemEnum.Ukendt;                            
                        }
                        else
                        {
                            var M1 = GetSystemType(current.ReadDynamicCsvProperty(DynamicProperty.M1));
                            var M2 = GetSystemType(current.ReadDynamicCsvProperty(DynamicProperty.M2));

                            if (M1 == otherSidePS) ps = M2;
                            else ps = M1;
                        }
                    }                    
                    break;
                case PipelineElementType.F_Model:
                case PipelineElementType.Y_Model:
                case PipelineElementType.Reduktion: //PipeSystemType can be read directly
                    string psStr = current.ReadDynamicCsvProperty(DynamicProperty.SysNavn);
                    Enum.TryParse(psStr, out ps);
                    break;
                default:
                    throw new Exception($"Unexpected type received {type}! Must only be SizeArray blocks.");
            }

            if (ps == PipeSystemEnum.Ukendt)
            {
                prdDbg($"Could not find PipeSystemEnum for block {current.Handle}!");
                throw new Exception("Could not find PipeSystemEnum for block!");
            }
            #endregion

            #region TryGetPipeType
            //Reset query params for deferred execution
            //because they could have been changed
            start = range.mainS; end = range.mainE;

            PipeTypeEnum pt;
            switch (type)
            {
                case PipelineElementType.F_Model:
                case PipelineElementType.Y_Model: //Need to look at sides
                    if (!TryGetPipeType(pipeline, start, end, out pt))
                    {//If operation fails, try other side   

                        start = range.secondaryS; end = range.secondaryE;

                        PipeTypeEnum otherSidePT;
                        if (!TryGetPipeType(pipeline, start, end, out otherSidePT))
                        {
                            prdDbg($"Could not find PipeType for block {current.Handle}!");
                            throw new Exception("Could not find PipeType for block!");
                        }
                        else
                        {
                            if (otherSidePT == PipeTypeEnum.Enkelt) pt = PipeTypeEnum.Twin;
                            else pt = PipeTypeEnum.Enkelt;
                        }
                    }
                    break;
                case PipelineElementType.Materialeskift:
                case PipelineElementType.Reduktion: //PipeType can be read directly
                    string ptStr = current.ReadDynamicCsvProperty(DynamicProperty.System);
                    Enum.TryParse(ptStr, out pt);
                    break;
                default:
                    throw new Exception($"Unexpected type received {type}! Must only be SizeArray blocks.");
            }

            if (pt == PipeTypeEnum.Ukendt)
            {
                prdDbg($"Could not find PipeTypeEnum for block {current.Handle}!");
                throw new Exception("Could not find PipeTypeEnum for block!");
            }
            #endregion

            #region TryGetPipeSeries
            //Reset query params for deferred execution
            //because they could have been changed
            start = range.mainS; end = range.mainE;

            PipeSeriesEnum serie;

            switch (type)
            {
                case PipelineElementType.F_Model:
                case PipelineElementType.Y_Model: //Need to look at sides
                case PipelineElementType.Materialeskift: //because block information is unreliable
                case PipelineElementType.Reduktion:
                    TryGetPipeSeries(pipeline, start, end, out serie);
                    break;
                default:
                    throw new Exception($"Unexpected type received {type}! Must only be SizeArray blocks.");
            }

            if (serie == PipeSeriesEnum.Undefined)
            {
                //Fall back on reading series from size array block, this is unstable!
                string seriesStr = current.ReadDynamicCsvProperty(DynamicProperty.Serie);
                Enum.TryParse(seriesStr, out serie);
            }

            if (serie == PipeSeriesEnum.Undefined)
            {
                prdDbg($"Could not find PipeSeriesEnum for block {current.Handle}!");
                throw new Exception("Could not find PipeSeriesEnum for block!");
            }
            #endregion

            return new SizeEntryV2(dn, range.mainS, range.mainE, GetPipeKOd(ps, dn, pt, serie), ps, pt, serie);
        }
        /// <summary>
        /// Assuming br is a SizeArray block. Currently NOT USED.
        /// </summary>
        private (HashSet<Entity> One, HashSet<Entity> Two) GatherConnectedEntities(
            BlockReference br, IPipelineV2 pipeline)
        {
            HashSet<Entity> one = new HashSet<Entity>();
            HashSet<Entity> two = new HashSet<Entity>();
            var res = (one, two);

            var ce = pipeline.PipelineEntities.GetConnectedEntitiesDelimited(br);

            if (ce.Count > 3) { prdDbg($"Block {br.Handle} has more than 3 entities connected!"); return res; }
            if (ce.Count < 1) { prdDbg($"Block {br.Handle} does not have any entities connected!"); return res; }

            // If the block currently processed is an X-model
            // we can potentially have three collections of entities.
            // Determine if it is the case
            string type = br.ReadDynamicCsvProperty(
                DynamicProperty.Type, false);
            if (type == "F-Model" || type == "Y-Model")
            {
                if (ce.Count == 3)
                {
                    var query1 = ce.Where(x => x.Any(y =>
                    y.GetEntityPipeType(true) == PipeTypeEnum.Enkelt));
                    foreach (var item in query1) one.UnionWith(item);

                    var query2 = ce.Where(x => x.Any(y =>
                    y.GetEntityPipeType(true) == PipeTypeEnum.Twin));
                    foreach (var item in query2) two.UnionWith(item);

                    return res;
                }
                else if (ce.Count == 2)
                {
                    res.one.UnionWith(ce.First());
                    res.two.UnionWith(ce.Last());
                    return res;
                }
                else if (ce.Count == 1)
                {
                    res.one.UnionWith(ce.First());
                    return res;
                }
            }
            else if (ce.Count == 3)
            {
                prdDbg($"Block {br.Handle} is not an X-model but has 3 entities connected!");
                return res;
            }
            else if (ce.Count == 2)
            {
                res.one.UnionWith(ce.First());
                res.two.UnionWith(ce.Last());
                return res;
            }
            else if (ce.Count == 1)
            {
                res.one.UnionWith(ce.First());
                return res;
            }
            throw new NotImplementedException("Hit code that shouldn't be hit.");
        }
    }
    /// <summary>
    /// This class assumes that there are no SizeArray blocks in the pipeline.
    /// There can by multiple polylines in the pipeline.
    /// There can be multiple BlockReferences in the pipeline.
    /// </summary>
    public class PipelineSizeArrayV2NoBlocks : PipelineSizeArrayV2Base
    {
        public PipelineSizeArrayV2NoBlocks(IPipelineV2 pipeline)
        {
            int dn = 0;
            if (!TryGetDN(pipeline, 0.0, pipeline.EndStation, out dn))
            {
                prdDbg($"Could not find DN for pipeline {pipeline.Name}!");
                throw new Exception("Could not find DN for pipeline!");
            }

            PipeSystemEnum ps = PipeSystemEnum.Ukendt;
            if (!TryGetPipeSystem(pipeline, 0.0, pipeline.EndStation, out ps))
            {
                prdDbg($"Could not find PipeSystemEnum for pipeline {pipeline.Name}!");
                throw new Exception("Could not find PipeSystemEnum for pipeline!");
            }

            PipeTypeEnum pt = PipeTypeEnum.Ukendt;
            if (!TryGetPipeType(pipeline, 0.0, pipeline.EndStation, out pt))
            {
                prdDbg($"Could not find PipeTypeEnum for pipeline {pipeline.Name}!");
                throw new Exception("Could not find PipeTypeEnum for pipeline!");
            }

            PipeSeriesEnum series = PipeSeriesEnum.Undefined;
            var pline = pipeline.PipelineEntities.GetPolylines().FirstOrDefault();
            if (pline != null) series = GetPipeSeriesV2(pline, true);

            //Fall back on blocks
            if (series == PipeSeriesEnum.Undefined)
            {
                if (pipeline.PipelineEntities.GetBlockReferences().Count() == 0)
                    throw new Exception($"Could not find PipeSeriesEnum for pipeline {pipeline.Name}!\n" +
                        $"Polylines failed to provide Series and there's no blocks present!");

                foreach (var br in pipeline.PipelineEntities.GetBlockReferences())
                {
                    string seriesStr = br.ReadDynamicCsvProperty(DynamicProperty.Serie);
                    Enum.TryParse(seriesStr, out series);
                    if (series != PipeSeriesEnum.Undefined) break;
                }
            }

            sizes.Add(new SizeEntryV2(
                dn,
                0.0,
                pipeline.EndStation,
                GetPipeKOd(ps, dn, pt, series),
                ps,
                pt,
                series));
        }
    }
    public static class PipelineSizeArrayFactory
    {
        public static IPipelineSizeArrayV2 CreateSizeArray(IPipelineV2 pipeline)
        {
            //Case only one polyline in the pipeline
            if (pipeline.PipelineEntities.GetPolylines().Count() == 1 &&
                !pipeline.PipelineEntities.HasSizeArrayBrs()) return new PipelineSizeArrayV2OnePolyline(pipeline);
            //Case array defining blocks exist
            else if (pipeline.PipelineEntities.HasSizeArrayBrs()) return new PipelineSizeArrayV2Blocks(pipeline);
            else return new PipelineSizeArrayV2NoBlocks(pipeline);

            throw new NotImplementedException();
        }
    }
    public readonly record struct TopologyEntry(
        string type, double station, BlockReference br);
    public readonly record struct RangeEntry(
        double mainS, double mainE, double secondaryS, double secondaryE, BlockReference br,
        BlockReference? mainBr, BlockReference? secondaryBr);
    public struct SizeEntryV2
    {
        [JsonInclude]
        public readonly int DN;
        [JsonInclude]
        public readonly double StartStation;
        [JsonInclude]
        public readonly double EndStation;
        [JsonInclude]
        public readonly double Kod;
        [JsonInclude]
        public readonly double VerticalMinRadius => GetPipeMinElasticRadiusVerticalCharacteristic(System, DN, Type);
        [JsonInclude]
        public readonly PipeSystemEnum System;
        [JsonInclude]
        public readonly PipeTypeEnum Type;
        [JsonInclude]
        public readonly PipeSeriesEnum Series;

        [JsonIgnore]
        public string SizePrefix
        {
            get
            {
                return System switch
                {
                    PipeSystemEnum.Ukendt => "UK",
                    PipeSystemEnum.Stål => "DN",
                    PipeSystemEnum.Kobberflex => "Ø",
                    PipeSystemEnum.AluPex => "Ø",
                    PipeSystemEnum.PertFlextra => "Ø",
                    PipeSystemEnum.AquaTherm11 => "Ø",
                    _ => "UNKNOWN",
                };
            }
        }

        public SizeEntryV2(
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
                Series.ToString(),
                VerticalMinRadius.ToString("F2")
            };
        }
    }
    public enum PipelineSizesArrangement
    {
        Unknown, //Should throw an exception
        OneSize, //Cannot be constructed with blocks
        SmallToLargeAscending, //Blocks preferred
        LargeToSmallDescending, //Blocks preferred
        MiddleDescendingToEnds //When a pipe is supplied from the middle
    }
    public class SizeEntryCollection : ICollection<SizeEntryV2>
    {
        private List<SizeEntryV2> _L = new List<SizeEntryV2>();
        public SizeEntryV2 this[int index] { get => _L[index]; set => _L[index] = value; }
        public SizeEntryCollection() { }
        public SizeEntryCollection(IEnumerable<SizeEntryV2> sizes)
        {
            _L.AddRange(sizes);
        }
        public void Add(SizeEntryV2 item)
        {
            _L.Add(item);
        }
        public int Count => _L.Count;
        public bool IsReadOnly => false;
        public void Clear() => _L.Clear();
        public bool Contains(SizeEntryV2 item) => _L.Contains(item);
        public void CopyTo(SizeEntryV2[] array, int arrayIndex) => _L.CopyTo(array, arrayIndex);
        public bool Remove(SizeEntryV2 item) => _L.Remove(item);
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= _L.Count) return false;
            _L.RemoveAt(index);
            return true;
        }
        public IEnumerator<SizeEntryV2> GetEnumerator() => _L.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
