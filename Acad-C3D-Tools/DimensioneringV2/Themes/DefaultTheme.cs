using DimensioneringV2.GraphFeatures;
using DimensioneringV2.Legend;

using Mapsui;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;

using System.Collections.Generic;

using DimensioneringV2.UI.MapProperty;
namespace DimensioneringV2.Themes
{
    class DefaultTheme : StyleBase, IThemeStyle, IStyle, ILegendSource
    {
        readonly IStyle _red = new VectorStyle
        {
            Line = new Pen(Color.Red) { Width = 3 }
        };
        readonly IStyle _yellow = new VectorStyle
        {
            Line = new Pen(Color.DarkOrange) { Width = 3 }
        };

        public DefaultTheme() { }

        private static IList<LegendItem> GetLegendItems() =>
        [
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
        ];

        public LegendElement? BuildLegendPanel() => new StackPanel
        {
            Background = new Color(255, 255, 255, 200),
            Padding = new Thickness(10, 5, 10, 5),
            Children = [
                new TextBlock
                {
                    Text = LegendTitleProvider.GetTitle(UI.MapProperty.MapPropertyEnum.Default),
                    FontSize = 16,
                    Align = LegendTextAlign.Center,
                },
                new Spacer { Height = 4 },
                new ItemList { Items = GetLegendItems() },
            ]
        };

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