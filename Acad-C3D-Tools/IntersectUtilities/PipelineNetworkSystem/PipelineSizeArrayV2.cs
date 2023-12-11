using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.Collections;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
    public class PipelineSizeArrayV2OnePolyline : PipelineSizeArrayV2Base
    {
        public PipelineSizeArrayV2OnePolyline(IPipelineV2 pipeline)
        {
            Arrangement = PipelineSizesArrangement.OneSize;

            Polyline pline = pipeline.Entities.GetPolylines().First();

            //sizes.Add(new SizeEntryV2(
            //    GetPipeDN(pline),
            //    pipeline.
            //    ));

        }
    }
    public static class PipelineSizeArrayFactory
    {


        public static IPipelineSizeArrayV2 CreateSizeArray(IPipelineV2 pipeline)
        {
            //Case only one polyline in the pipeline
            if (pipeline.Entities.GetPolylines().Count() == 1) return new PipelineSizeArrayV2OnePolyline(pipeline);
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
