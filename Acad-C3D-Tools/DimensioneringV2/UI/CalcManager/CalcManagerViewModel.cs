using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.Models;
using DimensioneringV2.Services;

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
    }

    public event EventHandler? CloseRequested;

    public RelayCommand LoadCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public CalcManagerViewModel()
    {
        LoadCommand = new RelayCommand(LoadSelected, () => SelectedNetwork != null);
        SaveCommand = new RelayCommand(SaveSelected, () => SelectedNetwork != null);
        DeleteCommand = new RelayCommand(DeleteSelected, () => SelectedNetwork != null);
        Refresh();
    }

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
}
