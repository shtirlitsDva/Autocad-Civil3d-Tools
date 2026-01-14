using NorsynHydraulicCalc;
using System.Collections.Generic;
using System.Windows;

namespace DimensioneringV2.UI.PipeSettings
{
    public partial class AddRuleDialog : Window
    {
        public IEnumerable<PipeType> AvailableParentPipeTypes { get; }
        public PipeType SelectedParentPipeType { get; set; }

        public AddRuleDialog(IEnumerable<PipeType> availableParentPipeTypes)
        {
            AvailableParentPipeTypes = availableParentPipeTypes;
            DataContext = this;
            InitializeComponent();
            Loaded += (s, e) => DarkTitleBarHelper.EnableDarkTitleBar(this);

            // Select first by default
            ParentPipeTypeComboBox.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (ParentPipeTypeComboBox.SelectedItem != null)
            {
                SelectedParentPipeType = (PipeType)ParentPipeTypeComboBox.SelectedItem;
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
