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
    class StyleMapPipeSize_NoLabels : StyleBase
    {
        private PenWidthCalculator<int>? _penWidthCalculator;
        protected readonly Func<AnalysisFeature, Dim> _prop;

        public StyleMapPipeSize_NoLabels(Func<AnalysisFeature, Dim> prop)
        {
            _penWidthCalculator = new PenWidthCalculator<int>();

            _prop = prop;
        }

        public override void ApplyStyle(IEnumerable<IFeature> features)
        {
            var af = features.Cast<AnalysisFeature>();
            int min = af.Min(x => _prop(x).NominalDiameter)!;
            int max = af.Max(x => _prop(x).NominalDiameter)!;

            _penWidthCalculator.SetMinMaxValues(min, max);

            base.ApplyStyle(features);
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

            var rgb = _prop(f).RGB;
            Color color = Color.FromArgb(255, rgb[0], rgb[1], rgb[2]);

            var s1 = new VectorStyle
            {
                Line = new Pen(color)
                {
                    Width = _penWidthCalculator.CalculatePenWidth(value)
                }
            };
            return [s1];
        }
    }
}
