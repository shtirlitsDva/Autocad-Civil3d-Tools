using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.LookupData
{
    class LookupDataWater72Ipa28 : LookupDataBase
    {
        private static Dictionary<int, double> _rhoD = new Dictionary<int, double>()
        {
            { -5, 0.97 },
            { -2, 0.969 },
            { 0, 0.968 },
            { 5, 0.966 },
            { 10, 0.963 },
            { 11, 0.9626 },
            { 12, 0.9621 },
            { 13, 0.9615 },
            { 14, 0.9609 },
            { 15, 0.9603 },
            { 16, 0.9597 },
            { 17, 0.9591 },
            { 18, 0.9585 },
            { 19, 0.9578 },
            { 20, 0.957 },
        };
        protected override Dictionary<int, double> rhoD => _rhoD;
        private static Dictionary<int, double> _cpD = new Dictionary<int, double>()
        {
            { -5, 4.209 },
            { -2, 4.213 },
            { 0, 4.216 },
            { 5, 4.224 },
            { 10, 4.232 },
            { 11, 4.2332 },
            { 12, 4.2348 },
            { 13, 4.2364 },
            { 14, 4.238 },
            { 15, 4.2397 },
            { 16, 4.2413 },
            { 17, 4.2429 },
            { 18, 4.2446 },
            { 19, 4.2463 },
            { 20, 4.248 },
        };

        protected override Dictionary<int, double> cpD => _cpD;

        protected override Dictionary<int, double> nuD => throw new NotImplementedException();

        private static Dictionary<int, double> _muD = new Dictionary<int, double>()
        {
            { -5, 0.0084 },
            { -2, 0.007 },
            { 0, 0.0063 },
            { 5, 0.0049 },
            { 10, 0.0039 },
            { 11, 0.0037 },
            { 12, 0.00354 },
            { 13, 0.00339 },
            { 14, 0.00326 },
            { 15, 0.00312 },
            { 16, 0.00300 },
            { 17, 0.00288 },
            { 18, 0.00277 },
            { 19, 0.00266 },
            { 20, 0.0026 },
        };
        protected override Dictionary<int, double> muD => _muD;

        protected override int LowT => -5;
        protected override int HighT => 20;
    }
}
