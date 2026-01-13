using NorsynHydraulicCalc;
using System.Collections.Generic;
using System.Windows;

namespace DimensioneringV2.UI.PipeSettings
{
    public partial class AddPipeTypeDialog : Window
    {
        public IEnumerable<PipeType> AvailablePipeTypes { get; }
        public PipeType SelectedPipeType { get; set; }

        public AddPipeTypeDialog(IEnumerable<PipeType> availablePipeTypes)
        {
            AvailablePipeTypes = availablePipeTypes;
            DataContext = this;
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);

            // Select first by default
            PipeTypeComboBox.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (PipeTypeComboBox.SelectedItem != null)
            {
                SelectedPipeType = (PipeType)PipeTypeComboBox.SelectedItem;
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
