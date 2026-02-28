using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities
{
    public static class StringGridFormCaller
    {
        public static string Call(IEnumerable<string> list, string message)
        {
            if (list == null || !list.Any())
                throw new ArgumentException("List cannot be null or empty.", nameof(list));
            var window = new Forms.StringGridWindow(list, message);
            window.ShowDialog();
            return window.SelectedValue;
        }

        public static bool YesNo(string message)
        {
            var window = new Forms.StringGridWindow(
                new List<string> { "Yes", "No" },
                message);
            window.ShowDialog();
            return window.SelectedValue == "Yes";
        }

        public static T? SelectEnum<T>(string message, IEnumerable<T>? excludeValues = null) where T : struct, Enum
        {
            var enumValues = Enum.GetValues<T>()
                .Where(e => excludeValues == null || !excludeValues.Contains(e))
                .Select(e => e.ToString())
                .OrderBy(s => s)
                .ToList();

            var window = new Forms.StringGridWindow(enumValues, message);
            window.ShowDialog();

            if (string.IsNullOrEmpty(window.SelectedValue))
                return null;

            if (Enum.TryParse<T>(window.SelectedValue, out T result))
                return result;

            return null;
        }
    }
}
