using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.UtilsCommon.DataManager
{
    internal class StierRecord
    {
        internal (string ProjectId, string EtapeId) Key { get; }
        internal string? Fremtid { get; }
        internal string? Alignments { get; }
        internal List<string> Længdeprofiler { get; } = new();
        internal List<string> Ler { get; } = new();
        internal string? Surface { get; }

        private IEnumerable<string> _allFiles =>
            (Fremtid != null ? [Fremtid] : Enumerable.Empty<string>())
            .Concat(Alignments != null ? [Alignments] : Enumerable.Empty<string>())
            .Concat(Surface != null ? [Surface] : Enumerable.Empty<string>())
            .Concat(Længdeprofiler)
            .Concat(Ler);

        internal bool ContainsFile(string filename) =>
            _allFiles.Any(f => string.Equals(f, filename, StringComparison.OrdinalIgnoreCase));

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
