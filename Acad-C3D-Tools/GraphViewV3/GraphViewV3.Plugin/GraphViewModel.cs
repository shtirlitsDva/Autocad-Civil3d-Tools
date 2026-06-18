using GraphViewV3.Core;

namespace GraphViewV3;

/// <summary>State the WPF view renders from. The live loop pushes a new NetworkResult on
/// the UI thread; the control redraws on <see cref="Updated"/>. Event-driven (not data
/// binding) because the graph is canvas-drawn, not item-templated.</summary>
internal sealed class GraphViewModel
{
    public NetworkResult Latest { get; private set; } = NetworkResult.Empty;
    public string Status { get; private set; } = "Open a drawing — waiting for FJV entities…";

    public event Action? Updated;

    public void Set(NetworkResult result, string status)
    {
        Latest = result;
        Status = status;
        Updated?.Invoke();
    }

    public void SetStatus(string status)
    {
        Status = status;
        Updated?.Invoke();
    }
}
