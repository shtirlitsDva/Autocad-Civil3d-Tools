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
        internal static IPipelineSegmentV2 Create(
            (double midStation, List<Entity> ents) group)
        {
            var (midStation, ents) = group;

            //First single component case
            if (ents.Count == 1 && ents.First() is BlockReference)
            {
                var br = (BlockReference)ents.First();
                var type = br.ReadDynamicCsvProperty(DynamicProperty.Function);

                switch (type)
                {
                    case "SizeArray":
                        return new PipelineTransitionV2(midStation, br);
                    default:
                        throw new Exception(
                            $"Unknown single entity type '{type}' in PipelineSegmentFactoryV2!" );
                }
            }
            //Pipeline segment case
            else
            {
                return new PipelineSegmentV2(midStation, group.ents);
            }            
        }
    }
}
