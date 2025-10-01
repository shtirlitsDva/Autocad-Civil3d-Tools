using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IntersectUtilities.Forms.PipeSettingsWpf.ViewModels
{
    internal partial class PipeSettingTypeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string typeName;  // e.g. "Enkelt" or "Twin"

        // Each “size row” below it
        public ObservableCollection<PipeSettingSizeViewModel> Sizes { get; }
            = new ObservableCollection<PipeSettingSizeViewModel>();
    }
}
