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
        public StyleBase() 
        {
            MinVisible = 0;
            MaxVisible = double.MaxValue;
            Enabled = true;
            Opacity = 1f;
        }

        public virtual double MinVisible { get; set; }
        public virtual double MaxVisible { get; set; }
        public virtual bool Enabled { get; set; }
        public virtual float Opacity { get; set; }

        public override bool Equals(object? obj)
        {
            if (!(obj is StyleBase style)) return false;
            return Equals(style);
        }

        public bool Equals(StyleBase? style)
        {
            if (style == null) return false;

            if (MinVisible != style.MinVisible) return false;

            if (MaxVisible != style.MaxVisible) return false;

            if (Enabled != style.Enabled) return false;

            return true;
        }

        public override int GetHashCode()
        {
            return MinVisible.GetHashCode() ^ MaxVisible.GetHashCode() ^ Enabled.GetHashCode() ^ Opacity.GetHashCode();
        }

        public static bool operator ==(StyleBase? style1, StyleBase? style2)
        {
            return Equals(style1, style2);
        }

        public static bool operator !=(StyleBase? style1, StyleBase? style2)
        {
            return !Equals(style1, style2);
        }
    }
}