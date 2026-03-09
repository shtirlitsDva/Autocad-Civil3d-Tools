using System.IO;

namespace IntersectUtilities.BatchProcessing.BPUIv2.DrawingList;

public class DrawingListItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public bool IsIncluded { get; set; } = true;
    public bool FileExists => File.Exists(FilePath);

    public DrawingListItem() { }

    public DrawingListItem(string filePath, bool isIncluded = true)
    {
        FilePath = filePath;
        IsIncluded = isIncluded;
    }
}
