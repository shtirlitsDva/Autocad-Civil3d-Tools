using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IntersectUtilities
{
    public static class HtmlGenerator
    {
        public static string GenerateTable(List<Dictionary<string, object>> group)
        {
            StringBuilder sb = new StringBuilder();

            // Start table
            sb.AppendLine("<table class='table table-bordered'>");

            // Header row
            sb.AppendLine("<thead>");
            sb.AppendLine("<tr>");
            sb.AppendLine("<th>Property Name</th>");
            for (int i = 1; i <= group.Count; i++)
            {
                sb.AppendLine($"<th>Object {i}</th>");
            }
            sb.AppendLine("</tr>");
            sb.AppendLine("</thead>");

            // Body
            sb.AppendLine("<tbody>");

            // Collect all property names from all objects in the group
            HashSet<string> allProperties = new HashSet<string>();
            foreach (var obj in group)
            {
                foreach (var key in obj.Keys)
                {
                    allProperties.Add(key);
                }
            }

            foreach (var property in allProperties)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{property}</td>");

                var firstValue = group[0].ContainsKey(property) ? group[0][property] : null;
                bool allSame = true;

                // Check if all values are the same for this property across the group
                foreach (var obj in group)
                {
                    if (!obj.ContainsKey(property) || !obj[property].Equals(firstValue))
                    {
                        allSame = false;
                        break;
                    }
                }

                foreach (var obj in group)
                {
                    var value = obj.ContainsKey(property) ? obj[property] : "<varies>";
                    var style = allSame ? "background-color: green;" : "";

                    sb.AppendLine($"<td style='{style}'>{value}</td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody>");

            // End table
            sb.AppendLine("</table>");

            return sb.ToString();
        }
        public static void SaveToFile(string htmlContent, string filePath)
        {
            // Add bootstrap for styling (you can download and host it locally if needed)
            string fullContent = $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>Overlapping Groups Analysis</title>
            <link href='https://maxcdn.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css' rel='stylesheet'>
        </head>
        <body>
        <div class='container mt-5'>
            {htmlContent}
        </div>
        </body>
        </html>";

            File.WriteAllText(filePath, fullContent);
        }
        public static string GenerateHtmlReport(HashSet<HashSet<SerializablePolyline3d>> groups)
        {
            StringBuilder html = new StringBuilder();
            html.Append("<html><head><style>table { border-collapse: collapse; } td, th { border: 1px solid black; padding: 8px; } hr { margin-top: 30px; }</style></head><body>");

            foreach (var group in groups)
            {
                if (group.Any())
                {
                    var representative = group.First();
                    html.Append($"<h2>Table for Group Number {representative.GroupNumber}</h2>");
                    html.Append("<table>");
                    html.Append("<thead><tr><th></th>");

                    // Headers: Handles
                    foreach (var polyline in group)
                    {
                        html.Append($"<th>{polyline.Handle}</th>");
                    }

                    html.Append("</tr></thead><tbody>");

                    // Determine all unique property keys in the group
                    var allKeys = group.SelectMany(p => p.Properties.Keys).Distinct();

                    foreach (var key in allKeys)
                    {
                        html.Append($"<tr><td>{key}</td>");

                        foreach (var polyline in group)
                        {
                            var value = polyline.Properties.TryGetValue(key, out var propValue) ? propValue.ToString() : "";
                            html.Append($"<td>{value}</td>");
                        }

                        html.Append("</tr>");
                    }

                    html.Append("</tbody></table>");
                    html.Append("<hr>");  // Separation line
                }
            }

            html.Append("</body></html>");

            return html.ToString();
        }
    }
}
