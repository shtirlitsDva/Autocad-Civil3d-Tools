using Autodesk.AutoCAD.ApplicationServices;

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;

namespace PipeScheduleV2Tests
{
    internal static class PipeScheduleV2Report
    {
        public static string WriteHtmlReport(List<Ps2Result> results, string reportsDir)
        {
            string ts = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string path = Path.Combine(reportsDir, $"PS2_TestReport_{ts}.html");
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var sb = new StringBuilder();
            int passed = results.FindAll(x => x.Status == Ps2Status.Passed).Count;
            int failed = results.FindAll(x => x.Status == Ps2Status.Failed).Count;
            int errors = results.FindAll(x => x.Status == Ps2Status.Error).Count;
            int skipped = results.FindAll(x => x.Status == Ps2Status.Skipped).Count;
            sb.AppendLine("<html><head><meta charset=\"utf-8\"><style>body{font-family:sans-serif} table{border-collapse:collapse;width:100%} th,td{border:1px solid #ccc;padding:6px} .ok{color:#0a0} .fail{color:#a00} .skip{color:#888}</style></head><body>");
            sb.AppendLine($"<h2>PipeScheduleV2 Tests</h2>");
            sb.AppendLine($"<p>DWG: {WebUtility.HtmlEncode(db.Filename)}<br/>UTC: {System.DateTime.UtcNow:u}</p>");
            sb.AppendLine($"<h3>Summary</h3><ul><li>Total: {results.Count}</li><li>Passed: {passed}</li><li>Failed: {failed}</li><li>Errors: {errors}</li><li>Skipped: {skipped}</li></ul>");
            sb.AppendLine("<table><thead><tr><th>Name</th><th>Status</th><th>Duration ms</th><th>Message</th><th>StackTrace</th></tr></thead><tbody>");
            foreach (var r in results)
            {
                string cls = r.Status == Ps2Status.Passed ? "ok" : (r.Status == Ps2Status.Skipped ? "skip" : "fail");
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(r.Name)}</td>");
                sb.AppendLine($"<td class=\"{cls}\">{r.Status}</td>");
                sb.AppendLine($"<td>{r.Duration.TotalMilliseconds:F0}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(r.Message)}</td>");
                sb.AppendLine($"<td><pre>{WebUtility.HtmlEncode(r.StackTrace)}</pre></td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</body></html>");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }

        public static void WriteRawLog(List<Ps2Result> results, string logPath)
        {
            var sb = new StringBuilder();
            foreach (var r in results)
            {
                sb.AppendLine($"{r.Status}: {r.Name} - {r.Message}");
                if (!string.IsNullOrWhiteSpace(r.StackTrace)) sb.AppendLine(r.StackTrace);
            }
            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
        }
    }
}


