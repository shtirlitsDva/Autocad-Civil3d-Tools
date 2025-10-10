using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;

using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineSegmentV2 : SegmentBaseV2
    {
        public override string Label => HtmlLabel([            
            ($"{_size.StartStation:F2}-{_size.EndStation:F2}","blue"),
            ($"{_size.Type} {_size.SizePrefix} {_size.DN}", "red"),]);
        public override double MidStation => (_size.StartStation + _size.EndStation) / 2;
        public override IEnumerable<Handle> Handles => _ents.Select(e => e.Handle);

        private SizeEntryV2 _size;
        protected override List<Entity> _ents { get; }

        internal PipelineSegmentV2(
            SizeEntryV2 sizeEntry,
            List<Entity> ents,
            IPipelineV2 owner,
            PropertySetHelper psh) : base(owner, psh)
        {
            _size = sizeEntry;
            _ents = ents.ToList();
        }
    }
}
