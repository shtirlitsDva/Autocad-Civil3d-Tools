using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineTransitionV2 : SegmentBaseV2
    {
        public override string Label => HtmlLabel(
        [
            (Owner.Name, ""),
            ($"Transition {_br.ReadDynamicCsvProperty(DynamicProperty.System)} {MidStation:F2}", "blue"),
            ($"{_br.ReadDynamicCsvProperty(DynamicProperty.DN1)} / {_br.ReadDynamicCsvProperty(DynamicProperty.DN2)}", "red")
        ]);
        public override double MidStation => _midStation;
        public override IEnumerable<Handle> Handles => [_br.Handle];
        protected override List<Entity> _ents => [_br];

        private double _midStation;
        private BlockReference _br;

        internal PipelineTransitionV2(
            double midStation, 
            BlockReference br,
            IPipelineV2 owner,
            PropertySetHelper psh) : base(owner, psh)
        {  
            _midStation = midStation;
            _br = br; 
        }
    }
}
