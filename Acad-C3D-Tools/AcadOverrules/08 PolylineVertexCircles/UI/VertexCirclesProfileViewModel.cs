using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// The editable state of one profile. This is the source of truth while the window is
    /// open - <see cref="ToModel"/> projects it back to a serialisable
    /// <see cref="VertexCirclesProfile"/> whenever the settings need to be applied or saved.
    /// Any edit anywhere in the profile, including inside the two marker styles and the layer
    /// filter rows, surfaces as <see cref="Changed"/> so the owner can push a live preview.
    /// </summary>
    public partial class VertexCirclesProfileViewModel : ObservableObject
    {
        /// <summary>Raised for every edit, including edits inside the styles and filters.</summary>
        public event EventHandler? Changed;

        [ObservableProperty]
        private string name;

        public ObservableCollection<LayerFilterViewModel> LayerFilters { get; } = new();

        /// <summary>The circle drawn where only line segments meet.</summary>
        public MarkerStyleViewModel Straight { get; }

        /// <summary>The circle drawn where at least one arc segment meets the vertex.</summary>
        public MarkerStyleViewModel Arc { get; }

        /// <summary>Both styles, for the operations that apply to the profile as a whole.</summary>
        public IReadOnlyList<MarkerStyleViewModel> Styles { get; }

        public VertexCirclesProfileViewModel(VertexCirclesProfile profile)
        {
            name = profile.Name;

            Straight = new MarkerStyleViewModel(profile.Settings.StraightVertex);
            Arc = new MarkerStyleViewModel(profile.Settings.ArcVertex);
            Styles = new[] { Straight, Arc };

            foreach (MarkerStyleViewModel style in Styles)
                style.PropertyChanged += OnStyleChanged;

            foreach (string filter in profile.Settings.LayerFilters)
                LayerFilters.Add(new LayerFilterViewModel(filter));

            foreach (LayerFilterViewModel item in LayerFilters)
                item.PropertyChanged += OnLayerFilterChanged;

            LayerFilters.CollectionChanged += OnLayerFiltersCollectionChanged;
        }

        /// <summary>True while every numeric field in both styles is parseable and positive.</summary>
        public bool IsValid => Styles.All(s => s.IsValid);

        /// <summary>Drives the warning line in the window.</summary>
        public bool IsInvalid => !IsValid;

        public VertexCirclesProfile ToModel() =>
            new VertexCirclesProfile
            {
                Name = Name,
                Settings = new VertexCirclesSettings
                {
                    StraightVertex = Straight.ToModel(),
                    ArcVertex = Arc.ToModel(),
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

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// An edit in either style can flip the validity of the whole profile, which drives
        /// the warning line. Raising it here also raises <see cref="Changed"/> through the
        /// override above, which is what pushes the live preview.
        /// </summary>
        private void OnStyleChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsValid));
            OnPropertyChanged(nameof(IsInvalid));
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
