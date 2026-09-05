using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

using Autodesk.AutoCAD.DatabaseServices;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// Drives the TOGGLEPOLYVERTICESSETTINGS window.
    ///
    /// The profile view models are the source of truth while the window is open. Every edit
    /// rebuilds the whole config and pushes it into <see cref="VertexCirclesSettingsService"/>
    /// for a live preview, but the settings file is only written when the window closes or
    /// when a profile is added, renamed or removed - so typing does not touch the disk.
    /// </summary>
    public partial class VertexCirclesSettingsViewModel : ObservableObject
    {
        private readonly VertexCirclesSettingsService _service = VertexCirclesSettingsService.Instance;

        /// <summary>
        /// A single edit raises several notifications (the value, then the derived validity
        /// and swatch properties), and a regen is not cheap in a big drawing. Coalescing the
        /// burst into one regen keeps typing smooth.
        /// </summary>
        private readonly DispatcherTimer _livePreview = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

        [ObservableProperty]
        private VertexCirclesProfileViewModel? selectedProfile;

        [ObservableProperty]
        private bool isOverruleEnabled;

        public ObservableCollection<VertexCirclesProfileViewModel> Profiles { get; } = new();

        public VertexCirclesSettingsViewModel()
        {
            VertexCirclesConfig config = _service.Config;

            foreach (VertexCirclesProfile profile in config.Profiles)
                Add(new VertexCirclesProfileViewModel(profile.Clone()));

            if (Profiles.Count == 0)
                Add(new VertexCirclesProfileViewModel(new VertexCirclesProfile()));

            selectedProfile =
                Profiles.FirstOrDefault(
                    p => string.Equals(p.Name, config.ActiveProfileName, StringComparison.OrdinalIgnoreCase))
                ?? Profiles[0];

            isOverruleEnabled = Commands.IsOverruleActive<PolylineVertexCircles>();

            AvailableLinetypes = BuildLinetypeChoices();
            AvailableSegmentLinetypes =
                new[] { SegmentStyleViewModel.PolylineLinetypeChoice }
                    .Concat(AvailableLinetypes)
                    .ToList();

            _livePreview.Tick += (_, _) =>
            {
                _livePreview.Stop();
                ApplyLive();
            };
        }

        /// <summary>Where the profiles are stored, shown as a hint in the window.</summary>
        public string SettingsPath => VertexCirclesSettingsStore.SettingsPath;

        /// <summary>
        /// The linetype names offered in the vertex tabs of the Appearance group: the ones
        /// loaded in the active drawing plus the ones the profiles already store. A profile
        /// travels between drawings, so its linetype need not be loaded here - keeping the
        /// stored name in the list is what stops the ComboBox from clearing it. A name that is
        /// not loaded in the drawing on screen draws as Continuous, see
        /// <see cref="LinetypeResolver"/>.
        /// </summary>
        public IReadOnlyList<string> AvailableLinetypes { get; }

        /// <summary>
        /// The same list for the segment tabs, with "same as the polyline" in front: a
        /// segment override can keep the linetype of the polyline, a circle has no polyline
        /// linetype to keep.
        /// </summary>
        public IReadOnlyList<string> AvailableSegmentLinetypes { get; }

        public bool CanDeleteProfile => Profiles.Count > 1;

        /// <summary>
        /// Pushes the current editor state into the running overrule and regenerates, so the
        /// drawing behind the window updates as the user types.
        /// </summary>
        public void ApplyLive()
        {
            _service.SetConfig(BuildConfig());
            Commands.RegenActiveDocument();
        }

        /// <summary>Applies and writes the profiles to disk. Called when the window closes.</summary>
        public void SaveOnExit()
        {
            _livePreview.Stop();
            _service.SetConfig(BuildConfig());

            try
            {
                _service.Save();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Could not save the vertex circle profiles to\n{SettingsPath}\n\n{ex.Message}",
                    "Polyline vertex circles", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            Commands.RegenActiveDocument();
        }

        [RelayCommand]
        private void NewProfile()
        {
            string? name = NameInputDialog.Prompt(
                "New profile", "Name of the new profile:", UniqueName("Profile"));
            if (name == null) return;

            var profile = new VertexCirclesProfileViewModel(
                new VertexCirclesProfile { Name = UniqueName(name) });

            Add(profile);
            SelectedProfile = profile;
            Persist();
        }

        [RelayCommand]
        private void CopyProfile()
        {
            if (SelectedProfile == null) return;

            string? name = NameInputDialog.Prompt(
                "Copy profile", "Name of the copy:", UniqueName($"{SelectedProfile.Name} copy"));
            if (name == null) return;

            VertexCirclesProfile copy = SelectedProfile.ToModel();
            copy.Name = UniqueName(name);

            var profile = new VertexCirclesProfileViewModel(copy);

            Add(profile);
            SelectedProfile = profile;
            Persist();
        }

        [RelayCommand]
        private void RenameProfile()
        {
            if (SelectedProfile == null) return;

            string? name = NameInputDialog.Prompt(
                "Rename profile", "New name:", SelectedProfile.Name);
            if (name == null) return;
            if (string.Equals(name, SelectedProfile.Name, StringComparison.Ordinal)) return;

            SelectedProfile.Name = UniqueName(name);
            Persist();
        }

        [RelayCommand]
        private void DeleteProfile()
        {
            if (SelectedProfile == null) return;

            if (Profiles.Count <= 1)
            {
                MessageBox.Show(
                    "The last profile cannot be deleted.",
                    "Polyline vertex circles", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(
                    $"Delete the profile \"{SelectedProfile.Name}\"?",
                    "Polyline vertex circles",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            VertexCirclesProfileViewModel doomed = SelectedProfile;
            int index = Profiles.IndexOf(doomed);

            doomed.Changed -= OnProfileChanged;
            Profiles.Remove(doomed);

            SelectedProfile = Profiles[Math.Max(0, index - 1)];
            OnPropertyChanged(nameof(CanDeleteProfile));
            Persist();
        }

        [RelayCommand]
        private void AddLayersFromDrawing()
        {
            if (SelectedProfile == null) return;

            var picked = LayerPickerDialog.PickLayers();
            if (picked == null || picked.Count == 0) return;

            SelectedProfile.AddFilters(picked);
        }

        [RelayCommand]
        private void ResetProfile()
        {
            if (SelectedProfile == null) return;

            if (MessageBox.Show(
                    $"Reset the profile \"{SelectedProfile.Name}\" to the default settings?",
                    "Polyline vertex circles",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var defaults = new VertexCirclesProfile { Name = SelectedProfile.Name };
            var replacement = new VertexCirclesProfileViewModel(defaults);

            int index = Profiles.IndexOf(SelectedProfile);
            SelectedProfile.Changed -= OnProfileChanged;

            replacement.Changed += OnProfileChanged;
            Profiles[index] = replacement;
            SelectedProfile = replacement;
        }

        partial void OnSelectedProfileChanged(VertexCirclesProfileViewModel? value) => ApplyLive();

        partial void OnIsOverruleEnabledChanged(bool value)
        {
            //Apply first, so the overrule starts up with what is on screen.
            _service.SetConfig(BuildConfig());
            Commands.SetOverruleActive<PolylineVertexCircles>(value);
        }

        /// <summary>
        /// Builds <see cref="AvailableLinetypes"/> from the drawing and from what the profiles
        /// already store, then snaps every style onto the list.
        /// </summary>
        private IReadOnlyList<string> BuildLinetypeChoices()
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                MarkerStyle.DefaultLinetype,
            };

            foreach (string name in ReadLinetypeNames()) names.Add(name);

            foreach (VertexCirclesProfileViewModel profile in Profiles)
                foreach (StyleViewModelBase style in profile.Styles)
                    SnapToDrawingSpelling(style, names);

            return names.ToList();
        }

        /// <summary>
        /// Makes sure a style's stored linetype is in <paramref name="names"/> and spelled the
        /// way the drawing spells it: the ComboBox matches items with Equals, so a stored
        /// "DASHED" would not select a table entry named "Dashed". A name the drawing does not
        /// have is kept as it stands, because the profile travels between drawings.
        /// A segment style showing the "same as the polyline" placeholder is not a table
        /// entry and is left alone.
        /// </summary>
        private static void SnapToDrawingSpelling(StyleViewModelBase style, SortedSet<string> names)
        {
            string stored = (style.Linetype ?? string.Empty).Trim();

            if (style is SegmentStyleViewModel &&
                SegmentStyleViewModel.IsPolylineLinetypeChoice(stored))
            {
                style.Linetype = SegmentStyleViewModel.PolylineLinetypeChoice;
                return;
            }

            if (stored.Length == 0)
            {
                style.Linetype = MarkerStyle.DefaultLinetype;
                return;
            }

            if (!names.TryGetValue(stored, out string? loaded))
            {
                names.Add(stored);
                if (!string.Equals(style.Linetype, stored, StringComparison.Ordinal))
                    style.Linetype = stored;
                return;
            }

            if (!string.Equals(style.Linetype, loaded, StringComparison.Ordinal))
                style.Linetype = loaded;
        }

        /// <summary>The linetypes loaded in the active drawing.</summary>
        private static IEnumerable<string> ReadLinetypeNames()
        {
            var names = new List<string>();

            Autodesk.AutoCAD.ApplicationServices.Document? doc =
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return names;

            using (Transaction tx = doc.Database.TransactionManager.StartOpenCloseTransaction())
            {
                var table = (LinetypeTable)tx.GetObject(doc.Database.LinetypeTableId, OpenMode.ForRead);

                foreach (ObjectId id in table)
                {
                    var record = (LinetypeTableRecord)tx.GetObject(id, OpenMode.ForRead);

                    //ByLayer and ByBlock are records in the table but not a look a marker can
                    //have - the overrule sets the trait outright, so what they would resolve
                    //to is not predictable. They are left out of the list on purpose.
                    if (string.Equals(record.Name, "ByLayer", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(record.Name, "ByBlock", StringComparison.OrdinalIgnoreCase)) continue;

                    names.Add(record.Name);
                }

                tx.Commit();
            }

            return names;
        }

        private VertexCirclesConfig BuildConfig() =>
            new VertexCirclesConfig
            {
                ActiveProfileName = SelectedProfile?.Name ?? VertexCirclesProfile.DefaultProfileName,
                Profiles = Profiles.Select(p => p.ToModel()).ToList(),
            };

        private void Add(VertexCirclesProfileViewModel profile)
        {
            profile.Changed += OnProfileChanged;
            Profiles.Add(profile);
            OnPropertyChanged(nameof(CanDeleteProfile));
        }

        private void OnProfileChanged(object? sender, EventArgs e)
        {
            _livePreview.Stop();
            _livePreview.Start();
        }

        /// <summary>Applies and writes to disk - used for the structural profile operations.</summary>
        private void Persist()
        {
            _service.SetConfig(BuildConfig());

            try
            {
                _service.Save();
            }
            catch (System.Exception)
            {
                //Reported on close by SaveOnExit; a failed intermediate save is not worth a dialog.
            }
        }

        /// <summary>Appends a counter until the name is free.</summary>
        private string UniqueName(string wanted)
        {
            string candidate = string.IsNullOrWhiteSpace(wanted) ? "Profile" : wanted.Trim();

            bool Taken(string name) =>
                Profiles.Any(p => p != SelectedProfile
                    && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (!Taken(candidate)) return candidate;

            for (int i = 2; ; i++)
            {
                string numbered = $"{candidate} {i}";
                if (!Taken(numbered)) return numbered;
            }
        }
    }
}
