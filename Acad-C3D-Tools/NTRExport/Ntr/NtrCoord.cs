using NTRExport.Geometry;
using NTRExport.TopologyModel;

using System;
using System.Linq;

namespace NTRExport.Ntr
{
    internal static class NtrCoord
    {
        // Offsets in meters to translate model space before mm scaling
        public static double OffsetX { get; private set; } = 0.0;
        public static double OffsetY { get; private set; } = 0.0;

        public static void InitFromTopology(Topology topo, double marginMeters = 0.0)
        {
            if (topo.Nodes.Count == 0) { OffsetX = 0.0; OffsetY = 0.0; return; }
            var minX = topo.Nodes.Min(n => n.Pos.X);
            var minY = topo.Nodes.Min(n => n.Pos.Y);
            OffsetX = minX - marginMeters;
            OffsetY = minY - marginMeters;
        }
    }
}


