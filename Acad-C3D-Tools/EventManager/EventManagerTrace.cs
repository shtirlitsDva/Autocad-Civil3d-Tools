#nullable enable

using System;

namespace EventManager
{
    /// <summary>
    /// The one place the EventManager reports a failure it deliberately swallowed.
    /// </summary>
    /// <remarks>
    /// Everything reported here happens on a path that must not throw -- AutoCAD's own document
    /// lifecycle dispatch, a subscriber's <c>-=</c>, the idle pump -- so the alternative to
    /// reporting is losing the failure entirely.
    /// <para>
    /// The shared project has no logging dependency of its own and the plugins hosting it log to
    /// different places, so the destination is injected: a plugin passes its own sink to
    /// <c>AcadEventManager</c>'s constructor. Without one the report goes to
    /// <see cref="System.Diagnostics.Trace"/>, which in a Release AutoCAD process reaches only a
    /// native debugger -- enough while developing, not enough to appear in a user's bug report,
    /// which is exactly why the sink exists.
    /// </para>
    /// <para>
    /// One instance per manager, deliberately not static: two plugins load into the same AutoCAD
    /// process, and a static sink would both let the second overwrite the first and let a plugin
    /// that unloads strand a delegate pointing into an unloaded assembly.
    /// </para>
    /// </remarks>
    internal sealed class EventManagerTrace
    {
        private readonly Action<string, Exception?>? _sink;

        internal EventManagerTrace(Action<string, Exception?>? sink) => _sink = sink;

        /// <summary>Reports a swallowed failure. Never throws.</summary>
        internal void Report(string message, Exception? ex)
        {
            if (_sink != null)
            {
                try
                {
                    _sink(message, ex);
                    return;
                }
                catch (Exception sinkFailure)
                {
                    // The host's own logger is broken. Trace is the only destination left, and
                    // this call sits on a path that must not throw, so report both there.
                    Write($"the log sink threw while reporting \"{message}\"", sinkFailure);
                }
            }

            Write(message, ex);
        }

        /// <summary>
        /// Runs one step that is allowed to fail, reporting instead of propagating. Used where
        /// the alternative is aborting a teardown half way through and stranding the rest of it.
        /// </summary>
        internal void Guard(string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                Report(what, ex);
            }
        }

        private static void Write(string message, Exception? ex)
            => System.Diagnostics.Trace.WriteLine(
                ex == null
                    ? $"AcadEventManager: {message}"
                    : $"AcadEventManager: {message}: {ex}");
    }
}
