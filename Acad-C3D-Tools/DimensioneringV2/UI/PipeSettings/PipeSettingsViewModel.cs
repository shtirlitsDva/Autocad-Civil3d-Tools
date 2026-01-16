using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;
using NorsynHydraulicCalc.Rules;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace DimensioneringV2.UI.PipeSettings
{
    public partial class PipeSettingsViewModel : ObservableObject
    {
        private readonly PipeTypeConfiguration _originalConfig;
        private readonly MediumTypeEnum _medium;
        private readonly SegmentType _segmentType;
        private readonly PipeTypes _pipeTypes;
        private readonly IEnumerable<PipeType> _flPipeTypes;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmpty))]
        private ObservableCollection<PipeTypePriority> priorities;

        [ObservableProperty]
        private PipeTypePriority? selectedPriority;

        [ObservableProperty]
        private string windowTitle;

        [ObservableProperty]
        private bool isStikledning;

        /// <summary>
        /// True when there are no priorities configured.
        /// </summary>
        public bool IsEmpty => Priorities == null || Priorities.Count == 0;

        /// <summary>
        /// Available pipe types for the current medium and segment type.
        /// </summary>
        public IEnumerable<PipeType> AvailablePipeTypes { get; }

        /// <summary>
        /// Result indicating whether the user confirmed the changes.
        /// </summary>
        public bool DialogResult { get; private set; }

        /// <summary>
        /// Gets DN values for a specific pipe type (used by DN ComboBoxes).
        /// </summary>
        public int[] GetDnValuesForPipeType => SelectedPriority != null
            ? _pipeTypes.GetAvailableDnValues(SelectedPriority.PipeType)
            : Array.Empty<int>();

        /// <summary>
        /// Exposes PipeTypes for the converter to use.
        /// </summary>
        public PipeTypes PipeTypes => _pipeTypes;

        /// <summary>
        /// Creates a ViewModel for FL pipe settings (no rules support).
        /// </summary>
        public PipeSettingsViewModel(PipeTypeConfiguration config, MediumTypeEnum medium, PipeTypes pipeTypes)
            : this(config, medium, pipeTypes, Enumerable.Empty<PipeType>())
        {
        }

        /// <summary>
        /// Creates a ViewModel for SL pipe settings with support for rules.
        /// </summary>
        /// <param name="config">The SL pipe configuration to edit.</param>
        /// <param name="medium">The medium type.</param>
        /// <param name="pipeTypes">The pipe types instance for DN lookups.</param>
        /// <param name="flPipeTypes">Available FL pipe types for parent pipe rules.</param>
        public PipeSettingsViewModel(PipeTypeConfiguration config, MediumTypeEnum medium, PipeTypes pipeTypes, IEnumerable<PipeType> flPipeTypes)
        {
            _originalConfig = config;
            _medium = medium;
            _segmentType = config.SegmentType;
            _pipeTypes = pipeTypes;
            _flPipeTypes = flPipeTypes;
            IsStikledning = _segmentType == SegmentType.Stikledning;

            // Create a working copy of the priorities
            Priorities = new ObservableCollection<PipeTypePriority>(
                config.Priorities.Select(p => p.Clone()));

            // Subscribe to collection changes to update IsEmpty
            Priorities.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsEmpty));

            // Set window title based on segment type
            WindowTitle = _segmentType == SegmentType.Fordelingsledning
                ? "Rørindstillinger for Fordelingsledninger"
                : "Rørindstillinger for Stikledninger";

            // Get available pipe types for this medium and segment type (from NHS)
            AvailablePipeTypes = _segmentType == SegmentType.Fordelingsledning
                ? MediumPipeTypeRules.GetValidPipeTypesForSupply(medium)
                : MediumPipeTypeRules.GetValidPipeTypesForService(medium);

            // Select first if available
            if (Priorities.Count > 0)
            {
                SelectedPriority = Priorities[0];
            }
        }

        /// <summary>
        /// Gets DN values for a specific pipe type.
        /// </summary>
        public int[] GetDnValues(PipeType pipeType)
        {
            return _pipeTypes.GetAvailableDnValues(pipeType);
        }

        partial void OnSelectedPriorityChanged(PipeTypePriority? value)
        {
            OnPropertyChanged(nameof(GetDnValuesForPipeType));
            DeleteCommand.NotifyCanExecuteChanged();
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }

        #region Commands
        [RelayCommand]
        private void Add()
        {
            // Show dialog to select pipe type
            var dialog = new AddPipeTypeDialog(AvailablePipeTypes);
            if (dialog.ShowDialog() != true)
                return;

            var selectedType = dialog.SelectedPipeType;
            var pipe = _pipeTypes.GetPipeType(selectedType) as PipeBase;
            var dnValues = _pipeTypes.GetAvailableDnValues(selectedType);

            var newPriority = new PipeTypePriority(
                Priorities.Count + 1,
                selectedType,
                dnValues.FirstOrDefault(),
                dnValues.LastOrDefault());

            // Initialize with default accept criteria from pipe type
            if (pipe != null)
            {
                newPriority.AcceptCriteria = pipe.GetAllDefaultAcceptCriteria(_segmentType);
            }
            else
            {
                // Fallback if pipe doesn't inherit from PipeBase (shouldn't happen)
                foreach (var dn in dnValues)
                {
                    newPriority.AcceptCriteria.Add(new DnAcceptCriteria(dn, 2.0, 100, true));
                }
            }

            Priorities.Add(newPriority);
            SelectedPriority = newPriority;
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private void Delete()
        {
            if (SelectedPriority == null) return;

            int index = Priorities.IndexOf(SelectedPriority);
            Priorities.Remove(SelectedPriority);

            // Renumber remaining priorities and refresh the list
            RefreshPrioritiesAfterReorder();

            // Select next item or previous if at end
            if (Priorities.Count > 0)
            {
                SelectedPriority = Priorities[Math.Min(index, Priorities.Count - 1)];
            }
            else
            {
                SelectedPriority = null;
            }
        }

        private bool CanDelete() => SelectedPriority != null;

        [RelayCommand(CanExecute = nameof(CanMoveUp))]
        private void MoveUp()
        {
            if (SelectedPriority == null) return;

            int index = Priorities.IndexOf(SelectedPriority);
            if (index <= 0) return;

            var item = SelectedPriority;
            Priorities.Move(index, index - 1);
            RefreshPrioritiesAfterReorder();
            SelectedPriority = item;
        }

        private bool CanMoveUp() => SelectedPriority != null && Priorities.IndexOf(SelectedPriority) > 0;

        [RelayCommand(CanExecute = nameof(CanMoveDown))]
        private void MoveDown()
        {
            if (SelectedPriority == null) return;

            int index = Priorities.IndexOf(SelectedPriority);
            if (index >= Priorities.Count - 1) return;

            var item = SelectedPriority;
            Priorities.Move(index, index + 1);
            RefreshPrioritiesAfterReorder();
            SelectedPriority = item;
        }

        private bool CanMoveDown() => SelectedPriority != null && Priorities.IndexOf(SelectedPriority) < Priorities.Count - 1;

        [RelayCommand]
        private void EditAcceptCriteria(PipeTypePriority? priority)
        {
            var target = priority ?? SelectedPriority;
            if (target == null) return;

            // Ensure all available DNs have accept criteria entries
            EnsureAcceptCriteriaComplete(target);

            var window = new AcceptCriteriaWindow();
            var viewModel = new AcceptCriteriaViewModel(target);
            window.DataContext = viewModel;
            window.ShowDialog();

            // Force UI refresh to update the criteria count
            var index = Priorities.IndexOf(target);
            if (index >= 0)
            {
                Priorities[index] = target;
            }
        }

        /// <summary>
        /// Ensures that the priority has accept criteria for all available DNs.
        /// This is needed when the user changes the DN range or when loading old data.
        /// New DNs get default values from CSV; existing DNs are marked uninitialized if they're new to the range.
        /// </summary>
        private void EnsureAcceptCriteriaComplete(PipeTypePriority priority)
        {
            var pipe = _pipeTypes.GetPipeType(priority.PipeType) as PipeBase;
            var allAvailableDns = _pipeTypes.GetAvailableDnValues(priority.PipeType);
            var existingDns = priority.AcceptCriteria.Select(c => c.NominalDiameter).ToHashSet();

            foreach (var dn in allAvailableDns)
            {
                if (!existingDns.Contains(dn))
                {
                    // Get default values from pipe type
                    if (pipe != null)
                    {
                        var defaultCriteria = pipe.CreateDefaultAcceptCriteria(dn, _segmentType);
                        // Mark as not initialized so user sees the orange warning
                        defaultCriteria.IsInitialized = false;
                        priority.AcceptCriteria.Add(defaultCriteria);
                    }
                    else
                    {
                        // Fallback with generic defaults
                        priority.AcceptCriteria.Add(new DnAcceptCriteria(dn, 2.0, 100, false));
                    }
                }
            }

            // Sort by DN
            priority.AcceptCriteria = priority.AcceptCriteria.OrderBy(c => c.NominalDiameter).ToList();
        }

        [RelayCommand]
        private void EditRegler(PipeTypePriority? priority)
        {
            var target = priority ?? SelectedPriority;
            if (target == null) return;

            if (!_flPipeTypes.Any())
            {
                MessageBox.Show(
                    "Ingen fordelingsledninger er konfigureret.\nKonfigurer FL først for at kunne definere forældrerør-regler.",
                    "Ingen FL konfiguration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var window = new RegelWindow();
            var viewModel = new RegelViewModel(target, _flPipeTypes);
            window.DataContext = viewModel;
            window.ShowDialog();

            // Force UI refresh
            var index = Priorities.IndexOf(target);
            if (index >= 0)
            {
                Priorities[index] = target;
            }
        }

        [RelayCommand]
        private void Ok(Window window)
        {
            // Copy the modified priorities back to the original configuration
            _originalConfig.Priorities.Clear();
            foreach (var priority in Priorities)
            {
                _originalConfig.Priorities.Add(priority);
            }

            DialogResult = true;
            window.DialogResult = true;
            window.Close();
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            DialogResult = false;
            window.DialogResult = false;
            window.Close();
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Renumbers priorities and forces a UI refresh by recreating the collection.
        /// This is needed because PipeTypePriority doesn't implement INotifyPropertyChanged.
        /// </summary>
        private void RefreshPrioritiesAfterReorder()
        {
            // Update priority numbers
            for (int i = 0; i < Priorities.Count; i++)
            {
                Priorities[i].Priority = i + 1;
            }

            // Force UI refresh by recreating the collection
            var items = Priorities.ToList();
            Priorities.Clear();
            foreach (var item in items)
            {
                Priorities.Add(item);
            }

            // Update command states
            MoveUpCommand.NotifyCanExecuteChanged();
            MoveDownCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Updates the accept criteria when pipe type or DN range changes.
        /// Preserves existing user-defined criteria; adds defaults from CSV for new DNs.
        /// </summary>
        public void UpdateAcceptCriteriaForPriority(PipeTypePriority priority)
        {
            var pipe = _pipeTypes.GetPipeType(priority.PipeType) as PipeBase;
            var availableDns = _pipeTypes.GetAvailableDnValues(priority.PipeType);
            var existingCriteria = priority.AcceptCriteria.ToDictionary(c => c.NominalDiameter);

            priority.AcceptCriteria.Clear();

            foreach (var dn in availableDns)
            {
                if (existingCriteria.TryGetValue(dn, out var existing))
                {
                    priority.AcceptCriteria.Add(existing);
                }
                else if (pipe != null)
                {
                    // Get default values from pipe type, mark as not initialized
                    var defaultCriteria = pipe.CreateDefaultAcceptCriteria(dn, _segmentType);
                    defaultCriteria.IsInitialized = false;
                    priority.AcceptCriteria.Add(defaultCriteria);
                }
                else
                {
                    // Fallback with generic defaults
                    priority.AcceptCriteria.Add(new DnAcceptCriteria(dn, 2.0, 100, false));
                }
            }
        }
        #endregion
    }
}
