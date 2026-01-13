using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NorsynHydraulicCalc;

using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace DimensioneringV2.UI.PipeSettings
{
    public partial class AcceptCriteriaViewModel : ObservableObject
    {
        private readonly PipeTypePriority _priority;

        [ObservableProperty]
        private string windowTitle;

        [ObservableProperty]
        private ObservableCollection<DnAcceptCriteria> visibleCriteria;

        public AcceptCriteriaViewModel(PipeTypePriority priority)
        {
            _priority = priority;
            WindowTitle = $"Acceptkriterier for {priority.PipeType}";

            // Show only criteria within the Min-Max DN range
            VisibleCriteria = new ObservableCollection<DnAcceptCriteria>(
                _priority.AcceptCriteria
                    .Where(c => c.NominalDiameter >= priority.MinDn && c.NominalDiameter <= priority.MaxDn)
                    .OrderBy(c => c.NominalDiameter));
        }

        [RelayCommand]
        private void Close(Window window)
        {
            // Mark all visible criteria as initialized when user closes the dialog
            // (user has reviewed the values by opening this window)
            foreach (var criteria in VisibleCriteria)
            {
                criteria.IsInitialized = true;
            }

            window.Close();
        }

        [RelayCommand]
        private void ApplyToAll()
        {
            if (VisibleCriteria.Count == 0) return;

            // Use the first row's values as template
            var template = VisibleCriteria.First();

            foreach (var criteria in VisibleCriteria.Skip(1))
            {
                criteria.MaxVelocity = template.MaxVelocity;
                criteria.MaxPressureGradient = template.MaxPressureGradient;
                criteria.IsInitialized = true;
            }
        }

        [RelayCommand]
        private void MarkAllInitialized()
        {
            foreach (var criteria in VisibleCriteria)
            {
                criteria.IsInitialized = true;
            }
        }
    }
}
