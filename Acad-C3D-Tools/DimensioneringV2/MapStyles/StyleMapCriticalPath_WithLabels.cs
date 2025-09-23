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
    //class StyleMapCriticalPath_WithLabels : StyleMapCriticalPath_NoLabels
    //{
    //    public StyleMapCriticalPath_WithLabels(Func<AnalysisFeature, bool> prop) : base(prop)
    //    {
            
    //    }

    //    public override IStyle[] GetStyles(IFeature feature)
    //    {
    //        var f = feature as AnalysisFeature;
    //        if (f == null) return [new VectorStyle()];

    //        var s1 = base.GetStyles(feature).First();

    //        var value = f.PressureLossAtClient;

    //        if (value > 0.0000001)
    //        {
    //            var s2 = new LabelStyle
    //            {
    //                Text = value.ToString("0.00"),
    //                //BackColor = new Brush(_gradientHelper.LookupColor(f.NumberOfBuildingsSupplied)),
    //                ForeColor = Color.Black,
    //                //Offset = new Offset(0, 0),
    //                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
    //                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center
    //            };
    //            return [s1, s2];
    //        }
    //        else
    //        {
    //            return [s1];
    //        }
    //    }
    //}
}
