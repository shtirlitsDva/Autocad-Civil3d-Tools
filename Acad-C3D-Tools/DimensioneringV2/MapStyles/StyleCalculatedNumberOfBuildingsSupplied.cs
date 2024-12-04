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
    class StyleCalculatedNumberOfBuildingsSupplied : StyleBase
    {
        private GradientHelper _gradientHelper;
        /// <summary>
        /// Remember: Do not use the supplied collection, use inherited private collection instead.
        /// </summary>
        public StyleCalculatedNumberOfBuildingsSupplied(IEnumerable<IFeature> features) : base(features)
        {
            var af = _features.Cast<AnalysisFeature>();
            var min = af.Min(f => f.NumberOfBuildingsSupplied);
            var max = af.Max(f => f.NumberOfBuildingsSupplied);

            _gradientHelper = new GradientHelper(min, max);
        }

        public override IStyle[] GetStyles(IFeature feature)
        {
            var f = feature as AnalysisFeature;

            var s1 = new VectorStyle
            {
                Line = new Pen(_gradientHelper.LookupColor(f.NumberOfBuildingsSupplied))
                {
                    Width = 3
                }
            };
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
