using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IntersectUtilities.Forms.PipeSettingsWpf.ViewModels
{
    internal partial class PipeSettingSizeViewModel : ObservableObject
    {
        [ObservableProperty]
        private int size;   // e.g. 20, 32, etc.

        // All possible lengths for this DN
        public ObservableCollection<double> Options { get; }
            = new ObservableCollection<double>();

        // Which length is chosen
        [ObservableProperty]
        private double selectedOption;
    }
}
