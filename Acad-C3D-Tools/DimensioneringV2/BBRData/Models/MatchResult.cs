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
    }

    internal class MatchResult
    {
        public List<MatchGroup> Groups { get; } = new();

        public int OneToOneCount => Groups.Count(g => g.Category == MatchCategory.OneToOne);

        /// <summary>OneToOne groups that are NOT ignored — only these receive data transfer.</summary>
        public IReadOnlyList<MatchGroup> TransferableGroups =>
            Groups.Where(g => g.Category == MatchCategory.OneToOne && !g.IsIgnored).ToList();

        /// <summary>Non-OneToOne groups that are NOT ignored — these block the update.</summary>
        public IEnumerable<MatchGroup> UnresolvedGroups =>
            Groups.Where(g => g.Category != MatchCategory.OneToOne && !g.IsIgnored);

        /// <summary>True when every non-1:1 group has been ignored, allowing the update.</summary>
        public bool AllNonOneToOneIgnored => !UnresolvedGroups.Any();
    }
}
