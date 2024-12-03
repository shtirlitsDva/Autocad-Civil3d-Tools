using DimensioneringV2.GraphFeatures;

using Mapsui;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    /// <summary>
    /// Shows the Supply lines in red and Service lines in yellow.
    /// </summary>
    class StyleBasic : StyleBase
    {
        public override IStyle GetStyle(IFeature feature)
        {
            var f = feature as AnalysisFeature;

            Style red = new VectorStyle
            {
                Line = new Pen(Color.Red) { Width = 3 }
            };
            Style yellow = new VectorStyle
            {
                Line = new Pen(Color.Yellow) { Width = 3 }
            };

            return f?.NumberOfBuildingsConnected == 1 ? yellow : red;
        }
    }
}
