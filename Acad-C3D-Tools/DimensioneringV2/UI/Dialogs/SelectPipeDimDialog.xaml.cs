using System.Windows;

namespace DimensioneringV2.UI.Dialogs
{
    public partial class SelectPipeDimDialog : Window
    {
        public SelectPipeDimViewModel ViewModel { get; }
        public SelectPipeDimViewModel.CloseReason ResultReason { get; private set; } = SelectPipeDimViewModel.CloseReason.Cancel;
        public SelectPipeDimDialog()
        {
            InitializeComponent();
            ViewModel = new SelectPipeDimViewModel();
            DataContext = ViewModel;
            ViewModel.CloseRequested += OnCloseRequested;
        }

        private void OnCloseRequested(object? sender, SelectPipeDimViewModel.CloseReason e)
        {
            ResultReason = e;
            DialogResult = e == SelectPipeDimViewModel.CloseReason.Ok;
            Close();
        }
    }
}


