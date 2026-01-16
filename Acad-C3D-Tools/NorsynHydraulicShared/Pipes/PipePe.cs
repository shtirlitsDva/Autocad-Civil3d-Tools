using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipePe : PipeBase
    {
        public PipePe(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipePe";
        protected override PipeType PipeType => PipeType.Pe;
        protected override string DimName => "PE";
        public override int OrderingPriority => 1;
        protected override double PricePerStk => 13000;
        public override SegmentType[] SupportedSegmentTypes => 
            [SegmentType.Fordelingsledning, SegmentType.Stikledning];
        public override MediumTypeEnum[] SupportedMediumTypes => 
            [MediumTypeEnum.Water72Ipa28];

        /// <summary>
        /// PE FL: DN 20-150: V=1.5, ΔP=180; DN 200-300: V=2.0, ΔP=220; DN 350+: V=2.5, ΔP=220
        /// PE SL: All: V=1.0, ΔP=500
        /// </summary>
        public override (double Velocity, int PressureGradient) GetDefaultAcceptCriteria(int dn, SegmentType segmentType)
        {
            if (segmentType == SegmentType.Stikledning)
            {
                return (1.0, 500);
            }
            
            // Fordelingsledning
            if (dn <= 150)
            {
                return (1.5, 180);
            }
            if (dn <= 300)
            {
                return (2.0, 220);
            }
            return (2.5, 220);
        }
    }
}
