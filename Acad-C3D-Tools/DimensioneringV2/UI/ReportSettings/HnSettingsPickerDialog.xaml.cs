using DimensioneringV2.Models;
using DimensioneringV2.Models.Report;

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DimensioneringV2.UI.ReportSettings;

public partial class HnSettingsPickerDialog : Window
{
    private readonly List<HydraulicNetwork> _networks;
    internal ReportHnSettings? SelectedSettings { get; private set; }

    internal HnSettingsPickerDialog(List<HydraulicNetwork> networks)
    {
        InitializeComponent();
        Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        _networks = networks;
        LbNetworks.ItemsSource = _networks;
    }

    private void LbNetworks_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LbNetworks.SelectedItem is HydraulicNetwork hn && hn.ReportSettings != null)
        {
            var s = hn.ReportSettings;
            TbPreview.Text =
                $"Projektnavn: {s.ProjectName ?? "–"}\n" +
                $"Projekt nr.: {s.ProjectNumber ?? "–"}\n" +
                $"Dokument nr.: {s.DocumentNumber ?? "–"}\n" +
                $"Udarbejdet af: {s.Author ?? "–"}\n" +
                $"Kontrolleret af: {s.Reviewer ?? "–"}\n" +
                $"Godkendt af: {s.Approver ?? "–"}\n" +
                $"Designtryk: {s.DesignPressureBar?.ToString("F1") ?? "–"} bar\n" +
                $"Bemærkning: {s.CoverNote ?? "–"}";
            BtnSelect.IsEnabled = true;
        }
        else
        {
            TbPreview.Text = "";
            BtnSelect.IsEnabled = false;
        }
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (LbNetworks.SelectedItem is HydraulicNetwork hn)
        {
            SelectedSettings = hn.ReportSettings;
            DialogResult = true;
        }
    }
}
