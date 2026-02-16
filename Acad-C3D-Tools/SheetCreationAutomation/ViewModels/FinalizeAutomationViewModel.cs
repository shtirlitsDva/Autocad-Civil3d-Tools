using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SheetCreationAutomation.Models;
using SheetCreationAutomation.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SheetCreationAutomation.ViewModels
{
    public partial class FinalizeAutomationViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartFinalizeScriptCommand))]
        private string _sheetsFolderPath = string.Empty;

        [ObservableProperty]
        private string _fileListStatus = string.Empty;

        [ObservableProperty]
        private string _fileListPath = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartFinalizeScriptCommand))]
        private string _projectName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartFinalizeScriptCommand))]
        private string _etapeName = string.Empty;

        [ObservableProperty]
        private string _lastStatus = string.Empty;

        public FinalizeAutomationViewModel()
        {
            LoadState();
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            try
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog();
                if (Directory.Exists(SheetsFolderPath))
                {
                    dialog.InitialDirectory = SheetsFolderPath;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SheetsFolderPath = EnsureTrailingSlash(dialog.SelectedPath);
                    UpdateFileListStatus();
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                LastStatus = $"Browse folder failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CreateFileList()
        {
            try
            {
                if (!Directory.Exists(SheetsFolderPath))
                {
                    LastStatus = "Sheets folder does not exist.";
                    return;
                }

                string outputPath = Path.Combine(SheetsFolderPath, "fileList.txt");
                var fileNames = Directory.EnumerateFiles(SheetsFolderPath, "*_SHT.dwg")
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (fileNames.Count == 0)
                {
                    LastStatus = "No *_SHT.dwg files found.";
                    return;
                }

                var sb = new StringBuilder();
                foreach (string fileName in fileNames)
                {
                    sb.AppendLine(fileName);
                }

                File.WriteAllText(outputPath, sb.ToString().TrimEnd('\r', '\n'), new UTF8Encoding(false));
                UpdateFileListStatus();
                SaveState();
                LastStatus = $"Created fileList.txt with {fileNames.Count} entries.";
            }
            catch (Exception ex)
            {
                LastStatus = $"Create file list failed: {ex.Message}";
            }
        }

        [RelayCommand]
        private void OpenFileList()
        {
            try
            {
                if (!File.Exists(FileListPath))
                {
                    LastStatus = "fileList.txt not found.";
                    return;
                }

                Process.Start("notepad.exe", FileListPath)?.WaitForExit();
                UpdateFileListStatus();
                SaveState();
            }
            catch (Exception ex)
            {
                LastStatus = $"Open file list failed: {ex.Message}";
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartFinalizeScript))]
        private void StartFinalizeScript()
        {
            LastStatus = "Finalize tab UI is ready. Script wiring for tab 3 will be added next.";
            AutomationRunLog.Append(LastStatus);
        }

        private bool CanStartFinalizeScript()
        {
            return !string.IsNullOrWhiteSpace(SheetsFolderPath)
                && !string.IsNullOrWhiteSpace(FileListPath)
                && !string.IsNullOrWhiteSpace(ProjectName)
                && !string.IsNullOrWhiteSpace(EtapeName);
        }

        partial void OnSheetsFolderPathChanged(string value)
        {
            SheetsFolderPath = EnsureTrailingSlash(value);
            UpdateFileListStatus();
            SaveState();
        }
        partial void OnProjectNameChanged(string value) => SaveState();
        partial void OnEtapeNameChanged(string value) => SaveState();

        private void LoadState()
        {
            FinalizeUiState state = AutomationSettingsStore.LoadFinalizeState();
            SheetsFolderPath = EnsureTrailingSlash(state.SheetsFolderPath);
            FileListPath = state.FileListPath ?? string.Empty;
            ProjectName = state.ProjectName ?? string.Empty;
            EtapeName = state.EtapeName ?? string.Empty;
            UpdateFileListStatus();
        }

        private void SaveState()
        {
            var state = new FinalizeUiState
            {
                SheetsFolderPath = SheetsFolderPath,
                FileListPath = FileListPath,
                ProjectName = ProjectName,
                EtapeName = EtapeName
            };

            AutomationSettingsStore.SaveFinalizeState(state);
        }

        private void UpdateFileListStatus()
        {
            try
            {
                string candidate = string.IsNullOrWhiteSpace(SheetsFolderPath)
                    ? string.Empty
                    : Path.Combine(SheetsFolderPath, "fileList.txt");

                if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                {
                    FileListPath = string.Empty;
                    FileListStatus = "fileList.txt not found.";
                    return;
                }

                FileListPath = candidate;
                int count = File.ReadAllLines(candidate, Encoding.UTF8).Length;
                FileListStatus = $"fileList.txt FOUND. Entries: {count}.";
            }
            catch (Exception ex)
            {
                FileListStatus = $"fileList status failed: {ex.Message}";
            }
        }

        private static string EnsureTrailingSlash(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.EndsWith("\\", StringComparison.Ordinal) ? path : path + "\\";
        }
    }
}
