using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities
{
    public static class TGridFormCaller
    {
        public static T? Call<T>(IEnumerable<T> items, Func<T, string> displayValue, string message)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (displayValue == null) throw new ArgumentNullException(nameof(displayValue));

            var list = items.ToList();
            if (list.Count == 0)
                throw new ArgumentException("Items collection cannot be empty.", nameof(items));

            var window = new Forms.TGridWindow<T>(list, displayValue, message);
            window.ShowDialog();
            return window.SelectedValue;
        }

        public static T? SelectEnum<T>(string message, IEnumerable<T>? excludeValues = null) where T : struct, Enum
        {
            var values = Enum.GetValues<T>()
                .Where(e => excludeValues == null || !excludeValues.Contains(e))
                .ToList();

            if (values.Count == 0)
                return null;

            var window = new Forms.TGridWindow<T>(values, value => value.ToString() ?? string.Empty, message);
            window.ShowDialog();
            return window.SelectedValue;
        }
    }
}
