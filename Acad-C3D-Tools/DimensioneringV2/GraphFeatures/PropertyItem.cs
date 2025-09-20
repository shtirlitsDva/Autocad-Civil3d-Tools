using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphFeatures
{
    internal class PropertyItem
    {
        public string Name { get; }
        public object? Value { get; }
        public PropertyItem(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
