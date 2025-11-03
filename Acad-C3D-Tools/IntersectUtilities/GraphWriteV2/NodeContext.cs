using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.GraphWriteV2
{
    internal sealed class NodeContext
    {
        public Handle Handle { get; }
        public Entity Owner { get; }
        public string Alignment { get; }
        public string TypeLabel { get; }
        public string SystemLabel { get; }
        public string DnLabel { get; }
        public int LargestDn { get; }

        public NodeContext(
            Handle handle,
            Entity owner,
            string alignment,
            string typeLabel,
            string systemLabel,
            string dnLabel,
            int largestDn)
        {
            Handle = handle;
            Owner = owner;
            Alignment = alignment;
            TypeLabel = typeLabel;
            SystemLabel = systemLabel;
            DnLabel = dnLabel;
            LargestDn = largestDn;
        }
    }
}