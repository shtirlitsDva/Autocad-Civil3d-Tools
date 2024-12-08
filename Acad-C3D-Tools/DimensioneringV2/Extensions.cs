using Autodesk.AutoCAD.Geometry;

using DimensioneringV2.Geometry;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2
{
    internal static class Extensions
    {
        public static Point2D To2D(this Point3d pt)
        {
            return new Point2D(pt.X, pt.Y);
        }

        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                                .Cast<DescriptionAttribute>()
                                .FirstOrDefault();
            return attribute?.Description ?? value.ToString();
        }
    }
}
