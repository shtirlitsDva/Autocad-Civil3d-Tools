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
        private IStyle _red;
        private IStyle _yellow;
        public StyleBasic(IEnumerable<IFeature> features) : base(features)
        {
            _red = new VectorStyle
            {
                Line = new Pen(Color.Red) { Width = 3 }
            };
            _yellow = new VectorStyle
            {
                Line = new Pen(Color.Yellow) { Width = 3 }
            };
        }
        public override IStyle[] GetStyles(IFeature feature)
        {
            var f = feature as AnalysisFeature;

            return f?.NumberOfBuildingsConnected == 1 ? [_yellow] : [_red];
        }
    }
}
