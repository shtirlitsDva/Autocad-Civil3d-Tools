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
    class StyleNumberOfBuildingsSupplied_WithLabels : StyleNumberOfBuildingsSupplied_NoLabels
    {
        public StyleNumberOfBuildingsSupplied_WithLabels()
        {
            
        }

        public override IStyle[] GetStyles(IFeature feature)
        {
            var f = feature as AnalysisFeature;

            int nr = f.NumberOfBuildingsSupplied;

            if (nr == 0)
            {
                return new StyleDefault().GetStyles(f);
            }

            var s1 = base.GetStyles(feature).First();

            var s2 = new LabelStyle
            {
                Text = f.NumberOfBuildingsSupplied.ToString(),
                //BackColor = new Brush(_gradientHelper.LookupColor(f.NumberOfBuildingsSupplied)),
                ForeColor = Color.Black,
                //Offset = new Offset(0, 0),
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center
            };
            return [s1, s2];
        }
    }
}
