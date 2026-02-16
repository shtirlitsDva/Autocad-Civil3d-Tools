using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SheetCreationAutomation.Models;
using SheetCreationAutomation.Procedures.Sheets;
using SheetCreationAutomation.Services;
using SheetCreationAutomation.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SheetCreationAutomation.ViewModels
{
    public partial class SheetsAutomationViewModel : ObservableObject
    {
        private static readonly Regex PipelineNumberRegexOld = new Regex(@"(?<number>\d{2,3}?\s)", RegexOptions.Compiled);
        private static readonly Regex PipelineNumberRegexNew = new Regex(@"(?<number>\d{2,3})", RegexOptions.Compiled);

        private readonly SheetAutomationRunner automationRunner;
        private CancellationTokenSource? runCts;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartSheetsScriptCommand))]
        private bool _planOnly;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartSheetsScriptCommand))]
        private string _viewFrameFolder = string.Empty;

        [ObservableProperty]
        private string _fileListStatus = string.Empty;

        [ObservableProperty]
        private string _fileListPath = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartSheetsScriptCommand))]
        private string _sheetSetLocation = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartSheetsScriptCommand))]
        private string _coordinates = string.Empty;

        [ObservableProperty]
        private string _lastStatus = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartSheetsScriptCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelSheetsScriptCommand))]
        private bool _isRunning;

        public SheetsAutomationViewModel()
        {
            var waitPolicy = new WaitPolicy();
            var overlay = new WaitOverlayPresenter();
            var wizardDriver = new Civil3dCreateSheetsUiDriver(overlay, waitPolicy);
            automationRunner = new SheetAutomationRunner(wizardDriver, waitPolicy, overlay);

            LoadState();
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            try
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog();
                if (Directory.Exists(ViewFrameFolder))
                {
                    dialog.InitialDirectory = ViewFrameFolder;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ViewFrameFolder = EnsureTrailingSlash(dialog.SelectedPath);
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
                if (!Directory.Exists(ViewFrameFolder))
                {
                    LastStatus = "View Frame folder does not exist.";
                    return;
                }

                string outputPath = Path.Combine(ViewFrameFolder, "fileList.txt");
                var fileNames = Directory.EnumerateFiles(ViewFrameFolder, "*_VF.dwg")
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Where(name => GetPipelineNumber(name!) != 0)
                    .OrderBy(name => GetPipelineNumber(name!))
                    .ToList();

                if (fileNames.Count == 0)
                {
                    LastStatus = "No *_VF.dwg files found.";
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

        [RelayCommand]
        private void SelectSheetSet()
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Sheet Set Files|*.dst|All files|*.*"
                };

                string? folder = Path.GetDirectoryName(SheetSetLocation);
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                {
                    dialog.InitialDirectory = folder;
                }

                if (dialog.ShowDialog() == true)
                {
                    SheetSetLocation = dialog.FileName;
                    SaveState();
                }
            }
            catch (Exception ex)
            {
                LastStatus = $"Select Sheet Set failed: {ex.Message}";
            }
        }

        [RelayCommand(CanExecute = nameof(CanStartSheetsScript))]
        private async Task StartSheetsScript()
        {
            if (IsRunning)
            {
                return;
            }

            try
            {
                AutomationRunLog.Clear();
                AutomationRunLog.Append("Sheets run requested.");

                if (!ValidateInputs(out List<string> errors))
                {
                    LastStatus = "Validation failed: " + string.Join(" | ", errors);
                    AutomationRunLog.Append(LastStatus);
                    return;
                }

                if (!ResolveDrawingPaths(out IReadOnlyList<string> drawingsToProcess, out string drawingResolutionError))
                {
                    LastStatus = drawingResolutionError;
                    AutomationRunLog.Append(LastStatus);
                    return;
                }

                IsRunning = true;
                runCts = new CancellationTokenSource();
                LastStatus = $"Starting native Sheet Creation automation for {drawingsToProcess.Count} drawing(s)...";
                AutomationRunLog.Append(LastStatus);

                var context = new SheetAutomationContext
                {
                    ViewFrameFolder = ViewFrameFolder,
                    FileListPath = FileListPath,
                    SheetSetFilePath = ResolveAbsolutePath(SheetSetLocation),
                    ProfileViewOrigin = Coordinates.Trim(),
                    PlanOnly = PlanOnly,
                    DrawingPaths = drawingsToProcess
                };

                var progress = new Progress<string>(message =>
                {
                    LastStatus = message;
                    AutomationRunLog.Append(message);
                });

                SheetAutomationRunResult result = await ExecuteOnAcContextAsync(() =>
                    automationRunner.RunAsync(context, progress, runCts.Token));

                if (result.Succeeded)
                {
                    SaveState();
                    LastStatus = "Completed successfully.";
                    AutomationRunLog.Append(LastStatus);
                }
                else
                {
                    string failureMessage = result.Failure == null
                        ? "Automation failed without detailed diagnostics."
                        : $"Automation failed at step '{result.Failure.StepName}' " +
                          $"for '{Path.GetFileName(result.Failure.DrawingPath)}' after {result.Failure.Elapsed:mm\\:ss}. " +
                          $"{result.Failure.Message}";

                    LastStatus = failureMessage;
                    AutomationRunLog.Append(failureMessage);
                    if (result.Failure != null)
                    {
                        AcDebug.Print(failureMessage);
                        if (!string.IsNullOrWhiteSpace(result.Failure.SelectorSnapshot))
                        {
                            AutomationRunLog.Append("Failure selector snapshot:");
                            AutomationRunLog.Append(result.Failure.SelectorSnapshot);
                            AcDebug.Print(result.Failure.SelectorSnapshot);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LastStatus = "Automation cancelled.";
                AutomationRunLog.Append(LastStatus);
            }
            catch (Exception ex)
            {
                LastStatus = $"Start sheet script failed: {ex.Message}";
                AutomationRunLog.Append(LastStatus);
                AutomationRunLog.Append(ex.ToString());
                AcDebug.Print(ex);
            }
            finally
            {
                IsRunning = false;
                runCts?.Dispose();
                runCts = null;
            }
        }

        [RelayCommand(CanExecute = nameof(CanCancelSheetsScript))]
        private void CancelSheetsScript()
        {
            if (!IsRunning || runCts == null)
            {
                return;
            }

            LastStatus = "Cancellation requested...";
            AutomationRunLog.Append(LastStatus);
            runCts.Cancel();
        }

        private bool CanStartSheetsScript()
        {
            if (IsRunning)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(ViewFrameFolder)
                || string.IsNullOrWhiteSpace(FileListPath)
                || string.IsNullOrWhiteSpace(SheetSetLocation))
            {
                return false;
            }

            return PlanOnly || !string.IsNullOrWhiteSpace(Coordinates);
        }

        private bool CanCancelSheetsScript() => IsRunning;

        partial void OnPlanOnlyChanged(bool value)
        {
            SaveState();
            StartSheetsScriptCommand.NotifyCanExecuteChanged();
        }

        partial void OnViewFrameFolderChanged(string value)
        {
            ViewFrameFolder = EnsureTrailingSlash(value);
            UpdateFileListStatus();
            SaveState();
        }

        partial void OnSheetSetLocationChanged(string value) => SaveState();
        partial void OnCoordinatesChanged(string value) => SaveState();

        private void LoadState()
        {
            SheetsUiState state = AutomationSettingsStore.LoadSheetsState();
            PlanOnly = state.PlanOnly;
            ViewFrameFolder = EnsureTrailingSlash(state.ViewFrameFolder);
            FileListPath = state.FileListPath ?? string.Empty;
            SheetSetLocation = state.SheetSetLocation ?? string.Empty;
            Coordinates = state.Coordinates ?? string.Empty;
            UpdateFileListStatus();
        }

        private void SaveState()
        {
            var state = new SheetsUiState
            {
                PlanOnly = PlanOnly,
                ViewFrameFolder = ViewFrameFolder,
                FileListPath = FileListPath,
                SheetSetLocation = SheetSetLocation,
                Coordinates = Coordinates
            };

            AutomationSettingsStore.SaveSheetsState(state);
        }

        private void UpdateFileListStatus()
        {
            try
            {
                string candidate = string.IsNullOrWhiteSpace(ViewFrameFolder)
                    ? string.Empty
                    : Path.Combine(ViewFrameFolder, "fileList.txt");

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

        private bool ValidateInputs(out List<string> errors)
        {
            errors = new List<string>();

            if (!Directory.Exists(ViewFrameFolder))
            {
                errors.Add($"Missing folder: {ViewFrameFolder}");
            }

            if (!File.Exists(FileListPath))
            {
                errors.Add($"Missing fileList.txt: {FileListPath}");
            }

            if (!File.Exists(SheetSetLocation))
            {
                errors.Add($"Missing sheet set file: {SheetSetLocation}");
            }

            if (!PlanOnly && string.IsNullOrWhiteSpace(Coordinates))
            {
                errors.Add("Profile View Origin coordinates are required when Plan only is unchecked.");
            }

            return errors.Count == 0;
        }

        private bool ResolveDrawingPaths(out IReadOnlyList<string> drawingPaths, out string error)
        {
            drawingPaths = Array.Empty<string>();
            error = string.Empty;

            try
            {
                if (!File.Exists(FileListPath))
                {
                    error = $"fileList.txt not found: {FileListPath}";
                    return false;
                }

                var items = File.ReadAllLines(FileListPath, Encoding.UTF8)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (items.Count == 0)
                {
                    error = "fileList.txt contains no drawings.";
                    return false;
                }

                var resolved = new List<string>();
                foreach (string item in items)
                {
                    string path = Path.IsPathRooted(item) ? item : Path.Combine(ViewFrameFolder, item);
                    if (!File.Exists(path))
                    {
                        error = $"Drawing listed in fileList.txt does not exist: {path}";
                        return false;
                    }

                    resolved.Add(path);
                }

                drawingPaths = resolved;
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to resolve file list drawings: {ex.Message}";
                return false;
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

        private static string ResolveAbsolutePath(string inputPath)
        {
            string candidate = inputPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            return Path.GetFullPath(candidate);
        }

        private static int GetPipelineNumber(string str)
        {
            string number = string.Empty;
            if (PipelineNumberRegexOld.IsMatch(str))
            {
                number = PipelineNumberRegexOld.Match(str).Groups["number"].Value;
            }
            else if (PipelineNumberRegexNew.IsMatch(str))
            {
                number = PipelineNumberRegexNew.Match(str).Groups["number"].Value;
            }

            if (string.IsNullOrWhiteSpace(number))
            {
                return 0;
            }

            return Convert.ToInt16(number);
        }

        private static Task<T> ExecuteOnAcContextAsync<T>(Func<Task<T>> action)
        {
            SynchronizationContext? context = AcContext.Current;
            if (context == null || SynchronizationContext.Current == context)
            {
                return action();
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Post(async _ =>
            {
                try
                {
                    T result = await action();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }
    }
}
