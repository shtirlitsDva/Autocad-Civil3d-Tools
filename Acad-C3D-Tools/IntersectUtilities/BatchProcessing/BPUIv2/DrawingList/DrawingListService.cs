using System.IO;

namespace IntersectUtilities.BatchProcessing.BPUIv2.DrawingList;

/// <summary>
/// Singleton service that manages the current drawing list.
/// Independent from sequences — the same list can be reused across different sequences.
/// </summary>
public class DrawingListService
{
    private static readonly Lazy<DrawingListService> _instance = new(() => new DrawingListService());
    public static DrawingListService Instance => _instance.Value;

    private readonly List<DrawingListItem> _items = new();

    public IReadOnlyList<DrawingListItem> Items => _items.AsReadOnly();
    public DrawingListSource Source { get; private set; } = DrawingListSource.Manual;

    private DrawingListService() { }

    /// <summary>
    /// Scan a folder for .dwg files matching a mask pattern.
    /// </summary>
    public void LoadFromFolder(string folderPath, string mask = "*.dwg", bool includeSubfolders = false)
    {
        _items.Clear();
        Source = DrawingListSource.Folder;

        if (!Directory.Exists(folderPath)) return;

        var searchOption = includeSubfolders
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        var files = Directory.GetFiles(folderPath, mask, searchOption)
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files)
            _items.Add(new DrawingListItem(file));
    }

    /// <summary>
    /// Load drawing paths from a text file (one path per line).
    /// Relative paths are resolved against the text file's directory.
    /// </summary>
    public void LoadFromTextFile(string textFilePath)
    {
        _items.Clear();
        Source = DrawingListSource.TextFile;

        if (!File.Exists(textFilePath)) return;

        string baseDir = Path.GetDirectoryName(textFilePath) ?? "";
        var lines = File.ReadAllLines(textFilePath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        foreach (var line in lines)
        {
            string filePath = line.Trim();
            // Resolve relative paths against the text file's directory
            if (!Path.IsPathRooted(filePath))
                filePath = Path.Combine(baseDir, filePath);

            _items.Add(new DrawingListItem(filePath));
        }
    }

    /// <summary>
    /// Add individual file paths manually.
    /// </summary>
    public void AddFiles(IEnumerable<string> filePaths)
    {
        Source = DrawingListSource.Manual;
        foreach (var path in filePaths)
        {
            if (!_items.Any(i => i.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                _items.Add(new DrawingListItem(path));
        }
    }

    /// <summary>
    /// Remove a drawing from the list by file path.
    /// </summary>
    public void RemoveFile(string filePath)
    {
        _items.RemoveAll(i => i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clear all drawings from the list.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
    }

    /// <summary>
    /// Set inclusion state for all items.
    /// </summary>
    public void SetAllIncluded(bool included)
    {
        foreach (var item in _items)
            item.IsIncluded = included;
    }

    /// <summary>
    /// Returns only the included items whose files exist on disk.
    /// </summary>
    public IReadOnlyList<DrawingListItem> GetActiveItems()
    {
        return _items
            .Where(i => i.IsIncluded && i.FileExists)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Summary string for UI display.
    /// </summary>
    public string GetSummary()
    {
        int total = _items.Count;
        int included = _items.Count(i => i.IsIncluded);
        int missing = _items.Count(i => !i.FileExists);

        if (total == 0) return "No drawings loaded";

        string summary = $"{included} of {total} drawings";
        if (missing > 0) summary += $" ({missing} missing)";
        return summary;
    }
}
