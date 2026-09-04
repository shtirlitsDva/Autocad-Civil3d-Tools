#nullable enable

using System;

namespace EventManager
{
    /// <summary>
    /// One underlying hook shared by several <see cref="HookSlot{THandler}"/>s: installed when the
    /// first of them starts listening, uninstalled once none of them is listening any more.
    /// </summary>
    /// <remarks>
    /// A <see cref="HookSlot{THandler}"/> owns its hook alone, so its own handler count decides
    /// when to let go. This owns a hook that several slots feed off, so that decision cannot be
    /// taken from one slot's state; the siblings answer it collectively through
    /// <c>stillNeeded</c>.
    /// <para>
    /// The installed flag flips only once <c>install</c> has returned. That is the whole point of
    /// the type: a throwing install leaves the hook genuinely not installed, so
    /// <see cref="ReleaseIfUnused"/> stays reachable and <see cref="Ensure"/> stays retryable. A
    /// flag set up front instead would, on the first failure, make the release unreachable and
    /// every later <see cref="Ensure"/> a silent no-op -- the events fed by the hook would then be
    /// dead for the life of the manager.
    /// </para>
    /// <para>
    /// Undoing a half-completed install is the install action's own business -- only it knows how
    /// far it got -- and it must do that before it rethrows.
    /// </para>
    /// <para>Not thread safe. Expected to be used on the AutoCAD main thread.</para>
    /// </remarks>
    internal sealed class SharedHook
    {
        private readonly Action _install;
        private readonly Action _uninstall;
        private readonly Func<bool> _stillNeeded;
        private readonly EventManagerTrace _trace;
        private bool _installed;

        internal SharedHook(
            Action install, Action uninstall, Func<bool> stillNeeded, EventManagerTrace trace)
        {
            _install = install ?? throw new ArgumentNullException(nameof(install));
            _uninstall = uninstall ?? throw new ArgumentNullException(nameof(uninstall));
            _stillNeeded = stillNeeded ?? throw new ArgumentNullException(nameof(stillNeeded));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        /// <summary>True while the underlying hook is installed.</summary>
        internal bool Installed => _installed;

        /// <summary>
        /// Installs the hook unless it already is. A failing install reaches the subscriber, and
        /// leaves this in the not-installed state so a later attempt starts over cleanly.
        /// </summary>
        internal void Ensure()
        {
            if (_installed) return;

            _install();
            _installed = true;
        }

        /// <summary>
        /// Uninstalls the hook, but only once no sibling still needs it. Never throws.
        /// </summary>
        internal void ReleaseIfUnused()
        {
            if (!_installed || _stillNeeded()) return;
            Release();
        }

        /// <summary>
        /// Uninstalls the hook regardless of what the siblings say -- teardown, where there are no
        /// siblings left to consult. Never throws.
        /// </summary>
        internal void Release()
        {
            if (!_installed) return;

            // Clear the flag first: a failing uninstall must not leave this claiming a hook it no
            // longer owns, because that would block every later Ensure.
            _installed = false;
            _trace.Guard("failed to uninstall a shared event hook", _uninstall);
        }
    }
}
