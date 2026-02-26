using System.Collections.Generic;

namespace DimensioneringV2.Themes
{
    internal class ObjectKeyComparer : IEqualityComparer<object>
    {
        public static readonly ObjectKeyComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            if (x == null) return y == null;
            return x.Equals(y);
        }

        public int GetHashCode(object obj) => obj?.GetHashCode() ?? 0;
    }
}
