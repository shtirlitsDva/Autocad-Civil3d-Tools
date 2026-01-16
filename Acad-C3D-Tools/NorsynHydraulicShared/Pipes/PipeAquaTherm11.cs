using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipeAquaTherm11 : PipeBase
    {
        public PipeAquaTherm11(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipeAquaTherm11";
        protected override PipeType PipeType => PipeType.AquaTherm11;
        protected override string DimName => "AT";
        public override int OrderingPriority => 1;
        protected override double PricePerStk => 13000;
        public override SegmentType[] SupportedSegmentTypes => 
            [SegmentType.Fordelingsledning];
        public override MediumTypeEnum[] SupportedMediumTypes => 
            [MediumTypeEnum.Water];

        /// <summary>
        /// AquaTherm FL: All: V=1.5, ΔP=100
        /// </summary>
        public override (double Velocity, int PressureGradient) GetDefaultAcceptCriteria(int dn, SegmentType segmentType)
        {
            return (1.5, 100);
        }
    }
}
