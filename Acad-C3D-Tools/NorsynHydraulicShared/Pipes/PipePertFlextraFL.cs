using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public class PipePertFlextraFL : PipeBase
    {
        public PipePertFlextraFL(double roughness_mm) : base(roughness_mm) { }
        protected override string Name => "PipePertPIPEFL";
        protected override PipeType PipeType => PipeType.PertFlextraFL;
        protected override string DimName => "PertPIPE ";
        public override int OrderingPriority => 1;
        protected override double PricePerStk => 27990;
        public override SegmentType[] SupportedSegmentTypes => 
            [SegmentType.Fordelingsledning];
        public override MediumTypeEnum[] SupportedMediumTypes => 
            [MediumTypeEnum.Water];

        /// <summary>
        /// PertFlextra FL: All: V=2.0, ΔP=150
        /// </summary>
        public override (double Velocity, int PressureGradient) GetDefaultAcceptCriteria(int dn, SegmentType segmentType)
        {
            return (2.0, 150);
        }
        public override string ToString() => "PertPIPE";
    }
}
