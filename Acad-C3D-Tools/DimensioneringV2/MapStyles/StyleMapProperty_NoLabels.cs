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
    class StyleMapProperty_NoLabels<T> : StyleBase where T : struct, IComparable
    {
        private GradientHelper<T>? _gradientHelper;
        private PenWidthCalculator<T>? _penWidthCalculator;
        protected readonly Func<AnalysisFeature, T> _prop;
        
        public StyleMapProperty_NoLabels(Func<AnalysisFeature, T> prop)
        {
            _penWidthCalculator = new PenWidthCalculator<T>();

            _prop = prop;
        }

        public override void ApplyStyle(IEnumerable<IFeature> features)
        {
            var af = features.Cast<AnalysisFeature>();
            T min = af.Min(_prop)!;
            T max = af.Max(_prop)!;

            _gradientHelper = new GradientHelper<T>(min, max);
            _penWidthCalculator.SetMinMaxValues(min, max);

            base.ApplyStyle(features);
        }

        public override IStyle[] GetStyles(IFeature feature)
        {
            var f = feature as AnalysisFeature;
            if (f == null) return [new VectorStyle()];

            T value = _prop(f);

            if (EqualityComparer<T>.Default.Equals(value, default(T)))
            {
                return new StyleDefault().GetStyles(feature);
            }

            var s1 = new VectorStyle
            {
                Line = new Pen(_gradientHelper.GetGradientColor(value))
                {
                    Width = _penWidthCalculator.CalculatePenWidth(value)
                }
            };
            return [s1];
        }
    }
}
