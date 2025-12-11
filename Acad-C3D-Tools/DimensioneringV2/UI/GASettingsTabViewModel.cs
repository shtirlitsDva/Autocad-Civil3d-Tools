using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DimensioneringV2.Genetic;
using DimensioneringV2.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DimensioneringV2.UI
{
    public partial class GASettingsTabViewModel : ObservableObject
    {
        [ObservableProperty]
        private GASettings settings;

        public Array ChromosomeTypes => Enum.GetValues(typeof(ChromosomeType));
        public Array SelectionTypes => Enum.GetValues(typeof(SelectionType));
        public Array ReinsertionTypes => Enum.GetValues(typeof(ReinsertionType));
        public Array TerminationTypes => Enum.GetValues(typeof(TerminationType));
        public Array TaskExecutorTypes => Enum.GetValues(typeof(TaskExecutorType));

        /// <summary>
        /// Gets available crossover types based on selected chromosome.
        /// StrictUnique is only available for Strict chromosome.
        /// </summary>
        public IEnumerable<CrossoverType> AvailableCrossoverTypes
        {
            get
            {
                var all = Enum.GetValues(typeof(CrossoverType)).Cast<CrossoverType>();
                if (Settings.ChromosomeType == ChromosomeType.Strict)
                    return all;
                // For Relaxed chromosome, exclude StrictUnique
                return all.Where(c => c != CrossoverType.StrictUnique);
            }
        }

        /// <summary>
        /// Gets available mutation types based on selected chromosome.
        /// StrictGraph is only available for Strict chromosome.
        /// </summary>
        public IEnumerable<MutationType> AvailableMutationTypes
        {
            get
            {
                var all = Enum.GetValues(typeof(MutationType)).Cast<MutationType>();
                if (Settings.ChromosomeType == ChromosomeType.Strict)
                    return all;
                // For Relaxed chromosome, exclude StrictGraph
                return all.Where(m => m != MutationType.StrictGraph);
            }
        }

        public GASettingsTabViewModel()
        {
            settings = GASettingsService.Instance.Settings;
            settings.PropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GASettings.SelectionType))
                OnPropertyChanged(nameof(IsTournamentSelected));
            else if (e.PropertyName == nameof(GASettings.CrossoverType))
            {
                OnPropertyChanged(nameof(IsCrossoverMixProbabilityVisible));
                OnPropertyChanged(nameof(CrossoverMixProbability));
            }
            else if (e.PropertyName == nameof(GASettings.TerminationType))
                OnPropertyChanged(nameof(TerminationParametersView));
            else if (e.PropertyName == nameof(GASettings.TaskExecutorType))
                OnPropertyChanged(nameof(IsParallelExecutorSelected));
            else if (e.PropertyName == nameof(GASettings.ChromosomeType))
            {
                OnPropertyChanged(nameof(AvailableCrossoverTypes));
                OnPropertyChanged(nameof(AvailableMutationTypes));
                OnPropertyChanged(nameof(IsRelaxedChromosomeSelected));
                
                // Reset to valid defaults if current selection is no longer available
                if (Settings.ChromosomeType == ChromosomeType.Relaxed)
                {
                    if (Settings.CrossoverType == CrossoverType.StrictUnique)
                        Settings.CrossoverType = CrossoverType.Uniform;
                    if (Settings.MutationType == MutationType.StrictGraph)
                        Settings.MutationType = MutationType.FlipBit;
                }
            }
        }

        public bool IsTournamentSelected => Settings.SelectionType == SelectionType.Tournament;
        
        public bool IsCrossoverMixProbabilityVisible => 
            Settings.CrossoverType == CrossoverType.Uniform || 
            Settings.CrossoverType == CrossoverType.StrictUnique;

        public bool IsParallelExecutorSelected => Settings.TaskExecutorType == TaskExecutorType.Parallel;

        /// <summary>
        /// Returns true when Relaxed chromosome is selected (used to show graduated penalty checkbox).
        /// </summary>
        public bool IsRelaxedChromosomeSelected => Settings.ChromosomeType == ChromosomeType.Relaxed;

        public float CrossoverMixProbability
        {
            get => Settings.CrossoverType == CrossoverType.Uniform 
                ? Settings.UniformCrossoverMixProbability 
                : Settings.StrictUniqueCrossoverMixProbability;
            set
            {
                if (Settings.CrossoverType == CrossoverType.Uniform)
                    Settings.UniformCrossoverMixProbability = value;
                else
                    Settings.StrictUniqueCrossoverMixProbability = value;
                OnPropertyChanged();
            }
        }

        public object? TerminationParametersView
        {
            get
            {
                return Settings.TerminationType switch
                {
                    TerminationType.GenerationNumber => CreateParameterPanel("Max Generations:", "Settings.GenerationNumberTerminationCount"),
                    TerminationType.FitnessStagnation => CreateParameterPanel("Stagnant Generations:", "Settings.FitnessStagnationTerminationCount"),
                    TerminationType.FitnessThreshold => CreateParameterPanel("Target Fitness:", "Settings.FitnessThresholdTerminationValue"),
                    TerminationType.TimeEvolving => CreateParameterPanel("Max Time (seconds):", "Settings.TimeEvolvingTerminationSeconds"),
                    _ => null
                };
            }
        }

        private StackPanel CreateParameterPanel(string labelText, string bindingPath)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var label = new Label
            {
                Content = labelText,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            };
            label.SetResourceReference(Label.ForegroundProperty, "Text");
            panel.Children.Add(label);
            
            var textBox = new TextBox { Width = 60, Margin = new Thickness(4, 0, 0, 0) };
            textBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(bindingPath)
            {
                Source = this,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
            panel.Children.Add(textBox);
            
            return panel;
        }

        [RelayCommand]
        private void SaveSettings()
        {
            GASettingsService.Instance.SaveToActiveDocument();
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            GASettingsService.Instance.ResetToDefaults();
            Settings = GASettingsService.Instance.Settings;
            Settings.PropertyChanged += Settings_PropertyChanged;
            
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(IsTournamentSelected));
            OnPropertyChanged(nameof(IsCrossoverMixProbabilityVisible));
            OnPropertyChanged(nameof(CrossoverMixProbability));
            OnPropertyChanged(nameof(TerminationParametersView));
            OnPropertyChanged(nameof(IsParallelExecutorSelected));
            OnPropertyChanged(nameof(AvailableCrossoverTypes));
            OnPropertyChanged(nameof(AvailableMutationTypes));
            OnPropertyChanged(nameof(IsRelaxedChromosomeSelected));
        }
    }
}
