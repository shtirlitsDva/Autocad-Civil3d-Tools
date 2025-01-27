using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace XmlToHtmlDoc
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Read a list of XML file paths from DocsSources.txt
            //    Each line in DocsSources.txt should be a full path to an .xml file.
            const string sourcesPath = @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\2025\DocsSources.txt";
            List<string> xmlPaths = File.ReadAllLines(sourcesPath)
                                        .Where(line => !string.IsNullOrWhiteSpace(line))
                                        .ToList();

            // 2. We'll store all data in a dictionary of:
            //    AssemblyName -> (Category -> List of CommandInfo)
            var allAssembliesData = new Dictionary<string, Dictionary<string, List<CommandInfo>>>();

            // Parse each XML file
            foreach (var xmlFilePath in xmlPaths)
            {
                if (!File.Exists(xmlFilePath))
                {
                    Console.WriteLine($"XML file not found: {xmlFilePath}");
                    continue;
                }

                try
                {
                    XDocument doc = XDocument.Load(xmlFilePath);

                    // 2a. Extract the assembly name:
                    //     <doc><assembly><name>YourAssemblyName</name></assembly>
                    var assemblyName = doc.Descendants("assembly")
                                          .Select(a => (string)a.Element("name"))
                                          .FirstOrDefault() ?? "UnknownAssembly";

                    // Ensure we have a place to store data for this assembly
                    if (!allAssembliesData.ContainsKey(assemblyName))
                    {
                        allAssembliesData[assemblyName] = new Dictionary<string, List<CommandInfo>>();
                    }

                    // 2b. For each <member>, see if it has <command> and <category>.
                    var members = doc.Descendants("member");
                    foreach (var m in members)
                    {
                        var commandValue = m.Element("command")?.Value?.Trim();
                        var categoryValue = m.Element("category")?.Value?.Trim();
                        var summaryValue = m.Element("summary")?.Value?.Trim();

                        // Only list if both <command> AND <category> are present
                        if (!string.IsNullOrEmpty(commandValue) && !string.IsNullOrEmpty(categoryValue))
                        {
                            if (!allAssembliesData[assemblyName].ContainsKey(categoryValue))
                            {
                                allAssembliesData[assemblyName][categoryValue] = new List<CommandInfo>();
                            }

                            allAssembliesData[assemblyName][categoryValue].Add(
                                new CommandInfo
                                {
                                    Command = commandValue,
                                    Summary = summaryValue ?? "(No summary provided)"
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading {xmlFilePath}: {ex.Message}");
                }
            }

            // 3. Generate a SINGLE HTML file with collapsible regions
            string outputHtmlPath = Path.Combine(
                //Path.GetDirectoryName(sourcesPath) ?? ".",
                @"X:\AutoCAD DRI - 01 Civil 3D\Commands",
                "CommandsDoc.html");

            using (var writer = new StreamWriter(outputHtmlPath))
            {
                // Basic HTML structure
                writer.WriteLine("<!DOCTYPE html>");
                writer.WriteLine("<html>");
                writer.WriteLine("<head>");
                writer.WriteLine("  <meta charset='utf-8'/>");
                writer.WriteLine("  <title>AutoCAD Commands Documentation</title>");
                // Minimal inline CSS for readability
                //CSS START
                writer.WriteLine("  <style>");
                writer.WriteLine("    /* Dark background, light text */");
                writer.WriteLine("    body { background: #121212; color: #f1f1f1; font-family: Arial, sans-serif; margin: 20px; }");
                writer.WriteLine("    h1 { margin-bottom: 1em; }");
                writer.WriteLine("    details { margin-bottom: 1em; }");
                writer.WriteLine("    summary { font-weight: bold; font-size: 1.1em; cursor: pointer; }");

                /* 
                   For distinct grid lines:
                   - We use border-collapse and a darker border color (#444).
                   - We also use a background on <th> and .categoryRow 
                     that stands out on the dark background.
                */
                writer.WriteLine("    table { border-collapse: collapse; margin-left: 1em; width: 90%; border: 1px solid #444; }");
                writer.WriteLine("    th, td { border: 1px solid #444; padding: 6px 10px; }");
                writer.WriteLine("    th { background: #333; color: #fff; }");
                writer.WriteLine("    .categoryRow { background: #555; font-weight: bold; }");
                //CSS END

                writer.WriteLine("  </style>");
                writer.WriteLine("</head>");

                writer.WriteLine("<body>");
                writer.WriteLine("  <h1>AutoCAD Commands Documentation</h1>");

                // Sort assemblies by name if you like
                var sortedAssemblies = allAssembliesData.Keys.OrderBy(a => a).ToList();
                foreach (var asmName in sortedAssemblies)
                {
                    // <details> collapsible region for each assembly
                    writer.WriteLine("  <details>");
                    writer.WriteLine($"    <summary>Assembly: {asmName}</summary>");
                    writer.WriteLine("    <br/>");

                    // Insert a table
                    writer.WriteLine("    <table>");
                    writer.WriteLine("      <tr>");
                    writer.WriteLine("        <th style='width: 30%;'>Command(s)</th>");
                    writer.WriteLine("        <th>Summary</th>");
                    writer.WriteLine("      </tr>");

                    // Sort categories if desired
                    var categories = allAssembliesData[asmName].Keys.OrderBy(c => c).ToList();
                    foreach (var category in categories)
                    {
                        // A row for the Category
                        writer.WriteLine("      <tr class='categoryRow'>");
                        writer.WriteLine($"        <td colspan='2'>{category}</td>");
                        writer.WriteLine("      </tr>");

                        var commandList = allAssembliesData[asmName][category];
                        foreach (var cmdInfo in commandList)
                        {
                            writer.WriteLine("      <tr>");
                            writer.WriteLine($"        <td>{EscapeHtml(cmdInfo.Command)}</td>");
                            writer.WriteLine($"        <td>{EscapeHtml(cmdInfo.Summary)}</td>");
                            writer.WriteLine("      </tr>");
                        }
                    }

                    writer.WriteLine("    </table>");
                    writer.WriteLine("  </details>");
                }

                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }

            Console.WriteLine($"Documentation generated in a single file: {outputHtmlPath}");
        }

        // Simple helper to avoid HTML injection or broken markup
        private static string EscapeHtml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }

    // Data model for storing command + summary
    public class CommandInfo
    {
        public string Command { get; set; }
        public string Summary { get; set; }
    }
}
