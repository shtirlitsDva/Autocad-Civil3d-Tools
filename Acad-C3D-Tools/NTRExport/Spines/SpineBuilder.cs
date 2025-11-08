using Autodesk.AutoCAD.Geometry;

using NTRExport.Enums;
using NTRExport.TopologyModel;

namespace NTRExport.Spines
{
    internal sealed class SpineBuilder
    {
        public List<SpinePath> Build(Topology topo)
        {
            var paths = new List<SpinePath>();
            int idx = 1;

            // Baseline: create one path per pipe segment to establish the 3D spine representation.
            // Later, this can be upgraded to stitch continuous pipes into longer paths and to include bends/fittings.
            foreach (var p in topo.Pipes)
            {
                var path = new SpinePath($"P{idx++:0000}");
                var a = p.A.Node.Pos;
                var b = p.B.Node.Pos;

                // Elevation propagation will adjust Z; for now keep existing Z from topology.
                var role = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem
                    ? FlowRole.Supply
                    : (p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Retur ? FlowRole.Return : FlowRole.Unknown);

                path.Add(new SpineStraight(p.Source, a, b, p.DN, role));
                paths.Add(path);
            }

            return paths;
        }
    }
}


