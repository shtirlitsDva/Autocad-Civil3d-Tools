using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Topology
{
    internal class NtrSegmentTwin : NtrSegmentBase
    {
        public NtrSegmentTwin(
            PipeSystemEnum pipeSystem,
            PipeTypeEnum pipeType,
            PipeSeriesEnum pipeSeries,
            int dn,
            IEnumerable<Entity> entities,
            Polyline topology) :
            base(pipeSystem, pipeType, pipeSeries, dn, entities, topology)
        {

        }
    }
}
