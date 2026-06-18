namespace GraphViewV3;

internal enum ColorMode { ByDn, BySystem }

/// <summary>User-tunable view/connectivity settings, shared by the Settings tab (writer),
/// the live loop (tolerance), and the graph tab (colour mode, labels). Raises
/// <see cref="Changed"/> so consumers refresh.</summary>
internal sealed class GraphSettings
{
    public double Tolerance { get; set; } = 0.5;
    public ColorMode ColorBy { get; set; } = ColorMode.ByDn;
    public bool AlwaysLabels { get; set; }

    public event Action? Changed;
    public void RaiseChanged() => Changed?.Invoke();
}
