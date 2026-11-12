using System;
using System.Collections.Generic;
using System.Text;

namespace IntersectUtilities.LineTypes
{
    /// <summary>
    /// The shape names of a compiled shape file. A linetype record stores a shape by its
    /// number and the .lin names it, so going between the two needs the file. Reads the
    /// "AutoCAD-86 shapes 1.0" layout (ltypeshp.shx and the like): the header line, the
    /// first and last number and the count, a (number, size) pair per shape, then each
    /// shape's bytes, which start with its name. A font file or a unicode one answers no names.
    /// </summary>
    public static class ShxShapes
    {
        private const string ShapesHeader = "AutoCAD-86 shapes 1.0";
        private const byte EndOfHeader = 0x1A;

        /// <summary>Number to name; empty when the bytes are not a shapes file.</summary>
        public static IReadOnlyDictionary<int, string> Names(byte[] file)
        {
            var names = new Dictionary<int, string>();
            if (file.Length < ShapesHeader.Length
                || Encoding.ASCII.GetString(file, 0, ShapesHeader.Length) != ShapesHeader)
                return names;
            int at = Array.IndexOf(file, EndOfHeader);
            if (at < 0) return names;
            at++;
            if (at + 6 > file.Length) return names;
            int count = BitConverter.ToUInt16(file, at + 4);
            at += 6;

            var index = new List<(int Number, int Size)>();
            for (int i = 0; i < count; i++)
            {
                if (at + 4 > file.Length) return names;
                index.Add((BitConverter.ToUInt16(file, at), BitConverter.ToUInt16(file, at + 2)));
                at += 4;
            }
            foreach ((int number, int size) in index)
            {
                if (at + size > file.Length) break;
                int end = at;
                while (end < at + size && file[end] != 0) end++;
                names[number] = Encoding.ASCII.GetString(file, at, end - at);
                at += size;
            }
            return names;
        }
    }
}
