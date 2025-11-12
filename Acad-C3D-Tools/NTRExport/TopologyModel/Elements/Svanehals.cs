using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;

namespace NTRExport.TopologyModel
{
    internal sealed class Svanehals : TFitting
    {
        public Svanehals(Handle source)
            : base(source, PipelineElementType.Svanehals) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Svanehals);
        }

        internal override void AttachPropertySet()
        {
            var ntr = new NtrData(_entity);
        }

        public override string DotLabelForTest()
        {
            return $"{Source.ToString()} / {this.GetType().Name}\n{DnLabel()}";
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

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            // For later
            return base.Route(g, topo, ctx, entryPort, entryZ, entrySlope);
        }
    }
}

