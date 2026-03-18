using DimensioneringV2.Models;
using DimensioneringV2.Models.Report;

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DimensioneringV2.UI.ReportSettings;

public partial class HnReportSettingsDialog : Window
{
    internal ReportHnSettings Settings { get; }
    private readonly List<HydraulicNetwork> _otherHns;

    internal HnReportSettingsDialog(
        ReportHnSettings settings,
        List<HydraulicNetwork>? otherHns = null)
    {
        InitializeComponent();
        Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        Settings = settings;
        _otherHns = otherHns ?? new();

        if (_otherHns.Count == 0)
            BtnCopyFrom.Visibility = Visibility.Collapsed;

        LoadSettingsIntoUI(settings);
    }

    private void LoadSettingsIntoUI(ReportHnSettings s)
    {
        TbProjectName.Text = s.ProjectName ?? "";
        TbProjectNumber.Text = s.ProjectNumber ?? "";
        TbDocumentNumber.Text = s.DocumentNumber ?? "";
        TbAuthor.Text = s.Author ?? "";
        TbReviewer.Text = s.Reviewer ?? "";
        TbApprover.Text = s.Approver ?? "";
        TbDesignPressure.Text = s.DesignPressureBar?.ToString("F1", CultureInfo.InvariantCulture) ?? "";
        TbCoverNote.Text = s.CoverNote ?? "";
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

    private void CopyFromButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new HnSettingsPickerDialog(_otherHns);
        if (picker.ShowDialog() == true && picker.SelectedSettings != null)
        {
            LoadSettingsIntoUI(picker.SelectedSettings);
        }
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
