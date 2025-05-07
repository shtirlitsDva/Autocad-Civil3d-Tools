using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Accessibility;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI;

using Mapsui.Styles;

namespace DimensioneringV2.Labels
{
    class LabelManager
    {
        public static LabelStyle? GetLabelStyle(MapPropertyEnum prop)
        {
            if (prop == MapPropertyEnum.Default) return null;

            var labelStyle = new LabelStyle
            {
                //LabelColumn = AnalysisFeature.GetAttributeName(prop),

                LabelMethod = (feature) =>
                {
                    var attrName = AnalysisFeature.GetAttributeName(prop);
                    var value = feature[attrName];

                    return prop switch
                    {
                        MapPropertyEnum.UtilizationRate => value is double d ? $"{d * 100:F0}%" : value?.ToString(),
                        _ => value switch
                        {
                            double d => d.ToString("F2"),
                            float f => f.ToString("F2"),
                            int i => i.ToString(),
                            string s => s,
                            _ => value?.ToString()
                        }
                    };
                },

                ForeColor = Color.Black,
                Font = new Font() { FontFamily = "Arial", Size = 10 },
                BackColor = new Brush() { Color = Color.White, FillStyle = FillStyle.Solid },
                Halo = new Pen() { Color = Color.White, Width = 2 },
                HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
                CollisionDetection = true,
            };

            return labelStyle;
        }
    }
}
