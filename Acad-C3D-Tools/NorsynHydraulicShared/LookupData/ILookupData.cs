using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.LookupData
{
    internal interface ILookupData
    {
        double rho(double T);
        double cp(double T);
        double nu(double T);
        double mu(double T);
    }
}
