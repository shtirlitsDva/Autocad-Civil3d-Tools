using DimensioneringV2.Models;

using System.Windows;

namespace DimensioneringV2.UI.Dialogs;

public partial class NewCalcDialog : Window
{
    internal NewCalcSource? Result { get; private set; }

    public NewCalcDialog()
    {
        InitializeComponent();
    }

    private void OnLoadFromCivil(object sender, RoutedEventArgs e)
    {
        Result = NewCalcSource.Civil;
        DialogResult = true;
    }

    private void OnReuseCurrent(object sender, RoutedEventArgs e)
    {
        Result = NewCalcSource.CloneCurrent;
        DialogResult = true;
    }
}
