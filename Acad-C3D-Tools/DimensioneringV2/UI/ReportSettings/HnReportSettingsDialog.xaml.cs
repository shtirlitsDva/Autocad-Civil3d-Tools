using DimensioneringV2.Models.Report;

using System.Globalization;
using System.Windows;

namespace DimensioneringV2.UI.ReportSettings;

public partial class HnReportSettingsDialog : Window
{
    internal ReportHnSettings Settings { get; }

    internal HnReportSettingsDialog(ReportHnSettings settings)
    {
        InitializeComponent();
        Settings = settings;

        // Pre-fill from existing settings
        TbProjectName.Text = settings.ProjectName ?? "";
        TbProjectNumber.Text = settings.ProjectNumber ?? "";
        TbDocumentNumber.Text = settings.DocumentNumber ?? "";
        TbAuthor.Text = settings.Author ?? "";
        TbReviewer.Text = settings.Reviewer ?? "";
        TbApprover.Text = settings.Approver ?? "";
        TbDesignPressure.Text = settings.DesignPressureBar?.ToString("F1", CultureInfo.InvariantCulture) ?? "";
        TbCoverNote.Text = settings.CoverNote ?? "";
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Settings.ProjectName = NullIfEmpty(TbProjectName.Text);
        Settings.ProjectNumber = NullIfEmpty(TbProjectNumber.Text);
        Settings.DocumentNumber = NullIfEmpty(TbDocumentNumber.Text);
        Settings.Author = NullIfEmpty(TbAuthor.Text);
        Settings.Reviewer = NullIfEmpty(TbReviewer.Text);
        Settings.Approver = NullIfEmpty(TbApprover.Text);
        Settings.CoverNote = NullIfEmpty(TbCoverNote.Text);

        if (double.TryParse(TbDesignPressure.Text, NumberStyles.Float,
            CultureInfo.InvariantCulture, out double dp))
            Settings.DesignPressureBar = dp;

        DialogResult = true;
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
