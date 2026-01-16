using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeSteel : PipeBase
    {
        public PipeSteel(double roughness_mm) : base(roughness_mm) {}
        protected override string Name => "PipeSteel";
        protected override PipeType PipeType => PipeType.Stål;
        protected override string DimName => "DN ";
        public override int OrderingPriority => 2;
        protected override double PricePerStk => 27990;
        public override SegmentType[] SupportedSegmentTypes => 
            [SegmentType.Fordelingsledning, SegmentType.Stikledning];
        public override MediumTypeEnum[] SupportedMediumTypes => 
            [MediumTypeEnum.Water];

        /// <summary>
        /// Steel FL: DN 32-200: V=2.0, ΔP=150; DN 250+: V=3.0, ΔP=100
        /// Steel SL: All: V=1.5, ΔP=1000
        /// </summary>
        public override (double Velocity, int PressureGradient) GetDefaultAcceptCriteria(int dn, SegmentType segmentType)
        {
            if (segmentType == SegmentType.Stikledning)
            {
                return (1.5, 1000);
            }
            
            // Fordelingsledning
            if (dn <= 200)
            {
                return (2.0, 150);
            }
            return (3.0, 100);
        }
    }
}
