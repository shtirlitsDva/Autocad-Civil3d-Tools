using System.Windows;
using System.Windows.Data;
using DimensioneringV2.UI;
using IntersectUtilities.BatchProcessing.BPUIv2.Sequences;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.InputsDialog;

public partial class InputsDialog : Window
{
    public InputsDialogViewModel? ViewModel { get; }

    public InputsDialog(SequenceDefinition sequence)
    {
        try
        {
            ViewModel = new InputsDialogViewModel(sequence);
            DataContext = ViewModel;
            InitializeComponent();

            var view = CollectionViewSource.GetDefaultView(ViewModel.Inputs);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(InputItemViewModel.StepDisplayName)));

            Loaded += (_, _) => DarkTitleBarHelper.EnableDarkTitleBar(this);
        }
        catch (Exception ex)
        {
            prdDbg($"BPUIv2: Failed to initialize InputsDialog: {ex}");
            MessageBox.Show(
                $"Failed to open Inputs Dialog:\n{ex}",
                "BPv2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
