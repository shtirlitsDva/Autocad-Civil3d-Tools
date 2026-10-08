using System.Text.RegularExpressions;

namespace IntersectUtilities.LerPathCrawl;

internal static class LerCrawlReferenceIdentity
{
    private static readonly Regex lerToken = new(
        @"(?:^|[^A-Z0-9])(?:LER(?:[23]D)?|[23]DLER)(?=$|[^A-Z0-9])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsLer(string referenceName, string referencePath)
    {
        // Inspect only the leaf name: a base map stored in a LER folder is still
        // a base map. The filename also supports renamed xref instances.
        string name = referenceName[(referenceName.LastIndexOf('|') + 1)..];
        int separator = Math.Max(referencePath.LastIndexOf('/'), referencePath.LastIndexOf('\\'));
        string file = referencePath[(separator + 1)..];
        return lerToken.IsMatch(name) || lerToken.IsMatch(file);
    }
}
