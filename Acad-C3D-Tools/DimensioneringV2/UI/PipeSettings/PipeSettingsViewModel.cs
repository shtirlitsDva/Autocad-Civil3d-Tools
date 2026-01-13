using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

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

        public PipeSettingsViewModel(PipeTypeConfiguration config, MediumTypeEnum medium, PipeTypes pipeTypes)
        {
            _originalConfig = config;
            _medium = medium;
            _segmentType = config.SegmentType;
            _pipeTypes = pipeTypes;
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
            var dnValues = _pipeTypes.GetAvailableDnValues(selectedType);

            var newPriority = new PipeTypePriority(
                Priorities.Count + 1,
                selectedType,
                dnValues.FirstOrDefault(),
                dnValues.LastOrDefault());

            // Initialize with default accept criteria
            foreach (var dn in dnValues)
            {
                var defaultVelocity = _segmentType == SegmentType.Stikledning ? 1.0 : 1.5;
                var defaultGradient = _segmentType == SegmentType.Stikledning ? 600 : 100;
                newPriority.AcceptCriteria.Add(new DnAcceptCriteria(dn, defaultVelocity, defaultGradient, false));
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
        /// </summary>
        private void EnsureAcceptCriteriaComplete(PipeTypePriority priority)
        {
            var allAvailableDns = _pipeTypes.GetAvailableDnValues(priority.PipeType);
            var existingDns = priority.AcceptCriteria.Select(c => c.NominalDiameter).ToHashSet();

            foreach (var dn in allAvailableDns)
            {
                if (!existingDns.Contains(dn))
                {
                    var defaultVelocity = _segmentType == SegmentType.Stikledning ? 1.0 : 1.5;
                    var defaultGradient = _segmentType == SegmentType.Stikledning ? 600 : 100;
                    priority.AcceptCriteria.Add(new DnAcceptCriteria(dn, defaultVelocity, defaultGradient, false));
                }
            }

            // Sort by DN
            priority.AcceptCriteria = priority.AcceptCriteria.OrderBy(c => c.NominalDiameter).ToList();
        }

        [RelayCommand]
        private void EditRegler()
        {
            MessageBox.Show(
                "Regelkonfiguration er ikke implementeret endnu.",
                "Kommer snart",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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
        /// </summary>
        public void UpdateAcceptCriteriaForPriority(PipeTypePriority priority)
        {
            var availableDns = _pipeTypes.GetAvailableDnValues(priority.PipeType);
            var existingCriteria = priority.AcceptCriteria.ToDictionary(c => c.NominalDiameter);

            priority.AcceptCriteria.Clear();

            foreach (var dn in availableDns)
            {
                if (existingCriteria.TryGetValue(dn, out var existing))
                {
                    priority.AcceptCriteria.Add(existing);
                }
                else
                {
                    var defaultVelocity = _segmentType == SegmentType.Stikledning ? 1.0 : 1.5;
                    var defaultGradient = _segmentType == SegmentType.Stikledning ? 600 : 100;
                    priority.AcceptCriteria.Add(new DnAcceptCriteria(dn, defaultVelocity, defaultGradient, false));
                }
            }
        }
        #endregion
    }
}
