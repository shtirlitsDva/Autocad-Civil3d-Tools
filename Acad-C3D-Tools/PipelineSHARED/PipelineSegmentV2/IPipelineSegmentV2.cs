using Autodesk.AutoCAD.DatabaseServices;

using System.Collections.Generic;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public interface IPipelineSegmentV2
    {
        internal IPipelineV2 Owner { get; }
        internal double MidStation { get; }
        internal IEnumerable<Handle> Handles { get; }
        internal IEnumerable<Handle> ExternalHandles { get; }
        internal bool IsConnectedTo(IPipelineSegmentV2 other);
    }
}
