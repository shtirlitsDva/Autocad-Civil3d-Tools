using CommunityToolkit.Mvvm.ComponentModel;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.ViewModels
{
    public partial class IntersectionVisualizationViewModel : ObservableObject
    {
        [ObservableProperty]
        private IntersectionVisualizationModel? _visualizationModel;
        
        [ObservableProperty]
        private bool _isVisible = true;
        
        [ObservableProperty]
        private string _statusMessage = string.Empty;
        
        /// <summary>
        /// Updates the visualization with new data
        /// </summary>
        public void UpdateVisualization(IntersectionVisualizationModel model)
        {
            VisualizationModel = model;
            OnPropertyChanged(nameof(WorkingLineLength));
            OnPropertyChanged(nameof(IntersectionCount));
            OnPropertyChanged(nameof(DistanceMeasurementCount));
        }
        
        /// <summary>
        /// Clears the visualization
        /// </summary>
        public void ClearVisualization()
        {
            VisualizationModel = null;
            OnPropertyChanged(nameof(WorkingLineLength));
            OnPropertyChanged(nameof(IntersectionCount));
            OnPropertyChanged(nameof(DistanceMeasurementCount));
        }
        
        /// <summary>
        /// Shows the visualization
        /// </summary>
        public void Show()
        {
            IsVisible = true;
        }
        
        /// <summary>
        /// Hides the visualization
        /// </summary>
        public void Hide()
        {
            IsVisible = false;
        }
        
        /// <summary>
        /// Gets the working line display length
        /// </summary>
        public double WorkingLineLength => VisualizationModel?.WorkingLine?.DisplayLength ?? 0.0;
        
        /// <summary>
        /// Gets the intersection count
        /// </summary>
        public int IntersectionCount => VisualizationModel?.Intersections?.Count ?? 0;
        
        /// <summary>
        /// Gets the distance measurement count
        /// </summary>
        public int DistanceMeasurementCount => VisualizationModel?.DistanceMeasurements?.Count ?? 0;
    }
}
