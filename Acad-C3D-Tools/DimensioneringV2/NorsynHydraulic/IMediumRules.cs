using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NorsynHydraulicCalc;

namespace DimensioneringV2.NorsynHydraulic
{
    interface IMediumRules
    {
        IEnumerable<PipeType> GetValidPipeTypesForSupply();
        IEnumerable<PipeType> GetValidPipeTypesForService();
        void ApplyDefaults(HydraulicSettings settings);
    }
}