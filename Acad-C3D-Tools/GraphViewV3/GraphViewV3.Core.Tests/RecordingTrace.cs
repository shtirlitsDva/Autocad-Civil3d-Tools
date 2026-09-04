using EventManager;

namespace GraphViewV3.Core.Tests;

/// <summary>
/// Captures what the EventManager reported instead of throwing, so a test can tell a failure that
/// was handled from one that was silently dropped.
/// </summary>
/// <remarks>
/// This is also what the injectable sink is for in production: a plugin passes its own logger to
/// <c>AcadEventManager</c>'s constructor, and what the tests below assert on is exactly what a
/// user's log would show.
/// </remarks>
internal sealed class RecordingTrace
{
    internal List<(string Message, Exception? Error)> Reports { get; } = new();

    internal EventManagerTrace Trace { get; }

    internal RecordingTrace() => Trace = new EventManagerTrace((m, e) => Reports.Add((m, e)));

    /// <summary>True when something was reported whose message contains <paramref name="fragment"/>.</summary>
    internal bool Reported(string fragment)
        => Reports.Any(r => r.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
