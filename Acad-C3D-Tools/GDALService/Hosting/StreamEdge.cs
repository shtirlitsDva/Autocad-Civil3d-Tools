namespace GDALService.Hosting;

// TextReader.ReadLine says "the input has ended" with null. This is the one place
// that null is seen; the request loop gets a sequence of lines that simply ends.
internal static class StreamEdge
{
    public static IEnumerable<string> Lines(TextReader input)
    {
        while (input.ReadLine() is string line) { yield return line; }
    }
}
