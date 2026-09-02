using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// The editable state of one profile. This is the source of truth while the window is
    /// open - <see cref="ToModel"/> projects it back to a serialisable
    /// <see cref="VertexCirclesProfile"/> whenever the settings need to be applied or saved.
    /// Any edit anywhere in the profile, including inside the layer filter rows, surfaces as
    /// <see cref="Changed"/> so the owner can push a live preview.
    /// </summary>
    public partial class VertexCirclesProfileViewModel : ObservableObject
    {
        /// <summary>Raised for every edit, including edits inside <see cref="LayerFilters"/>.</summary>
        public event EventHandler? Changed;

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string radiusText;

        [ObservableProperty]
        private string lineWeightFactorText;

        [ObservableProperty]
        private bool useFixedColor;

        [ObservableProperty]
        private string fixedColor;

        private double _radius;
        private double _lineWeightFactor;

        public ObservableCollection<LayerFilterViewModel> LayerFilters { get; } = new();

        public VertexCirclesProfileViewModel(VertexCirclesProfile profile)
        {
            name = profile.Name;

            _radius = profile.Settings.Radius;
            _lineWeightFactor = profile.Settings.LineWeightFactor;

            radiusText = NumberText.Format(_radius);
            lineWeightFactorText = NumberText.Format(_lineWeightFactor);

            useFixedColor = profile.Settings.ColorMode == MarkerColorMode.FixedColor;
            fixedColor = profile.Settings.FixedColor;

            foreach (string filter in profile.Settings.LayerFilters)
                LayerFilters.Add(new LayerFilterViewModel(filter));

            foreach (LayerFilterViewModel item in LayerFilters)
                item.PropertyChanged += OnLayerFilterChanged;

            LayerFilters.CollectionChanged += OnLayerFiltersCollectionChanged;
        }

        /// <summary>True while both numeric fields hold something parseable and positive.</summary>
        public bool IsValid => IsRadiusValid && IsLineWeightFactorValid;

        /// <summary>Drives the warning line in the window.</summary>
        public bool IsInvalid => !IsValid;

        public bool IsRadiusValid =>
            NumberText.TryParse(RadiusText, out double radius) && radius > 0.0;

        public bool IsLineWeightFactorValid =>
            NumberText.TryParse(LineWeightFactorText, out double factor) && factor > 0.0;

        /// <summary>The colour swatch shown on the fixed colour button.</summary>
        public Brush FixedColorBrush
        {
            get
            {
                System.Drawing.Color rgb = HtmlColor.Parse(FixedColor);
                return new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
            }
        }

        public VertexCirclesProfile ToModel() =>
            new VertexCirclesProfile
            {
                Name = Name,
                Settings = new VertexCirclesSettings
                {
                    Radius = _radius,
                    LineWeightFactor = _lineWeightFactor,
                    ColorMode = UseFixedColor
                        ? MarkerColorMode.FixedColor
                        : MarkerColorMode.ComplementaryHue,
                    FixedColor = FixedColor,
                    LayerFilters = LayerFilters
                        .Select(f => f.Pattern.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList(),
                },
            };

        public void AddFilters(IEnumerable<string> patterns)
        {
            var existing = new HashSet<string>(
                LayerFilters.Select(f => f.Pattern.Trim()), StringComparer.OrdinalIgnoreCase);

            foreach (string pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                if (!existing.Add(pattern.Trim())) continue;

                LayerFilters.Add(new LayerFilterViewModel(pattern.Trim()));
            }
        }

        [RelayCommand]
        private void AddMask() => LayerFilters.Add(new LayerFilterViewModel("*"));

        [RelayCommand]
        private void RemoveFilter(LayerFilterViewModel? filter)
        {
            if (filter == null) return;
            LayerFilters.Remove(filter);
        }

        [RelayCommand]
        private void ClearFilters() => LayerFilters.Clear();

        //Keep the parsed values in step with the text, but never let unparseable input
        //destroy the last good value - the overrule keeps drawing while the user types.
        partial void OnRadiusTextChanged(string value)
        {
            if (NumberText.TryParse(value, out double radius) && radius > 0.0)
                _radius = radius;

            OnPropertyChanged(nameof(IsRadiusValid));
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(IsInvalid));
        }

        partial void OnLineWeightFactorTextChanged(string value)
        {
            if (NumberText.TryParse(value, out double factor) && factor > 0.0)
                _lineWeightFactor = factor;

            OnPropertyChanged(nameof(IsLineWeightFactorValid));
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(IsInvalid));
        }

        partial void OnFixedColorChanged(string value) =>
            OnPropertyChanged(nameof(FixedColorBrush));

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void OnLayerFiltersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (LayerFilterViewModel item in e.OldItems)
                    item.PropertyChanged -= OnLayerFilterChanged;

            if (e.NewItems != null)
                foreach (LayerFilterViewModel item in e.NewItems)
                    item.PropertyChanged += OnLayerFilterChanged;

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void OnLayerFilterChanged(object? sender, PropertyChangedEventArgs e) =>
            Changed?.Invoke(this, EventArgs.Empty);
    }
}
