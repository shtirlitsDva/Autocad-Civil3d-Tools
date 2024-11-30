using Mapsui;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal class FeatureStyleService
    {
        internal static IEnumerable<IFeature> ApplyStyle(IEnumerable<IFeature> features)
        {
            Style red = new VectorStyle
            {
                Line = new Pen(Color.Red) { Width = 3 }
            };
            Style yellow = new VectorStyle
            {
                Line = new Pen(Color.Yellow) { Width = 3 }
            };


            foreach (IFeature feature in features)
            {
                bool isBuilding = (bool)feature["IsBuildingConnection"];

                Style style = isBuilding ? yellow : red;

                feature.Styles.Add(style);

                yield return feature;
            }
        }
    }
}
