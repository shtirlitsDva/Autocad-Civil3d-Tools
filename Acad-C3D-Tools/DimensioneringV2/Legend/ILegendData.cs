using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Legend
{
    internal interface ILegendData
    {
        public LegendType LegendType { get; }
        public string LegendTitle { get; }
        public IList<LegendItem> Items { get; }
        public double Max { get; }
        public double Min { get; }
    }
}
