using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using NorsynHydraulicCalc;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace DimensioneringV2.UI.PipeSettings
{
    public partial class RegelViewModel : ObservableObject
    {
        private readonly PipeTypePriority _priority;
        private readonly IEnumerable<PipeType> _availableParentPipeTypes;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmpty))]
        private ObservableCollection<RuleViewModelBase> rules;

        [ObservableProperty]
        private RuleViewModelBase? selectedRule;

        [ObservableProperty]
        private string windowTitle;

        /// <summary>
        /// True when there are no rules configured.
        /// </summary>
        public bool IsEmpty => Rules == null || Rules.Count == 0;

        /// <summary>
        /// Available parent pipe types (from FL configuration).
        /// </summary>
        public IEnumerable<PipeType> AvailableParentPipeTypes => _availableParentPipeTypes;

        /// <summary>
        /// Result indicating whether the user confirmed the changes.
        /// </summary>
        public bool DialogResult { get; private set; }

        public RegelViewModel(PipeTypePriority priority, IEnumerable<PipeType> availableParentPipeTypes)
        {
            _priority = priority;
            _availableParentPipeTypes = availableParentPipeTypes;

            // Create view models from the NHS rules
            Rules = new ObservableCollection<RuleViewModelBase>(
                priority.Rules.Select(r => RuleViewModelBase.FromRule(r)));

            // Subscribe to collection changes to update IsEmpty
            Rules.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsEmpty));

            WindowTitle = $"Regler for {priority.PipeType}";

            // Select first if available
            if (Rules.Count > 0)
            {
                SelectedRule = Rules[0];
            }
        }

        partial void OnSelectedRuleChanged(RuleViewModelBase? value)
        {
            DeleteCommand.NotifyCanExecuteChanged();
        }

        #region Commands
        [RelayCommand]
        private void Add()
        {
            // Filter out parent pipe types that are already used
            var usedParentTypes = Rules
                .OfType<ParentPipeRuleViewModel>()
                .Select(r => r.ParentPipeType)
                .ToHashSet();

            var availableTypes = _availableParentPipeTypes
                .Where(pt => !usedParentTypes.Contains(pt))
                .ToList();

            if (availableTypes.Count == 0)
            {
                MessageBox.Show(
                    "Alle tilgængelige forældrerørtyper er allerede brugt.",
                    "Ingen tilgængelige typer",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Show dialog to add a rule
            var dialog = new AddRuleDialog(availableTypes);
            if (dialog.ShowDialog() != true)
                return;

            var newRule = new ParentPipeRuleViewModel(dialog.SelectedParentPipeType);
            Rules.Add(newRule);
            SelectedRule = newRule;
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private void Delete()
        {
            if (SelectedRule == null) return;

            int index = Rules.IndexOf(SelectedRule);
            Rules.Remove(SelectedRule);

            // Select next item or previous if at end
            if (Rules.Count > 0)
            {
                SelectedRule = Rules[Math.Min(index, Rules.Count - 1)];
            }
            else
            {
                SelectedRule = null;
            }
        }

        private bool CanDelete() => SelectedRule != null;

        [RelayCommand]
        private void Ok(Window window)
        {
            // Convert view models back to NHS rules and update the priority
            _priority.Rules.Clear();
            foreach (var ruleVm in Rules)
            {
                _priority.Rules.Add(ruleVm.ToRule());
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
    }
}
