using System.Collections.Generic;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

namespace DimensioneringV2.BBRData.Models
{
    internal enum MatchCategory
    {
        OneToOne,
        OneToMany,
        ManyToOne,
        ManyToMany,
        BbrUnmatched,
        CsvUnmatched
    }

    internal partial class MatchGroup : ObservableObject
    {
        public MatchCategory Category { get; set; }
        public string KeyValue { get; set; } = string.Empty;
        public List<BbrRowData> BbrRows { get; set; } = new();
        public List<CsvRowData> CsvRows { get; set; } = new();

        [ObservableProperty]
        private bool _isIgnored;

        /// <summary>Any group can be ignored — ignored groups never transfer data.</summary>
        public bool CanIgnore => true;

        /// <summary>
        /// For N:1 groups: true when ALL BBR rows are individually ignored.
        /// For all other groups: delegates to group-level IsIgnored.
        /// </summary>
        public bool IsFullyIgnored => Category == MatchCategory.ManyToOne
            ? BbrRows.All(r => r.IsIgnored)
            : IsIgnored;

        /// <summary>
        /// BBR rows not individually ignored. For non-N:1 groups returns all rows
        /// (group-level ignore is checked separately).
        /// </summary>
        public IReadOnlyList<BbrRowData> NonIgnoredBbrRows => Category == MatchCategory.ManyToOne
            ? BbrRows.Where(r => !r.IsIgnored).ToList()
            : BbrRows;

        /// <summary>
        /// True if this N:1 group has been narrowed to exactly 1 non-ignored BBR row
        /// (effectively 1:1) with exactly 1 CSV row — eligible for data transfer.
        /// </summary>
        public bool IsTransferableNToOne => Category == MatchCategory.ManyToOne
            && CsvRows.Count == 1 && NonIgnoredBbrRows.Count == 1;
    }

    internal class MatchResult
    {
        public List<MatchGroup> Groups { get; } = new();

        public int OneToOneCount => Groups.Count(g => g.Category == MatchCategory.OneToOne);

        /// <summary>
        /// Groups eligible for data transfer:
        /// - 1:1 groups that are NOT ignored
        /// - N:1 groups that have at least one non-ignored BBR row
        /// </summary>
        public IReadOnlyList<MatchGroup> TransferableGroups =>
            Groups.Where(g =>
                (g.Category == MatchCategory.OneToOne && !g.IsIgnored) ||
                g.IsTransferableNToOne
            ).ToList();

        /// <summary>
        /// Non-transferable groups that block the update.
        /// Excludes: 1:1 groups, fully-ignored groups, transferable N:1 groups.
        /// </summary>
        public IEnumerable<MatchGroup> UnresolvedGroups =>
            Groups.Where(g =>
                g.Category != MatchCategory.OneToOne &&
                !g.IsFullyIgnored &&
                !g.IsTransferableNToOne);

        /// <summary>True when no unresolved groups remain, allowing the update.</summary>
        public bool AllNonOneToOneIgnored => !UnresolvedGroups.Any();
    }
}
