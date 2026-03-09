using System.Windows.Controls;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI
{
    public partial class BatchProcessingControl : UserControl
    {
        public BatchProcessingControl()
        {
            InitializeComponent();
            DataContext = new BatchProcessingViewModel();
        }

        public BatchProcessingViewModel ViewModel => (BatchProcessingViewModel)DataContext;
    }
}
