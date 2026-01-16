using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeAluPex : PipeBase
    {
        public PipeAluPex(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipeAluPex";
        protected override PipeType PipeType => PipeType.AluPEX;
        protected override string DimName => "AluPEX ";
        public override int OrderingPriority => 0;
        protected override double PricePerStk => 27990;
        public override SegmentType[] SupportedSegmentTypes => 
            [SegmentType.Stikledning];
        public override MediumTypeEnum[] SupportedMediumTypes => 
            [MediumTypeEnum.Water];

        /// <summary>
        /// AluPEX SL: All: V=1.5, ΔP=1000
        /// </summary>
        public override (double Velocity, int PressureGradient) GetDefaultAcceptCriteria(int dn, SegmentType segmentType)
        {
            return (1.5, 1000);
        }
    }
}
