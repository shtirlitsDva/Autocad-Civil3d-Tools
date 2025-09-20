using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Models.Trykprofil
{
    public class PressureProfileEntry
    {
        public double Distance { get; init; }
        public double SupplyPressure { get; init; }
        public double ReturnPressure { get; init; }
        public PressureProfileEntry(double distance, double supplyPressure, double returnPressure)
        {
            Distance = distance;
            SupplyPressure = supplyPressure;
            ReturnPressure = returnPressure;
        }
    }
}
