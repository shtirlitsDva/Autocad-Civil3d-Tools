using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.GraphFeatures;
using DimensioneringV2.MapCommands;

using NorsynHydraulicCalc;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DimensioneringV2.UI.FeaturePopup
{
    internal partial class FeaturePopupViewModel : ObservableObject
    {
        public ObservableCollection<PropertyItem> Properties { get; } = new();

        private readonly CollectionViewSource _groupedSource = new();
        public ICollectionView GroupedProperties => _groupedSource.View;

        [ObservableProperty]
        private bool isServiceLine;

        private AnalysisFeature? _feature;

        public FeaturePopupViewModel()
        {
            _groupedSource.Source = Properties;
            _groupedSource.GroupDescriptions.Add(
                new PropertyGroupDescription(nameof(PropertyItem.Category)));
            _groupedSource.SortDescriptions.Add(
                new SortDescription(nameof(PropertyItem.CategoryOrder), ListSortDirection.Ascending));
        }

        public void Update(AnalysisFeature feature)
        {
            _feature = feature;
            IsServiceLine = feature.SegmentType == SegmentType.Stikledning;

            Properties.Clear();
            foreach (var item in ((IInfoForFeature)feature).PropertiesToDataGrid())
                Properties.Add(item);

            _groupedSource.View.Refresh();
        }

        [RelayCommand]
        private async Task Trykprofil()
        {
            if (_feature == null) return;
            await new MapCommands.Trykprofil().Execute(_feature);
        }

        [RelayCommand]
        private void CopyValue(string? value)
        {
            if (value == null) return;
            Clipboard.SetText(value);
        }
    }
}
