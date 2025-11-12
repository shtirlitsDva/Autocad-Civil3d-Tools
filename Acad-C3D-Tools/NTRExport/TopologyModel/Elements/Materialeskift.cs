using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;

namespace NTRExport.TopologyModel
{
    internal sealed class Materialeskift : TFitting
    {
        public Materialeskift(Handle source)
            : base(source, PipelineElementType.Materialeskift) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Materialeskift);
        }

        public override string DnLabel()
        {
            var br = _entity as BlockReference;
            if (br == null)
            {
                return "_entity is not BR!";
            }

            var dn1 = Convert.ToInt32(
                br.ReadDynamicCsvProperty(DynamicProperty.DN1));

            var dn2 = Convert.ToInt32(
                br.ReadDynamicCsvProperty(DynamicProperty.DN2));

            return $"{dn1.ToString()}/{dn2.ToString()}";
        }

        public override string DotLabelForTest()
        {
            return $"{Source.ToString()} / {this.GetType().Name}\n" +
                $"{DnLabel()}";
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double exitSlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();
            // TODO: Implement properly with elevation/slope propagation
            var pr = Ports.Take(2).ToArray();
            if (pr.Length < 2)
                return exits;
            var p1 = pr[0].Node.Pos;
            var p2 = pr[1].Node.Pos;
            var dn = topo.InferMainDn(this);
            g.Members.Add(
                new RoutedStraight(Source, this)
                {
                    A = new Point3d(p1.X, p1.Y, entryZ),
                    B = new Point3d(p2.X, p2.Y, entryZ),
                    DN = dn,
                    DnSuffix = "s",
                    Material = Material,
                    FlowRole = FlowRole.Unknown,
                }
            );
            // Propagate to other port
            var other = ReferenceEquals(entryPort, pr[0]) ? pr[1] : pr[0];
            exits.Add((other, entryZ, exitSlope));
            return exits;
        }
    }
}

