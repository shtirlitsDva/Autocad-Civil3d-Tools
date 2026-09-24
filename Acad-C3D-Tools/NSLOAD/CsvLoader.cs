using System;
using System.Collections.Generic;
using Microsoft.VisualBasic.FileIO;

namespace NSLOAD
{
    /// <summary>One row of the shared register.</summary>
    /// <param name="Path">The plugin DLL, or a native group's <c>*.oarx.json</c>.</param>
    /// <param name="AutoLoadByDefault">The optional third column reads
    /// <c>autoload</c>: a user who meets this app for the first time gets it with
    /// auto-load switched on. A user's own later choice always wins.</param>
    public record RegisterEntry(string Path, bool AutoLoadByDefault);

    public static class CsvLoader
    {
        private const string AutoLoadMarker = "autoload";

        public static Dictionary<string, RegisterEntry> Load(string csvPath)
        {
            var dict = new Dictionary<string, RegisterEntry>();

            using (var parser = new TextFieldParser(csvPath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(";");
                parser.HasFieldsEnclosedInQuotes = true;

                if (!parser.EndOfData)
                    parser.ReadFields();

                while (!parser.EndOfData)
                {
                    string[]? fields = parser.ReadFields();
                    if (fields != null && fields.Length >= 2)
                    {
                        string displayName = fields[0].Trim();
                        string path = fields[1].Trim();
                        bool autoLoad = fields.Length >= 3 &&
                            fields[2].Trim().Equals(AutoLoadMarker, StringComparison.OrdinalIgnoreCase);
                        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(path))
                            dict[displayName] = new RegisterEntry(path, autoLoad);
                    }
                }
            }

            return dict;
        }
    }
}
