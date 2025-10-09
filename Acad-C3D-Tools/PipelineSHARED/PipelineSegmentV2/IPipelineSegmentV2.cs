using Autodesk.AutoCAD.DatabaseServices;

using System.Collections.Generic;

namespace IntersectUtilities.PipelineNetworkSystem
{
    public interface IPipelineSegmentV2
    {
        internal double MidStation { get; }
        internal IEnumerable<Handle> Handles { get; }
        internal IEnumerable<Handle> ExternalHandles { get; }
        internal bool IsConnectedTo(IPipelineSegmentV2 other);
    }
}
