using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.LookupData
{
    internal interface ILookupData
    {
        double rho(int T);
        double cp(int T);
        double nu(int T);
        double mu(int T);
    }
}
