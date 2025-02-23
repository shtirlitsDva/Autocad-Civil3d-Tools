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

        // Which length is chosen
        [ObservableProperty]
        private double selectedOption;

        public ObservableCollection<OptionItemViewModel> Options { get; } = new();

        // If needed, call this to ensure SelectedOption is valid
        public void ValidateSelectedOption()
        {
            // If the user’s selectedOption doesn't exist in Options, pick a default
            if (Options.Count > 0 && !Options.Any(o => o.Value == SelectedOption))
            {
                SelectedOption = Options.First().Value;
            }
        }
    }
}
