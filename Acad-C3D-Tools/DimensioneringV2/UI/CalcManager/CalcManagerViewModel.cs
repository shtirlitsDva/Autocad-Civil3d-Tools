using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.MapCommands;
using DimensioneringV2.Models;
using DimensioneringV2.Services;
using DimensioneringV2.Services.Report;

using System;
using System.Collections.ObjectModel;
using System.Windows;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.UI.CalcManager;

internal partial class CalcManagerViewModel : ObservableObject
{
    public ObservableCollection<HnSummaryItem> Networks { get; } = new();

    [ObservableProperty]
    private HnSummaryItem? selectedNetwork;

    partial void OnSelectedNetworkChanged(HnSummaryItem? value)
    {
        LoadCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        BrowseSettingsCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        ExportDimsCommand.NotifyCanExecuteChanged();
        WriteToDwgCommand.NotifyCanExecuteChanged();
        GenerateReportCommand.NotifyCanExecuteChanged();
    }

    public event EventHandler? CloseRequested;

    public RelayCommand LoadCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand BrowseSettingsCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand ExportDimsCommand { get; }
    public RelayCommand WriteToDwgCommand { get; }
    public RelayCommand GenerateReportCommand { get; }

    public CalcManagerViewModel()
    {
        LoadCommand = new RelayCommand(LoadSelected, () => SelectedNetwork != null);
        SaveCommand = new RelayCommand(SaveSelected, () => SelectedNetwork != null);
        DeleteCommand = new RelayCommand(DeleteSelected, () => SelectedNetwork != null);
        BrowseSettingsCommand = new RelayCommand(BrowseSettings, () => SelectedNetwork != null);
        ExportCommand = new RelayCommand(ExportSelected, () => SelectedNetwork != null);
        ImportCommand = new RelayCommand(ImportFromFile);
        ExportDimsCommand = new RelayCommand(ExportDims, () => SelectedNetwork != null);
        WriteToDwgCommand = new RelayCommand(WriteToDwg, () => SelectedNetwork != null);
        GenerateReportCommand = new RelayCommand(GenerateReport, () => SelectedNetwork != null);
        Refresh();

        HydraulicNetworkManager.Instance.CalculationsFinished += OnCalculationsFinished;
    }

    public void Cleanup()
    {
        HydraulicNetworkManager.Instance.CalculationsFinished -= OnCalculationsFinished;
    }

    private void OnCalculationsFinished(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        Networks.Clear();
        var calculated = HydraulicNetworkManager.Instance.GetCalculatedNetworks();
        foreach (var hn in calculated)
            Networks.Add(new HnSummaryItem(hn));
    }

    private void LoadSelected()
    {
        if (SelectedNetwork == null) return;

        var result = MessageBox.Show(
            "Indlæsning overskriver nuværende indstillinger.\nFortsæt?",
            "Indlæs beregning",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        HydraulicNetworkManager.Instance.LoadHn(SelectedNetwork.Hn);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SaveSelected()
    {
        if (SelectedNetwork == null) return;

        var doc = AcAp.DocumentManager.MdiActiveDocument;
        HydraulicNetworkStorage.Save(doc, SelectedNetwork.Hn);
        Refresh();
    }

    private void DeleteSelected()
    {
        if (SelectedNetwork == null) return;

        var result = MessageBox.Show(
            $"Slet beregning '{SelectedNetwork.Id}'?\nDette kan ikke fortrydes.",
            "Slet beregning",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK) return;

        var manager = HydraulicNetworkManager.Instance;
        var isActive = manager.ActiveNetwork == SelectedNetwork.Hn;

        manager.RemoveNetwork(SelectedNetwork.Hn);

        if (SelectedNetwork.IsSaved)
        {
            var doc = AcAp.DocumentManager.MdiActiveDocument;
            HydraulicNetworkStorage.Delete(doc, SelectedNetwork.Id);
        }

        Refresh();
    }

    private void BrowseSettings()
    {
        if (SelectedNetwork == null) return;

        var frozen = SelectedNetwork.Hn.FrozenSettings;
        if (frozen == null)
        {
            MessageBox.Show(
                "Denne beregning har ingen gemte indstillinger.",
                "Ingen indstillinger",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SettingsBrowserDialog(frozen);
        dialog.ShowDialog();
    }

    private void ExportSelected()
    {
        if (SelectedNetwork == null) return;
        new SaveResult().Execute(SelectedNetwork.Hn);
    }

    private void ImportFromFile()
    {
        new LoadResult().Execute();
        Refresh();
    }

    private void ExportDims()
    {
        if (SelectedNetwork == null) return;
        new Dim2ImportDims().Execute(SelectedNetwork.Hn.Graphs);
    }

    private void WriteToDwg()
    {
        if (SelectedNetwork == null) return;
        new MapCommands.Write2Dwg().Execute(SelectedNetwork.Hn.Graphs);
    }

    private void GenerateReport()
    {
        if (SelectedNetwork == null) return;

        var hn = SelectedNetwork.Hn;

        // Ensure report profiles are loaded
        ReportProfileService.Instance.LoadFromActiveDocument();
        var profile = ReportProfileService.Instance.CurrentProfile;

        // If HN has no report settings yet, create default ones
        if (hn.ReportSettings == null)
        {
            hn.ReportSettings = new Models.Report.ReportHnSettings
            {
                Author = Environment.UserName,
            };
        }

        ReportOrchestrator.Generate(hn, profile);
    }
}
