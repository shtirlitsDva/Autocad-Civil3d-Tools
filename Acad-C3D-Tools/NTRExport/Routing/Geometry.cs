using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Routing
{
    internal class Geometry
    {
        internal static double GetBogRadius5D(int dn)
        {
            if (BogRadius5D.TryGetValue(dn, out double radius))
            {
                return radius;
            }
            else
            {
                throw new ArgumentException($"No BOG radius 5D defined for DN {dn}");
            }
        }

        private static readonly Dictionary<int, double> BogRadius5D = new()
        {
            { 15, 45 },
            { 20, 57 },
            { 25, 72 },
            { 32, 93 },
            { 40, 108 },
            { 50, 135 },
            { 65, 175 },
            { 80, 205 },
            { 100, 270 },
            { 125, 330 },
            { 150, 390 },
            { 200, 510 },
            { 250, 650 },
            { 300, 775 },
            { 350, 850 },
            { 400, 970 },
            { 450, 1122 },
            { 500, 1245 },
            { 550, 1000 },
            { 600, 1524 },
            { 700, 1778 },
            { 800, 2033 },
            { 900, 2285 },
            { 1000, 2540 },
        };
    }
}
