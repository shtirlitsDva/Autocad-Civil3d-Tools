using System.Globalization;

using LERImporter.Host.Brx;

namespace LERImporter.Tests;

/// <summary>
/// Reads a golden file (see Golden\README.md) into the AEC stream the codec handles:
/// the AcDbObject header is dropped and each handle becomes a slot, numbered in the
/// order the handles first appear.
/// </summary>
internal sealed class GoldenStream
{
    public IReadOnlyList<AecToken> Tokens { get; }
    public IReadOnlyList<string> Handles { get; }

    private GoldenStream(IReadOnlyList<AecToken> tokens, IReadOnlyList<string> handles)
    {
        Tokens = tokens;
        Handles = handles;
    }

    public static IEnumerable<string> Definitions() =>
        Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Golden"), "def-*.txt")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal);

    public static GoldenStream Load(string fileName)
    {
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Golden", fileName));
        int body = Array.FindIndex(lines, line => line.StartsWith("I32 ", StringComparison.Ordinal)) + 1;
        Assert.True(body > 0, $"{fileName}: no AcDbObject header");

        var handles = new List<string>();
        var tokens = new List<AecToken>();
        foreach (string line in lines.Skip(body))
        {
            int space = line.IndexOf(' ');
            string kind = line[..space];
            string value = line[(space + 1)..];
            tokens.Add(kind switch
            {
                "Bool" => AecToken.Bool(bool.Parse(value)),
                "Byte" => AecToken.Byte(byte.Parse(value, CultureInfo.InvariantCulture)),
                "I16" => AecToken.Int16(short.Parse(value, CultureInfo.InvariantCulture)),
                "I32" => AecToken.Int32(int.Parse(value, CultureInfo.InvariantCulture)),
                "Dbl" => AecToken.Double(double.Parse(value, CultureInfo.InvariantCulture)),
                "Str" => AecToken.String(value),
                "SPtr" => AecToken.Soft(Slot(handles, value)),
                "HPtr" => AecToken.Hard(Slot(handles, value)),
                _ => throw new InvalidDataException($"{fileName}: unknown token '{line}'"),
            });
        }
        return new GoldenStream(tokens, handles);
    }

    private static int Slot(List<string> handles, string handle)
    {
        int slot = handles.IndexOf(handle);
        if (slot >= 0) return slot;
        handles.Add(handle);
        return handles.Count - 1;
    }
}
