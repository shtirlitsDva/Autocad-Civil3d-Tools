using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntersectUtilities.BatchProcessing.BPUIv2.DrawingList;

namespace IntersectUtilities.BatchProcessing.BPUIv2.UI.DrawingList;

public partial class DrawingListViewModel : ObservableObject
{
    private readonly DrawingListService _service = DrawingListService.Instance;

    [ObservableProperty]
    private string folderPath = string.Empty;

    [ObservableProperty]
    private string fileMask = "*.dwg";

    [ObservableProperty]
    private bool includeSubfolders;

    [ObservableProperty]
    private ObservableCollection<DrawingListItemViewModel> drawingItems = new();

    [ObservableProperty]
    private string summary = "No drawings loaded";

    public DrawingListViewModel()
    {
        RefreshItems();
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder containing drawings",
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrEmpty(FolderPath) && Directory.Exists(FolderPath))
            dialog.SelectedPath = FolderPath;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            FolderPath = dialog.SelectedPath;
    }

    [RelayCommand]
    private void ScanFolder()
    {
        if (string.IsNullOrWhiteSpace(FolderPath)) return;

        _service.LoadFromFolder(FolderPath, FileMask, IncludeSubfolders);
        RefreshItems();
    }

    [RelayCommand]
    private void ImportTextFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import drawing list from text file",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            _service.LoadFromTextFile(dialog.FileName);
            RefreshItems();
        }
    }

    [RelayCommand]
    private void AddFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add drawing files",
            Filter = "Drawing files (*.dwg)|*.dwg|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            _service.AddFiles(dialog.FileNames);
            RefreshItems();
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        _service.SetAllIncluded(true);
        foreach (var item in DrawingItems)
            item.IsIncluded = true;
        UpdateSummary();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        _service.SetAllIncluded(false);
        foreach (var item in DrawingItems)
            item.IsIncluded = false;
        UpdateSummary();
    }

    private void RefreshItems()
    {
        DrawingItems.Clear();
        foreach (var item in _service.Items)
            DrawingItems.Add(new DrawingListItemViewModel(item));
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        Summary = _service.GetSummary();
    }
}

public partial class DrawingListItemViewModel : ObservableObject
{
    private readonly DrawingListItem _item;

    public DrawingListItemViewModel(DrawingListItem item)
    {
        _item = item;
        isIncluded = _item.IsIncluded;
    }

    [ObservableProperty]
    private bool isIncluded;

    partial void OnIsIncludedChanged(bool value)
    {
        _item.IsIncluded = value;
    }

    public string FileName => _item.FileName;
    public string FilePath => _item.FilePath;
    public bool FileExists => _item.FileExists;
}
