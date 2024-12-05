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
    class StyleNumberOfBuildingsSupplied_NoLabels : StyleBase
    {
        private GradientHelper? _gradientHelper;
        private PenWidthCalculator? _penWidthCalculator;
        /// <summary>
        /// Remember: Do not use the supplied collection, use inherited private collection instead.
        /// </summary>
        public StyleNumberOfBuildingsSupplied_NoLabels()
        {
            _penWidthCalculator = new PenWidthCalculator();
        }

        public override void ApplyStyle(IEnumerable<IFeature> features)
        {
            var af = features.Cast<AnalysisFeature>();
            int min = af.Min(f => f.NumberOfBuildingsSupplied);
            int max = af.Max(f => f.NumberOfBuildingsSupplied);

            _gradientHelper = new GradientHelper(min, max);
            _penWidthCalculator.SetMinMaxValues(min, max);

            base.ApplyStyle(features);
        }

        public override IStyle[] GetStyles(IFeature feature)
        {
            var f = feature as AnalysisFeature;

            int nr = f.NumberOfBuildingsSupplied;

            if (nr == 0)
            {
                return new StyleDefault().GetStyles(f);
            }

            var s1 = new VectorStyle
            {
                Line = new Pen(_gradientHelper.GetGradientColor(nr))
                {
                    Width = _penWidthCalculator.CalculatePenWidth(nr)
                }
            };
            return [s1];
        }
    }
}
