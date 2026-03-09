using System.Windows;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.SequenceComposer
{
    public partial class SequenceComposerWindow : Window
    {
        public SequenceComposerWindow()
        {
            InitializeComponent();
            DataContext = new SequenceComposerViewModel();
        }

        public SequenceComposerViewModel ViewModel => (SequenceComposerViewModel)DataContext;
    }
}
