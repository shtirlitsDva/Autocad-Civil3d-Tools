using System.Collections.Generic;

namespace UiSnoop.Models;

internal sealed record SnoopRenderResult
{
    public SnapshotData Snapshot { get; init; } = new();
    public string HumanOutput { get; init; } = string.Empty;
    public string LlmOutput { get; init; } = string.Empty;
    public string JsonOutput { get; init; } = string.Empty;
}

internal sealed record SnapshotData
{
    public string TimestampLocal { get; init; } = string.Empty;
    public bool FollowMouse { get; init; }
    public MousePositions Mouse { get; init; } = new();
    public ColorInfo PixelColor { get; init; } = new();
    public WindowInfo WindowUnderMouse { get; init; } = WindowInfo.Empty;
    public WindowInfo ControlUnderMouse { get; init; } = WindowInfo.Empty;
    public WindowInfo ActiveWindow { get; init; } = WindowInfo.Empty;
    public string StatusBarText { get; init; } = string.Empty;
    public TextCollection VisibleText { get; init; } = new();
    public TextCollection AllText { get; init; } = new();
    public UiAutomationInfo UiAutomation { get; init; } = UiAutomationInfo.Empty;
    public List<HierarchyNode> Hierarchy { get; init; } = new();
}

internal sealed record WindowInfo
{
    public static WindowInfo Empty { get; } = new();

    public string HandleHex { get; init; } = string.Empty;
    public string HandleDec { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string ClassNN { get; init; } = string.Empty;
    public uint ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public bool IsVisible { get; init; }
    public bool IsEnabled { get; init; }
    public RectInfo RectScreen { get; init; } = new();
    public RectInfo RectClient { get; init; } = new();
}

internal sealed record RectInfo
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

internal sealed record PointInfo(int X = 0, int Y = 0);

internal sealed record MousePositions
{
    public PointInfo Screen { get; init; } = new();
    public PointInfo Window { get; init; } = new();
    public PointInfo Client { get; init; } = new();
}

internal sealed record ColorInfo
{
    public string Hex { get; init; } = string.Empty;
    public byte Red { get; init; }
    public byte Green { get; init; }
    public byte Blue { get; init; }
}

internal sealed record TextCollection
{
    public List<string> Lines { get; init; } = new();
    public bool Truncated { get; init; }
}

internal sealed record UiAutomationInfo
{
    public static UiAutomationInfo Empty { get; } = new();

    public string Name { get; init; } = string.Empty;
    public string AutomationId { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string FrameworkId { get; init; } = string.Empty;
    public string ControlType { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool IsOffscreen { get; init; }
    public string Error { get; init; } = string.Empty;
    public List<string> SupportedPatterns { get; init; } = new();
}

internal sealed record HierarchyNode
{
    public string HandleHex { get; init; } = string.Empty;
    public string HandleDec { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}