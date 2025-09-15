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
            // Ensure second dropdown is populated for the initially selected type
            Loaded += (s, e) =>
            {
                // Trigger nominals load if not already populated
                if (ViewModel.NominalDiameters.Count == 0)
                {
                    // Force property setter to fire change handler
                    var cur = ViewModel.SelectedPipeType;
                    ViewModel.SelectedPipeType = cur;
                }
            };
        }

        private void OnCloseRequested(object? sender, SelectPipeDimViewModel.CloseReason e)
        {
            ResultReason = e;
            DialogResult = e == SelectPipeDimViewModel.CloseReason.Ok;
            Close();
        }
    }
}


