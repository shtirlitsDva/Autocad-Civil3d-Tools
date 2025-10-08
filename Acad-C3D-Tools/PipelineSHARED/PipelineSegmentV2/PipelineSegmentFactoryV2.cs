using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal static class PipelineSegmentFactoryV2
    {
        internal static List<IPipelineSegmentV2> Create(
            (double midStation, List<Entity> ents) group)
        {
            List<IPipelineSegmentV2> list = new();
            //First single component case
            if (group.ents.Count == 1 && group.ents.First() is BlockReference)
            {
                var br = (BlockReference)group.ents.First();
                var type = br.ReadDynamicCsvProperty(DynamicProperty.Function);

                switch (type)
                {
                    case "SizeArray":
                        list.Add(new PipelineTransitionV2(br));
                        break;
                    default:
                        break;
                }
            }
            //Pipeline segment case
            else
            {
                list.Add(new PipelineSegmentV2(group.ents));
            }

            return list;
        }
    }
}
