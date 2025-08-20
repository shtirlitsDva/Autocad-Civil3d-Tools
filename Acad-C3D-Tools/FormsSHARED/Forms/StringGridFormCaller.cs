using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities
{
    public static class StringGridFormCaller
    {
        public static string Call(IEnumerable<string> list, string message)
        {
            var form = new Forms.StringGridForm(list, message);
            form.ShowDialog();
            return form.SelectedValue;
        }

        public static bool YesNo(string message)
        {
            var form = new Forms.StringGridForm(
                new List<string> { "Yes", "No" },
                message);
            form.ShowDialog();
            return form.SelectedValue == "Yes";
        }

        public static T? SelectEnum<T>(string message, IEnumerable<T>? excludeValues = null) where T : struct, Enum
        {
            // Get all enum values and convert to strings, sorted alphabetically
            var enumValues = Enum.GetValues<T>()
                .Where(e => excludeValues == null || !excludeValues.Contains(e))
                .Select(e => e.ToString())
                .OrderBy(s => s)
                .ToList();
            
            var form = new Forms.StringGridForm(enumValues, message);
            form.ShowDialog();
            
            // If no selection was made (e.g., escape pressed), return null
            if (string.IsNullOrEmpty(form.SelectedValue))
                return null;
            
            // Try to parse the selected string back to the enum value
            if (Enum.TryParse<T>(form.SelectedValue, out T result))
                return result;
            
            // If parsing fails, return null
            return null;
        }
    }
}
