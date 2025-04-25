using DimensioneringV2.GraphFeatures;

using Mapsui;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Themes
{
    class DefaultTheme : StyleBase, IThemeStyle, IStyle    
    {
        readonly IStyle _red = new VectorStyle
        {
            Line = new Pen(Color.Red) { Width = 3 }
        };
        readonly IStyle _yellow = new VectorStyle
        {
            Line = new Pen(Color.Yellow) { Width = 3 }
        };        

        public DefaultTheme() {}

        public IStyle? GetStyle(IFeature feature)
        {
            if (feature is AnalysisFeature f)
            {
                return f.NumberOfBuildingsConnected == 1 ? _yellow : _red;
            }
            else
            {
                return StyleProvider.BasicStyle;
            }
        }
    }
}