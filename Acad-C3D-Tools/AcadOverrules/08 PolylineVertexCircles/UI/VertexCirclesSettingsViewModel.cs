using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

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

            _livePreview.Tick += (_, _) =>
            {
                _livePreview.Stop();
                ApplyLive();
            };
        }

        /// <summary>Where the profiles are stored, shown as a hint in the window.</summary>
        public string SettingsPath => VertexCirclesSettingsStore.SettingsPath;

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
        private void PickFixedColor()
        {
            if (SelectedProfile == null) return;

            var dialog = new Autodesk.AutoCAD.Windows.ColorDialog();
            dialog.Color = ToAcadColor(SelectedProfile.FixedColor);

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            //ColorValue resolves ACI and true colour alike, so any tab of the dialog works.
            SelectedProfile.FixedColor = HtmlColor.Format(dialog.Color.ColorValue);
            SelectedProfile.UseFixedColor = true;
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

        private static Autodesk.AutoCAD.Colors.Color ToAcadColor(string html)
        {
            System.Drawing.Color rgb = HtmlColor.Parse(html);
            return Autodesk.AutoCAD.Colors.Color.FromRgb(rgb.R, rgb.G, rgb.B);
        }
    }
}
