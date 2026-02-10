using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SheetCreationAutomation.Services;
using System.Threading;
using System.Windows;

namespace SheetCreationAutomation.ViewModels
{
    public partial class DebugVisualTreeViewModel : ObservableObject
    {
        private readonly SynchronizationContext? uiContext;

        [ObservableProperty]
        private string _logText = string.Empty;

        public DebugVisualTreeViewModel()
        {
            uiContext = SynchronizationContext.Current;
            LogText = AutomationRunLog.GetText();
            AutomationRunLog.LogChanged += OnLogChanged;
        }

        [RelayCommand]
        private void ClearLog()
        {
            AutomationRunLog.Clear();
        }

        [RelayCommand]
        private void CopyLog()
        {
            string text = LogText ?? string.Empty;
            if (text.Length == 0)
            {
                return;
            }

            Clipboard.SetText(text);
        }

        private void OnLogChanged()
        {
            if (uiContext != null && SynchronizationContext.Current != uiContext)
            {
                uiContext.Post(_ => LogText = AutomationRunLog.GetText(), null);
                return;
            }

            LogText = AutomationRunLog.GetText();
        }
    }
}
