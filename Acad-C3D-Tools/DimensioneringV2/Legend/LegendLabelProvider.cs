﻿using DimensioneringV2.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Legend
{
    internal static class LegendLabelProvider
    {
        public static string GetLabel(MapPropertyEnum property, object? value)
        {
            if (value == null) return "NULL";

            return property switch
            {
                //Categorical theme
                MapPropertyEnum.Bridge => (value is bool b && b) ? "Bridge edge" : "Non-bridge edge",
                MapPropertyEnum.CriticalPath => (value is bool b && b) ? "Kritisk forbruger" : "",
                MapPropertyEnum.SubGraphId => $"Sub-graph {value}",
                MapPropertyEnum.Pipe => value is string s ? s == "NA 000" ? "" : s.ToString() : "",
                
                //Not used for gradient legend

                //Not used
                MapPropertyEnum.Default => "",
                MapPropertyEnum.Basic => "",

                _ => value.ToString() ?? "Unknown",
            };
        }
    }
}