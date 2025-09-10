using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Models.Trykprofil
{
    public readonly record struct PressureProfileEntry(
        double Length, double Elevation,
        double SPmVS, double RPmVS,
        double SPbar, double RPbar);
}
