using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Geometry
{
    internal readonly struct Pt2(double x, double y) 
    { 
        public double X { get; } = x;
        public double Y { get; } = y;
    }    
}
