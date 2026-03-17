using DimensioneringV2.Models.Report;

using MessagePack;

using System;
using System.Linq;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal class ReportHnSettingsMsgDto
{
    [Key(0)] internal string? ProjectName { get; set; }
    [Key(1)] internal string? ProjectNumber { get; set; }
    [Key(2)] internal string? DocumentNumber { get; set; }
    [Key(3)] internal string? Author { get; set; }
    [Key(4)] internal string? Reviewer { get; set; }
    [Key(5)] internal string? Approver { get; set; }
    [Key(6)] internal string? CoverNote { get; set; }
    [Key(7)] internal double? DesignPressureBar { get; set; }
    [Key(8)] internal VersionHistoryEntryMsgDto[] VersionHistory { get; set; } = Array.Empty<VersionHistoryEntryMsgDto>();

    internal static ReportHnSettingsMsgDto FromDomain(ReportHnSettings settings)
    {
        return new ReportHnSettingsMsgDto
        {
            ProjectName = settings.ProjectName,
            ProjectNumber = settings.ProjectNumber,
            DocumentNumber = settings.DocumentNumber,
            Author = settings.Author,
            Reviewer = settings.Reviewer,
            Approver = settings.Approver,
            CoverNote = settings.CoverNote,
            DesignPressureBar = settings.DesignPressureBar,
            VersionHistory = settings.VersionHistory
                .Select(VersionHistoryEntryMsgDto.FromDomain)
                .ToArray()
        };
    }

    internal ReportHnSettings ToDomain()
    {
        return new ReportHnSettings
        {
            ProjectName = ProjectName,
            ProjectNumber = ProjectNumber,
            DocumentNumber = DocumentNumber,
            Author = Author,
            Reviewer = Reviewer,
            Approver = Approver,
            CoverNote = CoverNote,
            DesignPressureBar = DesignPressureBar,
            VersionHistory = VersionHistory
                .Select(v => v.ToDomain())
                .ToList()
        };
    }
}

[MessagePackObject]
internal class VersionHistoryEntryMsgDto
{
    [Key(0)] internal string? Revision { get; set; }
    [Key(1)] internal string? Date { get; set; }
    [Key(2)] internal string? Description { get; set; }
    [Key(3)] internal string? PerformedBy { get; set; }
    [Key(4)] internal string? ReviewedBy { get; set; }

    internal static VersionHistoryEntryMsgDto FromDomain(VersionHistoryEntry entry)
    {
        return new VersionHistoryEntryMsgDto
        {
            Revision = entry.Revision,
            Date = entry.Date,
            Description = entry.Description,
            PerformedBy = entry.PerformedBy,
            ReviewedBy = entry.ReviewedBy
        };
    }

    internal VersionHistoryEntry ToDomain()
    {
        return new VersionHistoryEntry
        {
            Revision = Revision,
            Date = Date,
            Description = Description,
            PerformedBy = PerformedBy,
            ReviewedBy = ReviewedBy
        };
    }
}
