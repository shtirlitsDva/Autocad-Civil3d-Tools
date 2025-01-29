﻿using DimensioneringV2.GraphFeatures;

using Mapsui;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.MapStyles
{
    class StyleMapBridge_NoLabels : StyleBase
    {
        protected readonly Func<AnalysisFeature, bool> _prop;
        
        public StyleMapBridge_NoLabels(Func<AnalysisFeature, bool> prop)
        {
            _prop = prop;
        }

        public override void ApplyStyle(IEnumerable<IFeature> features)
        {
            base.ApplyStyle(features);
        }

        public override IStyle[] GetStyles(IFeature feature)
        {
            var f = feature as AnalysisFeature;
            if (f == null) return [new VectorStyle()];

            bool value = _prop(f);

            VectorStyle s1;

            if (value)
            {
                s1 = new VectorStyle
                {
                    Line = new Pen(Color.Black)
                    {
                        Width = 3
                    }
                };
            }
            else
            {
                s1 = new VectorStyle
                {

                    Line = new Pen(Color.IndianRed)
                    {
                        Width = 5
                    }
                };
            }
            
            return [s1];
        }
    }
}
