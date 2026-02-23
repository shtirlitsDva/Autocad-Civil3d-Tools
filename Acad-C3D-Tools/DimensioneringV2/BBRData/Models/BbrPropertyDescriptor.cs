using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using IntersectUtilities;

namespace DimensioneringV2.BBRData.Models
{
    internal enum BbrDataType
    {
        String,
        Int,
        Double,
        Unknown
    }

    internal class BbrPropertyDescriptor
    {
        public string Name { get; }
        public BbrDataType DataType { get; }
        public PropertyInfo PropertyInfo { get; }
        public bool IsReadOnly { get; }

        private BbrPropertyDescriptor(PropertyInfo propertyInfo, BbrDataType dataType)
        {
            Name = propertyInfo.Name;
            DataType = dataType;
            PropertyInfo = propertyInfo;
            IsReadOnly = !propertyInfo.CanWrite;
        }

        public object? GetValue(BBR bbr) => PropertyInfo.GetValue(bbr);

        public void SetValue(BBR bbr, object value)
        {
            if (IsReadOnly)
                throw new InvalidOperationException($"Property '{Name}' is read-only.");
            PropertyInfo.SetValue(bbr, value);
        }

        public override string ToString() => $"{Name} ({DataType})";

        private static readonly Lazy<IReadOnlyList<BbrPropertyDescriptor>> _all =
            new(BuildFromReflection);

        public static IReadOnlyList<BbrPropertyDescriptor> All => _all.Value;

        private static List<BbrPropertyDescriptor> BuildFromReflection()
        {
            return typeof(BBR)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.DeclaringType == typeof(BBR))
                .Select(p => (prop: p, type: MapType(p.PropertyType)))
                .Where(x => x.type != BbrDataType.Unknown)
                .Select(x => new BbrPropertyDescriptor(x.prop, x.type))
                .ToList();
        }

        private static BbrDataType MapType(Type clrType)
        {
            if (clrType == typeof(string)) return BbrDataType.String;
            if (clrType == typeof(int)) return BbrDataType.Int;
            if (clrType == typeof(double)) return BbrDataType.Double;
            return BbrDataType.Unknown;
        }

        public static BbrDataType MapTypeFromString(string typeName)
        {
            return typeName.ToLowerInvariant() switch
            {
                "string" => BbrDataType.String,
                "int" => BbrDataType.Int,
                "double" => BbrDataType.Double,
                _ => BbrDataType.Unknown
            };
        }
    }
}
