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
    class DefaultTheme : StyleBase, IThemeStyle, IStyle, ILegendData
    {
        readonly IStyle _red = new VectorStyle
        {
            Line = new Pen(Color.Red) { Width = 3 }
        };
        readonly IStyle _yellow = new VectorStyle
        {
            Line = new Pen(Color.DarkOrange) { Width = 3 }
        };

        public DefaultTheme() {}

        public LegendType LegendType => LegendType.Categorical;

        public string LegendTitle => LegendTitleProvider.GetTitle(UI.MapPropertyEnum.Default);

        public IList<LegendItem> Items => GetLegendItems();

        public double Max => throw new NotImplementedException();

        public double Min => throw new NotImplementedException();

        private IList<LegendItem> GetLegendItems()
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
                    SymbolColor = Color.DarkOrange,
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