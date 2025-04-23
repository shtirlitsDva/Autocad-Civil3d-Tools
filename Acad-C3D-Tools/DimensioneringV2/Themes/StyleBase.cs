using DimensioneringV2.GraphFeatures;

using Mapsui;
using Mapsui.Extensions;
using Mapsui.Styles;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Themes
{
    internal abstract class StyleBase : IStyle
    {        
        protected StyleBase() { }

        public abstract double MinVisible { get; set; }
        public abstract double MaxVisible { get; set; }
        public abstract bool Enabled { get; set; }
        public abstract float Opacity { get; set; }
    }
}