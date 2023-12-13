using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.Collections;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.CommonScheduleExtensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntersectUtilities.UtilsCommon;
using QuikGraph.Serialization;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public interface IPipelineSizeArrayV2
    {
        SizeEntryV2 this[int index] { get; set; }
        PipelineSizesArrangement Arrangement { get; }
    }
    public abstract class PipelineSizeArrayV2Base : IPipelineSizeArrayV2
    {
        protected SizeEntryCollection sizes = new SizeEntryCollection();
        public SizeEntryV2 this[int index] { get => sizes[index]; set => sizes[index] = value; }
        public PipelineSizesArrangement Arrangement { get; protected set; }
        protected enum Side
        {
            //Left means towards the start of alignment
            Left,
            //Right means towards the end of alignment
            Right
        }
    }
    public class PipelineSizeArrayV2OnePolyline : PipelineSizeArrayV2Base
    {
        public PipelineSizeArrayV2OnePolyline(IPipelineV2 pipeline)
        {
            Arrangement = PipelineSizesArrangement.OneSize;

            Polyline pline = pipeline.Entities.GetPolylines().First();

            sizes.Add(new SizeEntryV2(
                GetPipeDN(pline),
                pipeline.GetPolylineStartStation(pline),
                pipeline.GetPolylineEndStation(pline),
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
            var sizeBrs = pipeline.Entities.GetBlockReferences()
                .Where(x => x.ReadDynamicCsvProperty(
                    DynamicProperty.Function, false) == "SizeArray");

            // Order the blocks by station
            var orderedSizeBrs = sizeBrs.OrderBy(x => pipeline.GetBlockStation(x)).ToArray();

            // The direction is assumed to be from start to and of alignment (or NA)
            // Use a new method using stations to gather blocks
            // Build an array to represent the topology of the pipeline

            List<(string type, double station, BlockReference br)> topology =
                new List<(string type, double station, BlockReference br)>();

            for (int i = 0; i < orderedSizeBrs.Length; i++)
            {
                var br = orderedSizeBrs[i];

                topology.Add(
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
            if (topology[0].type == "Materialeskift {#M1}{#DN1}x{#M2}{#DN2}")
            {
                double station = topology[0].station;
                if (station < 0.1) topology.RemoveAt(0);
            }
            if (topology[topology.Count - 1].type == "Materialeskift {#M1}{#DN1}x{#M2}{#DN2}")
            {
                double station = topology[topology.Count - 1].station;
                if (Math.Abs(pipeline.EndStation - station) < 0.1) topology.RemoveAt(topology.Count - 1);
            }
            #endregion

            //Prepare the ranges of stations for querying
            //This is to calculate the stations for each range before building the sizes array
            //This is to enable using a single method for all cases
            var ranges = new List<(double fsS, double fsE, double ssS, double ssE, BlockReference br)>();
            ranges.Add((0, topology[0].station, topology[0].station,
                topology.Count == 1 ? pipeline.EndStation : topology[1].station, topology[0].br));
            for (int i = 0; i < topology.Count; i++)
                ranges.Add((topology[i].station, i == topology.Count - 1 ? pipeline.EndStation : topology[i + 1].station,
                    i == 0 ? 0.0 : topology[i - 1].station, topology[i].station, topology[i].br));

            //Build the sizes array
            for (int i = 0; i < ranges.Count; i++)
            {
                //Deferred execution here
                double start = 0;
                double end = 0;
                var query = pipeline.GetEntitiesWithinStations(start, end)
                        .Where(x => sizeBrs.All(y => x.Id != y.Id)); //<- removing SizeArray blocks from query

                SizeEntryV2 size = GetSizeData(ranges[i], ref start, ref end, query);
                sizes.Add(size);
            }
        }

        private SizeEntryV2 GetSizeData(
            (double fsS, double fsE, double ssS, double ssE, BlockReference br) range,
            ref double start,
            ref double end,
            IEnumerable<Entity> query)
        {
            var current = range.br;

            PipelineElementType type = current.GetPipelineType();

            #region TryGetDN
            start = range.fsS; end = range.fsE;

            int dn;
            switch (type)
            {
                case PipelineElementType.F_Model: //X_Model DN can be read directly
                case PipelineElementType.Y_Model:
                    TryGetDN(query, out dn);
                    break;
                case PipelineElementType.Reduktion: //Need to look at sides
                case PipelineElementType.Materialeskift:
                    if (!TryGetDN(query, out dn))
                    {//If operation fails, try other side
                        int DN1 = Convert.ToInt32(current.ReadDynamicCsvProperty(
                            DynamicProperty.DN1, true));
                        int DN2 = Convert.ToInt32(current.ReadDynamicCsvProperty(
                            DynamicProperty.DN2, true));

                        //set the query params for the other side
                        start = range.ssS; end = range.ssE;

                        int otherSideDN;
                        if (!TryGetDN(query, out otherSideDN))
                        {
                            prdDbg($"Could not find DN for block {current.Handle}!");
                            throw new Exception("Could not find DN for block!");
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
            start = range.fsS; end = range.fsE;

            PipeSystemEnum ps;
            switch (type)
            {
                case PipelineElementType.Materialeskift://Need to look at sides
                    if (!TryGetPipeSystem(query, out ps))
                    {//If operation fails, try other side

                        start = range.ssS; end = range.ssE;

                        PipeSystemEnum otherSidePS;
                        if (!TryGetPipeSystem(query, out otherSidePS))
                        {
                            prdDbg($"Could not find PipeSystem for block {current.Handle}!");
                            throw new Exception("Could not find PipeSystem for block!");
                        }
                        else
                        {
                            var M1 = GetPipeSystem(current.ReadDynamicCsvProperty(
                                DynamicProperty.M1, true));
                            var M2 = GetPipeSystem(current.ReadDynamicCsvProperty(
                                DynamicProperty.M2, true));

                            if (M1 == otherSidePS) ps = M2;
                            else ps = M1;
                        }
                    }
                    break;
                case PipelineElementType.F_Model:
                case PipelineElementType.Y_Model:
                case PipelineElementType.Reduktion: //PipeSystemType can be read directly
                    string psStr = current.ReadDynamicCsvProperty(
                        DynamicProperty.SysNavn, true);
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
            start = range.fsS; end = range.fsE;

            PipeTypeEnum pt;
            switch (type)
            {
                case PipelineElementType.F_Model:
                case PipelineElementType.Y_Model: //Need to look at sides
                    if (!TryGetPipeType(query, out pt))
                    {//If operation fails, try other side

                        start = range.ssS; end = range.ssE;

                        PipeTypeEnum otherSidePT;
                        if (!TryGetPipeType(query, out otherSidePT))
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
                    string ptStr = current.ReadDynamicCsvProperty(
                        DynamicProperty.System, true);
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

            return new SizeEntryV2(dn, range.fsS, range.fsE, default, ps, pt, default);

        }

        private bool TryGetDN(IEnumerable<Entity> ents, out int dn)
        {
            dn = 0;

            foreach (var item in ents)
            {
                switch (item)
                {
                    case Polyline pline:
                        dn = GetPipeDN(pline);
                        break;
                    case BlockReference br:
                        string type = br.ReadDynamicCsvProperty(
                            DynamicProperty.Type, false);
                        if (type == "Afgreningsstuds" ||
                            type == "Svanehals")
                        {
                            dn = Convert.ToInt32(br.ReadDynamicCsvProperty(
                                DynamicProperty.DN2, true));
                        }
                        else dn = Convert.ToInt32(br.ReadDynamicCsvProperty(
                            DynamicProperty.DN1, true));
                        break;
                }

                if (dn != 0) return true;
            }
            return false;
        }
        private bool TryGetPipeSystem(IEnumerable<Entity> ents, out PipeSystemEnum ps)
        {
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
                string psStr = br.ReadDynamicCsvProperty(
                    DynamicProperty.SysNavn, true);
                Enum.TryParse(psStr, out ps);
                if (ps != PipeSystemEnum.Ukendt) return true;
            }
            return false;
        }
        private bool TryGetPipeType(IEnumerable<Entity> ents, out PipeTypeEnum pt)
        {
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
        private SizeEntryV2 GetDirectedSizeEntry(BlockReference br, IPipelineV2 pipeline, Side side)
        {
            // traverse both sides of the block and gather the objects in both directions
            throw new NotImplementedException();
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

            var ce = pipeline.Entities.GetConnectedEntitiesDelimited(br);

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
                    CommonScheduleExtensions.GetEntityPipeType(y, true) == PipeTypeEnum.Enkelt));
                    foreach (var item in query1) one.UnionWith(item);

                    var query2 = ce.Where(x => x.Any(y =>
                    CommonScheduleExtensions.GetEntityPipeType(y, true) == PipeTypeEnum.Twin));
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
    public static class PipelineSizeArrayFactory
    {
        public static IPipelineSizeArrayV2 CreateSizeArray(IPipelineV2 pipeline)
        {
            //Case only one polyline in the pipeline
            if (pipeline.Entities.GetPolylines().Count() == 1) return new PipelineSizeArrayV2OnePolyline(pipeline);
            //Case array defining blocks exist
            else if (pipeline.Entities.HasSizeArrayBrs()) return new PipelineSizeArrayV2Blocks(pipeline);
            else throw new NotImplementedException();
        }
    }
    public struct SizeEntryV2
    {
        public readonly int DN;
        public readonly double StartStation;
        public readonly double EndStation;
        public readonly double Kod;
        public readonly PipeSystemEnum System;
        public readonly PipeTypeEnum Type;
        public readonly PipeSeriesEnum Series;

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
                Series.ToString()
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
}
