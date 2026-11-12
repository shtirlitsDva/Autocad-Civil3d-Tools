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
            string sourcesPath = args.Length > 0
                ? args[0]
                : @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\2025\DocsSources.txt";
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
                        string documentationStatus = m.Elements("documentation").Attributes("status")
                            .Select(attribute => attribute.Value.Trim()).DefaultIfEmpty("published").First();
                        if (!string.Equals(documentationStatus, "published", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string executionValue = m.Elements("execution").Select(element => element.Value.Trim())
                            .DefaultIfEmpty(string.Empty).First();
                        if (string.IsNullOrWhiteSpace(executionValue))
                        {
                            executionValue = string.Join(Environment.NewLine,
                                m.Elements().Where(element => element.Name.LocalName == "remarks" || element.Name.LocalName == "example")
                                    .Select(element => element.Value.Trim()).Where(value => !string.IsNullOrWhiteSpace(value)));
                        }

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
                                    Summary = summaryValue ?? "(No summary provided)",
                                    Execution = executionValue
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
            // Optional paths allow generation and checking before replacing the shared page.
            string outputHtmlPath = args.Length > 1
                ? args[1]
                : Path.Combine(@"X:\AutoCAD DRI - 01 Civil 3D\Commands", "CommandsDoc.html");

            using (var writer = new StreamWriter(outputHtmlPath))
            {
                // Basic HTML structure
                writer.WriteLine("<!DOCTYPE html>");
                writer.WriteLine("<html lang='en'>");
                writer.WriteLine("<head>");
                writer.WriteLine("  <meta charset='utf-8'/>");
                writer.WriteLine("  <title>AutoCAD Commands Documentation</title>");
                writer.WriteLine("  <meta name='viewport' content='width=device-width, initial-scale=1'/>");
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
                writer.WriteLine("    table { border-collapse: collapse; width: 100%; table-layout: fixed; border: 1px solid #444; }");
                writer.WriteLine("    th, td { border: 1px solid #444; padding: 8px 10px; text-align: left; vertical-align: top; overflow-wrap: anywhere; }");
                writer.WriteLine("    .execution { white-space: pre-line; }");
                writer.WriteLine("    .help { color: #bbb; line-height: 1.5; }");
                writer.WriteLine("    th { background: #333; color: #fff; }");
                writer.WriteLine("    .categoryRow { background: #555; font-weight: bold; }");
                //CSS END

                writer.WriteLine("  </style>");
                writer.WriteLine("</head>");

                writer.WriteLine("<body>");
                writer.WriteLine("  <h1>AutoCAD Commands Documentation</h1>");
                writer.WriteLine("  <p class='help'>Load an up-to-date version of the named library before running its commands. Expand a library below to see descriptions and execution instructions. Use F2 in AutoCAD to read the full command history. Development and superseded commands are omitted.</p>");

                // Sort assemblies by name if you like
                var sortedAssemblies = allAssembliesData.Keys.OrderBy(a => a).ToList();
                foreach (var asmName in sortedAssemblies)
                {
                    // <details> collapsible region for each assembly
                    writer.WriteLine("  <details>");
                    writer.WriteLine($"    <summary>{EscapeHtml(asmName)}</summary>");
                    writer.WriteLine("    <br/>");

                    // Insert a table
                    writer.WriteLine("    <table>");
                    writer.WriteLine("      <tr>");
                    writer.WriteLine("        <th style='width: 22%;'>Command(s)</th>");
                    writer.WriteLine("        <th style='width: 34%;'>Description</th>");
                    writer.WriteLine("        <th>Execution</th>");
                    writer.WriteLine("      </tr>");

                    // Sort categories if desired
                    var categories = allAssembliesData[asmName].Keys.OrderBy(c => c).ToList();
                    foreach (var category in categories)
                    {
                        // A row for the Category
                        writer.WriteLine("      <tr class='categoryRow'>");
                        writer.WriteLine($"        <td colspan='3'>{EscapeHtml(category)}</td>");
                        writer.WriteLine("      </tr>");

                        var commandList = allAssembliesData[asmName][category];
                        foreach (var cmdInfo in commandList.OrderBy(x => x.Command))
                        {
                            writer.WriteLine("      <tr>");
                            writer.WriteLine($"        <td>{EscapeHtml(cmdInfo.Command)}</td>");
                            writer.WriteLine($"        <td>{EscapeHtml(cmdInfo.Summary)}</td>");
                            writer.WriteLine($"        <td class='execution'>{EscapeHtml(cmdInfo.Execution)}</td>");
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
        public string Command { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Execution { get; set; } = string.Empty;
    }
}
