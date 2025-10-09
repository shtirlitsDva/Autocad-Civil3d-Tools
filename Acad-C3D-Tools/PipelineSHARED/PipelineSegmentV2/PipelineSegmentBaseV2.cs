using Autodesk.AutoCAD.DatabaseServices;

using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal abstract class SegmentBaseV2 : IPipelineSegmentV2
    {
        public abstract double MidStation { get; }
        public abstract IEnumerable<Handle> Handles { get; }
        public IEnumerable<Handle> ExternalHandles {
            get => _ents.SelectMany(x => GetOtherHandles(ReadConnection(x)))
                .Where(x => _ents.All(y => y.Handle != x)).Distinct();
        }
        protected abstract IEnumerable<Entity> _ents { get; }
        public bool IsConnectedTo(IPipelineSegmentV2 other)
        {
            return other.ExternalHandles.Any(x => Handles.Any(y => x == y));
        }
    }
}
