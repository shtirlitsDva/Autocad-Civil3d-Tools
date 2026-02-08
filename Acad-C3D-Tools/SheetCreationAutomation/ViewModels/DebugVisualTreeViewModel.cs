using Autodesk.AutoCAD.ApplicationServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SheetCreationAutomation.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Automation;

namespace SheetCreationAutomation.ViewModels
{
    public partial class DebugVisualTreeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _lastError = string.Empty;

        [ObservableProperty]
        private string _selectedDetails = string.Empty;

        public ObservableCollection<WindowNodeViewModel> Nodes { get; } = new ObservableCollection<WindowNodeViewModel>();

        public DebugVisualTreeViewModel()
        {
            Refresh();
        }

        [RelayCommand]
        private void Refresh()
        {
            try
            {
                Nodes.Clear();
                IntPtr rootHandle = Application.MainWindow.Handle;
                if (rootHandle == IntPtr.Zero)
                {
                    LastError = "AutoCAD main window handle not found.";
                    return;
                }

                WindowNodeViewModel root = BuildNode(rootHandle, 0);
                Nodes.Add(root);
                LastError = string.Empty;
                SelectedDetails = BuildDetailText(root);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        public void SelectNode(WindowNodeViewModel? node)
        {
            if (node == null)
            {
                return;
            }

            SelectedDetails = BuildDetailText(node);
        }

        private static WindowNodeViewModel BuildNode(IntPtr hwnd, int depth)
        {
            WindowMetadata metadata = Win32WindowTools.GetMetadata(hwnd);
            var node = new WindowNodeViewModel
            {
                Handle = hwnd,
                Title = metadata.Title,
                ClassName = metadata.ClassName,
                ProcessId = metadata.ProcessId,
                ThreadId = metadata.ThreadId,
                IsVisible = metadata.IsVisible,
                IsEnabled = metadata.IsEnabled,
                Depth = depth
            };

            foreach (IntPtr child in Win32WindowTools.EnumerateDirectChildWindows(hwnd))
            {
                WindowMetadata childMetadata = Win32WindowTools.GetMetadata(child);
                if (string.IsNullOrWhiteSpace(childMetadata.ClassName))
                {
                    continue;
                }

                node.Children.Add(BuildNode(child, depth + 1));
            }

            return node;
        }

        private static string BuildDetailText(WindowNodeViewModel node)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HWND: 0x{node.Handle.ToInt64():X}");
            sb.AppendLine($"Class: {node.ClassName}");
            sb.AppendLine($"Title: {node.Title}");
            sb.AppendLine($"PID/TID: {node.ProcessId}/{node.ThreadId}");
            sb.AppendLine($"Visible/Enabled: {node.IsVisible}/{node.IsEnabled}");
            sb.AppendLine();
            sb.AppendLine("UIA:");

            try
            {
                AutomationElement element = AutomationElement.FromHandle(node.Handle);
                sb.AppendLine($"Name: {element.Current.Name}");
                sb.AppendLine($"AutomationId: {element.Current.AutomationId}");
                sb.AppendLine($"ControlType: {element.Current.ControlType.ProgrammaticName}");
                sb.AppendLine($"UIA ClassName: {element.Current.ClassName}");
                sb.AppendLine($"IsEnabled: {element.Current.IsEnabled}");
                sb.AppendLine($"IsOffscreen: {element.Current.IsOffscreen}");
                sb.AppendLine("Patterns:");

                foreach (string pattern in GetSupportedPatterns(element))
                {
                    sb.AppendLine($"- {pattern}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Unavailable: {ex.Message}");
            }

            return sb.ToString();
        }

        private static IEnumerable<string> GetSupportedPatterns(AutomationElement element)
        {
            var patterns = new List<(AutomationPattern Pattern, string Name)>
            {
                (InvokePattern.Pattern, "Invoke"),
                (TogglePattern.Pattern, "Toggle"),
                (ValuePattern.Pattern, "Value"),
                (SelectionItemPattern.Pattern, "SelectionItem"),
                (SelectionPattern.Pattern, "Selection"),
                (ExpandCollapsePattern.Pattern, "ExpandCollapse"),
                (WindowPattern.Pattern, "Window"),
                (ScrollPattern.Pattern, "Scroll"),
                (TextPattern.Pattern, "Text"),
                (RangeValuePattern.Pattern, "RangeValue")
            };

            foreach ((AutomationPattern pattern, string name) in patterns)
            {
                if (element.TryGetCurrentPattern(pattern, out _))
                {
                    yield return name;
                }
            }
        }
    }

    public partial class WindowNodeViewModel : ObservableObject
    {
        [ObservableProperty]
        private IntPtr _handle;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _className = string.Empty;

        [ObservableProperty]
        private uint _processId;

        [ObservableProperty]
        private uint _threadId;

        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private int _depth;

        public ObservableCollection<WindowNodeViewModel> Children { get; } = new ObservableCollection<WindowNodeViewModel>();

        public string Display => $"{ClassName} | \"{Title}\" | 0x{Handle.ToInt64():X}";
    }
}
