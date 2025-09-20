using DimensioneringV2.GraphFeatures;

using Mapsui;
using Mapsui.Styles;

using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    class StyleMapPipeSize_WithLabels : StyleMapPipeSize_NoLabels
    {
        public StyleMapPipeSize_WithLabels(Func<AnalysisFeature, Dim> prop) : base(prop)
        {
            
        }

        public override IStyle[] GetStyles(IFeature feature)
        {
            var f = feature as AnalysisFeature;
            if (f == null) return [new VectorStyle()];

            int value = _prop(f).NominalDiameter;

            if (value == 0)
            {
                return new StyleDefault().GetStyles(feature);
            }

            var s1 = base.GetStyles(feature).First();

            var s2 = new LabelStyle
            {
                Text = _prop(f).DimName,
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
