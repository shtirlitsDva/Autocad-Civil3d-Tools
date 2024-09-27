using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NorsynHydraulicCalcTestApp
{
    public class RowData
    {
        public string? Address { get; set; }
        public int NumberOfUnits { get; set; }
        public int NumberOfBuildings { get; set; }
        public SegmentType Segment { get; set; }
        public double HeatingDemand { get; set; }
        public double Length { get; set; }
        public double ReynoldsFrem { get; set; }
        public double ReynoldsRetur { get; set; }
        public double FlowFrem { get; set; }
        public double FlowRetur { get; set; }
        public double GradientFrem { get; set; }
        public double GradientRetur { get; set; }
        public string? Dimension { get; set; }
        public double FrictionLossFrem { get => Length * GradientFrem; }
        public double FrictionLossRetur { get => Length * GradientRetur; }
    }
}
