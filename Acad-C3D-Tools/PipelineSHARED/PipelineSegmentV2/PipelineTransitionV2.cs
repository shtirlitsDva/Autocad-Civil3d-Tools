using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.PipelineNetworkSystem
{
    internal class PipelineTransitionV2 : SegmentBaseV2
    {
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
