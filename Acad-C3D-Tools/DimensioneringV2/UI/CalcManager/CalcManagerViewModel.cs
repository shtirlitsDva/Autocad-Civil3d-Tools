using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.Models;
using DimensioneringV2.Services;

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DimensioneringV2.UI.CalcManager;

internal partial class CalcManagerViewModel : ObservableObject
{
    public ObservableCollection<HnSummaryItem> Networks { get; } = new();

    [ObservableProperty]
    private HnSummaryItem? selectedNetwork;

    public event EventHandler? CloseRequested;

    public CalcManagerViewModel()
    {
        Refresh();
    }

    private void Refresh()
    {
        Networks.Clear();
        var calculated = HydraulicNetworkManager.Instance.GetCalculatedNetworks();
        foreach (var hn in calculated)
            Networks.Add(new HnSummaryItem(hn));
    }

    public IRelayCommand LoadCommand => new RelayCommand(LoadSelected, () => SelectedNetwork != null);

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

    public IRelayCommand SaveCommand => new RelayCommand(SaveSelected, () => SelectedNetwork != null);

    private void SaveSelected()
    {
        if (SelectedNetwork == null) return;

        var doc = AcAp.DocumentManager.MdiActiveDocument;
        HydraulicNetworkStorage.Save(doc, SelectedNetwork.Hn);
        SelectedNetwork.IsSaved = true;
        Refresh();
    }

    public IRelayCommand DeleteCommand => new RelayCommand(DeleteSelected, () => SelectedNetwork != null);

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
        var calculated = manager.GetCalculatedNetworks();
        calculated.Remove(SelectedNetwork.Hn);

        if (SelectedNetwork.IsSaved)
        {
            var doc = AcAp.DocumentManager.MdiActiveDocument;
            HydraulicNetworkStorage.Delete(doc, SelectedNetwork.Id);
        }

        if (manager.ActiveNetwork == SelectedNetwork.Hn)
        {
            var last = calculated.LastOrDefault();
            if (last != null)
                manager.LoadHn(last);
        }

        Refresh();
    }
}
