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
                    DynamicProperty.Function, CsvData.Get("fjvKomponenter"), false) == "SizeArray");

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
                        DynamicProperty.Type, CsvData.Get("fjvKomponenter"), false),
                        pipeline.GetBlockStation(br), br));
            }

            #region Squash reducers with same size
            // Handle a very specific case where there are enkelt pipes
            // and reducers of both frem and retur are present.
            // One of the reducers needs to be removed.
            // It is ASSUMED that they are placed close to each other.
            // Now iterate over the topology comparing i and i+1
            // if they are both reducers and the same size, remove the first one
            for (int i = topology.Count - 2; i >= 0; i--)
            {
                var f = topology[i];
                var s = topology[i + 1];

                if (f.type == "Reducer" && s.type == "Reducer")
                {
                    int fDn1 = Convert.ToInt32(f.br.ReadDynamicCsvProperty(
                        DynamicProperty.DN1, CsvData.Get("fjvKomponenter")));
                    int fDn2 = Convert.ToInt32(f.br.ReadDynamicCsvProperty(
                        DynamicProperty.DN2, CsvData.Get("fjvKomponenter")));
                    int sDn1 = Convert.ToInt32(s.br.ReadDynamicCsvProperty(
                        DynamicProperty.DN1, CsvData.Get("fjvKomponenter")));
                    int sDn2 = Convert.ToInt32(s.br.ReadDynamicCsvProperty(
                        DynamicProperty.DN2, CsvData.Get("fjvKomponenter")));

                    if (fDn1 == sDn1 && fDn2 == sDn2) topology.RemoveAt(i);
                }
            }
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

            //Build the sizes array
            for (int i = 0; i < topology.Count; i++)
            {
                var current = topology[i];

                double start = 0;
                double end = 0;
                var query = pipeline.GetEntitiesWithinStations(start, end)
                        .Where(x => sizeBrs.All(y => x.Id != y.Id));

                if (i == 0) //Handle the first iteration
                {
                    start = 0.0; end = current.station;
                    


                }
            }
        }
        private SizeEntryV2 GetDirectedSizeEntry(BlockReference br, IPipelineV2 pipeline, Side side)
        {
            // traverse both sides of the block and gather the objects in both directions
            throw new NotImplementedException();
        }
        /// <summary>
        /// Assuming br is a SizeArray block.
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
                DynamicProperty.Type, CsvData.Get("fjvKomponenter"), false);
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
