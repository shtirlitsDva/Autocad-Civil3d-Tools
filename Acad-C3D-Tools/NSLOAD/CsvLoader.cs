using System.Collections.Generic;
using Microsoft.VisualBasic.FileIO;

namespace NSLOAD
{
    public static class CsvLoader
    {
        public static Dictionary<string, string> Load(string csvPath)
        {
            var dict = new Dictionary<string, string>();

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
                        if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(path))
                            dict[displayName] = path;
                    }
                }
            }

            return dict;
        }
    }
}
