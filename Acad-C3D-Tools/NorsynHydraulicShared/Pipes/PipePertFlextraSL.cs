using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipePertFlextraSL : PipeBase
    {
        public PipePertFlextraSL(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipePertFlextraSL";
        protected override PipeType PipeType => PipeType.PertFlextraSL;
        protected override string DimName => "PertFlextra ";
        public override int OrderingPriority => 1;
        protected override double PricePerStk => 27990;
        public override SegmentType[] SupportedSegmentTypes => 
            [SegmentType.Stikledning];
        public override MediumTypeEnum[] SupportedMediumTypes => 
            [MediumTypeEnum.Water];

        /// <summary>
        /// PertFlextra SL: All: V=1.5, ΔP=1000
        /// </summary>
        public override (double Velocity, int PressureGradient) GetDefaultAcceptCriteria(int dn, SegmentType segmentType)
        {
            return (1.5, 1000);
        }
    }
}
