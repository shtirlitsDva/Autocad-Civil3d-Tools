using System;
using System.Collections.Generic;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public interface IPipe
    {
        Dim GetDim(int dia);
    }
}
