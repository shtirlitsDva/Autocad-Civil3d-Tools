using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.Forms.PipeSettingsWpf.ViewModels
{
    internal partial class PipeSettingSystemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string systemName; // e.g. "PipeTypeCU"

        [ObservableProperty]
        private PipeSystemEnum pipeTypeSystem;

        public ObservableCollection<PipeSettingTypeViewModel> Types { get; }
            = new ObservableCollection<PipeSettingTypeViewModel>();
    }
}
