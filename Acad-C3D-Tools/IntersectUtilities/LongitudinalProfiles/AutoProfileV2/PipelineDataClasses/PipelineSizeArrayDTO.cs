using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2.PipelineDataClasses
{
    internal class PipelineSizeArrayDTO
    {
        public double[][] PipelineSizeArrayData;

        public PipelineSizeArrayDTO(IPipelineSizeArrayV2? sizeArray)
        {
            if (sizeArray == null) throw new Exception(
                $"Null pipeline size array passed to {nameof(PipelineSizeArrayDTO)} constructor!");

            if (sizeArray.Length == 0) throw new Exception(
                $"Size array of length 0 passed to {nameof(PipelineSizeArrayDTO)} constructor!");

            List<double[]> sizeArrayData = new();

            for (int i = 0; i < sizeArray.Length; i++)
            {
                var cs = sizeArray[i];
                sizeArrayData.Add([cs.StartStation, cs.EndStation, cs.VerticalMinRadius, cs.Kod]);
            }

            PipelineSizeArrayData = sizeArrayData.ToArray();
        }
    }
}
