using System.Collections.Generic;

namespace IntersectUtilities.DataManager
{
    internal class StierRecord
    {
        internal (string, string) Key { get; }
        internal string? Fremtid { get; }
        internal string? Alignments { get; }
        internal List<string> Længdeprofiler { get; } = new();
        internal List<string> Ler { get; } = new();
        internal string? Surface { get; }

        internal StierRecord(
            (string, string) key,
            string? fremtid, 
            string? alignments, 
            List<string> længdeprofiler, 
            List<string> ler, 
            string? surface)
        {
            Key = key;
            Fremtid = fremtid;
            Alignments = alignments;
            Længdeprofiler = længdeprofiler;
            Ler = ler;
            Surface = surface;
        }
    }
}
