﻿using DimensioneringV2.GraphFeatures;

using DotSpatial.Projections.Transforms;

using Mapsui;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    class StyleMapProperty_WithLabels<T> : StyleMapProperty_NoLabels<T> where T : struct, IComparable
    {
        public StyleMapProperty_WithLabels(Func<AnalysisFeature, T> prop) : base(prop)
        {

        }

        public override IStyle[] GetStyles(IFeature feature)
        {
            var f = feature as AnalysisFeature;
            if (f == null) return [new VectorStyle()];

            T value = _prop(f);

            if (EqualityComparer<T>.Default.Equals(value, default))
            {
                return new StyleDefault().GetStyles(feature);
            }

            var s1 = base.GetStyles(feature).First();

            Color foreColor = Color.Black;
            Color backColor = Color.White;
            if (value is double d1 && double.IsNaN(d1))
            {
                foreColor = Color.Red;
                backColor = Color.Yellow;
            }            

            var s2 = new LabelStyle
            {
                Text = value is double d ? d.ToString("F2") : value.ToString(),
                //BackColor = new Brush(_gradientHelper.LookupColor(f.NumberOfBuildingsSupplied)),
                ForeColor = foreColor,
                //Offset = new Offset(0, 0),
                
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center
            };
            return [s1, s2];
        }
    }
}
