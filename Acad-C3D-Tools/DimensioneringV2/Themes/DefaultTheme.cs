using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Legend;

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
    class DefaultTheme : StyleBase, IThemeStyle, IStyle, ILegendItemProvider
    {
        readonly IStyle _red = new VectorStyle
        {
            Line = new Pen(Color.Red) { Width = 3 }
        };
        readonly IStyle _yellow = new VectorStyle
        {
            Line = new Pen(Color.) { Width = 3 }
        };

        public DefaultTheme() {}

        public IList<LegendItem> GetLegendItems()
        {
            return new List<LegendItem>()
            {
                new LegendItem
                {
                    Label = "Forsyningsrør",
                    SymbolColor = Color.Red,
                    SymbolLineWidth = 3
                },
                new LegendItem
                {
                    Label = "Stikrør",
                    SymbolColor = Color.Yellow,
                    SymbolLineWidth = 3
                }
            };
        }

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