using System.Reflection;
using System.Text;

using Autodesk.AutoCAD.Runtime;

namespace NorsynDistrictZones;

/// <summary>
/// Builds the load banner by REFLECTION: scans this plugin's assembly for
/// <c>[CommandMethod]</c> methods, reads each one's optional
/// <see cref="CommandSummaryAttribute"/>, and lists them sorted by name. Adding a
/// command (and decorating it) makes it appear automatically — there is no
/// hand-maintained command list to drift out of sync.
/// </summary>
internal static class CommandBanner
{
    public static string Build(Assembly pluginAssembly)
    {
        var commands = DiscoverCommands(pluginAssembly)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Pad the name column to the longest command name (+2) so the dashes line up.
        int pad = commands.Count == 0 ? 0 : commands.Max(c => c.Name.Length) + 2;

        var sb = new StringBuilder();
        sb.Append("\nNorsyn District Zones loaded — auto-zone reactor active.");
        sb.Append($"\nCommands ({commands.Count}):\n");
        foreach (var (name, summary) in commands)
        {
            sb.Append(summary.Length == 0
                ? $"  {name}\n"
                : $"  {name.PadRight(pad)}— {summary}\n");
        }
        return sb.ToString();
    }

    private static IReadOnlyList<(string Name, string Summary)> DiscoverCommands(Assembly asm)
    {
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

        var list = new List<(string Name, string Summary)>();
        foreach (var t in types)
            // NDZ commands are INSTANCE methods (unlike EnercityUtility's static ones),
            // so the Instance flag is required or the scan finds nothing.
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                var cm = m.GetCustomAttribute<CommandMethodAttribute>();
                if (cm is null) continue;
                var su = m.GetCustomAttribute<CommandSummaryAttribute>();
                list.Add((cm.GlobalName, su?.Summary ?? ""));
            }
        return list;
    }
}
