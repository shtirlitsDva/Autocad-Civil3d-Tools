using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineSegmentV2 : SegmentBaseV2
    {
        public override double MidStation => (_size.StartStation + _size.EndStation) / 2;
        public override IEnumerable<Handle> Handles => _ents.Select(e => e.Handle);
        public override IEnumerable<Handle> ExternalHandles => throw new NotImplementedException();

        private SizeEntryV2 _size;
        private List<Entity> _ents;        

        internal PipelineSegmentV2(SizeEntryV2 sizeEntry, List<Entity> ents)
        {
            _size = sizeEntry;
            _ents = ents;
        }
    }
}
