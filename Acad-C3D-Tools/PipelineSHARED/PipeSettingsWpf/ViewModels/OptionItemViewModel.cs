using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IntersectUtilities.Forms.PipeSettingsWpf.ViewModels
{
    internal partial class OptionItemViewModel : ObservableObject
    {
        public double Value { get; }
        public string GroupName => $"Group_{_parent.Size}";
        public bool IsSelected
        {
            get => Math.Abs(_parent.SelectedOption - Value) < 0.000001;
            set
            {
                if (value)
                {
                    // If user checks this radio button,
                    // update the parent’s SelectedOption
                    _parent.SelectedOption = Value;
                }
            }
        }

        private readonly PipeSettingSizeViewModel _parent;
        public OptionItemViewModel(double value, PipeSettingSizeViewModel parent)
        {
            Value = value;
            _parent = parent;

            // Whenever the parent's SelectedOption changes,
            // we raise PropertyChanged for IsSelected so that the UI stays in sync.
            _parent.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PipeSettingSizeViewModel.SelectedOption))
                {
                    OnPropertyChanged(nameof(IsSelected));
                }
            };
        }
    }
}
