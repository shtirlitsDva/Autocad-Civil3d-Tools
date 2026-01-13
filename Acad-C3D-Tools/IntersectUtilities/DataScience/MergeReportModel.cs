using System;
using System.Collections.Generic;

namespace IntersectUtilities.DataScience
{
    public class MergeReportModel
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string Styles { get; set; } = "";
        public List<SummaryCard> SummaryCards { get; set; } = new();
        public List<StageCount> MatchStages { get; set; } = new();
        public List<ReportSection> Sections { get; set; } = new();
    }

    public class SummaryCard
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string CssClass { get; set; } = ""; // "success", "warning", "error", or ""
    }

    public class StageCount
    {
        public string Stage { get; set; } = "";
        public int Count { get; set; }
        public string CssClass => $"stage-{Stage.Replace("+", "")}";
    }

    public class ReportSection
    {
        public string Title { get; set; } = "";
        public int Count { get; set; }
        public string BadgeClass { get; set; } = ""; // "success", "warning", "error"
        public bool AutoOpen => BadgeClass != "success" && Count > 0;
        public string EmptyMessage { get; set; } = "No data.";
        public List<SectionGroup> Groups { get; set; } = new();
    }

    public class SectionGroup
    {
        public string Header { get; set; } = "";
        public string Description { get; set; } = "";
        public List<DataTableModel> Tables { get; set; } = new();
    }

    public class DataTableModel
    {
        public string Label { get; set; } = "";
        public List<string> Columns { get; set; } = new();
        public List<List<CellValue>> Rows { get; set; } = new();
    }

    public class CellValue
    {
        public string Display { get; set; } = "";
        public string FullValue { get; set; } = "";
        public string CssClass { get; set; } = "";
        public bool IsBadge { get; set; }

        public static CellValue Simple(string value, int maxLength = 25)
        {
            var full = value ?? "";
            var display = full.Length > maxLength ? full.Substring(0, maxLength - 3) + "..." : full;
            return new CellValue { Display = display, FullValue = full };
        }

        public static CellValue Badge(string value, string cssClass)
        {
            return new CellValue { Display = value, FullValue = value, CssClass = cssClass, IsBadge = true };
        }
    }
}
