using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeCu : PipeBase
    {
        public PipeCu(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipeCu";
        protected override PipeType PipeType => PipeType.Kobber;
        protected override string DimName => "Cu ";
        public override int OrderingPriority => 0;
        protected override double PricePerStk => 27990;
        public override SegmentType[] SupportedSegmentTypes => 
            [SegmentType.Stikledning];
        public override MediumTypeEnum[] SupportedMediumTypes => 
            [MediumTypeEnum.Water];

        /// <summary>
        /// Copper SL: All: V=1.0, ΔP=1000
        /// </summary>
        public override (double Velocity, int PressureGradient) GetDefaultAcceptCriteria(int dn, SegmentType segmentType)
        {
            return (1.0, 1000);
        }

        public override string ToString() => "Kobberflex";
    }
}
