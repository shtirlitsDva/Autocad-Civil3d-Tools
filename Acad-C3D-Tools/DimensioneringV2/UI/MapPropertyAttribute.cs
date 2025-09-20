using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.UI
{
    [AttributeUsage(AttributeTargets.Property)]
    class MapPropertyAttribute : Attribute
    {
        public MapPropertyEnum Property;
        public MapPropertyAttribute(MapPropertyEnum property)
        {
            Property = property;
        }
    }
}
