#nullable enable

using System;

namespace EventManager
{
    /// <summary>
    /// The non-generic face of <see cref="HookSlot{THandler}"/>, so a manager can keep every slot
    /// it has created in one list and release them all without knowing their handler types.
    /// </summary>
    internal interface IHookSlot
    {
        /// <summary>True while the underlying hook is installed.</summary>
        bool Installed { get; }

        /// <summary>Drops every handler and uninstalls the underlying hook if it is installed.</summary>
        void Release();
    }

    /// <summary>
    /// One multiplexed event: a multicast delegate holding the subscribers, plus the pair of
    /// actions that install and uninstall the single underlying hook that feeds it.
    /// </summary>
    /// <typeparam name="THandler">The delegate type the event exposes.</typeparam>
    /// <remarks>
    /// The handlers live in a multicast delegate rather than a list, so <see cref="Handlers"/>
    /// hands the forwarder an immutable snapshot: a subscriber may subscribe or unsubscribe from
    /// inside a dispatch without invalidating it. The underlying hook is installed lazily on the
    /// first subscription and uninstalled again when the last handler leaves, so a manager
    /// exposing many events hooks only the ones somebody is listening to, for exactly as long as
    /// somebody is listening. The pair is idempotent and re-armable any number of times.
    /// <para>
    /// Not thread safe. Expected to be used on the AutoCAD main thread.
    /// </para>
    /// </remarks>
    internal sealed class HookSlot<THandler> : IHookSlot where THandler : Delegate
    {
        private readonly Action _install;
        private readonly Action _uninstall;
        private readonly EventManagerTrace _trace;
        private THandler? _handlers;
        private bool _installed;

        internal HookSlot(Action install, Action uninstall, EventManagerTrace trace)
        {
            _install = install ?? throw new ArgumentNullException(nameof(install));
            _uninstall = uninstall ?? throw new ArgumentNullException(nameof(uninstall));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        /// <summary>
        /// The current invocation list, or <c>null</c> when nobody is subscribed. Reading this
        /// property IS the snapshot -- invoke the value it returns, never the field behind it.
        /// </summary>
        internal THandler? Handlers => _handlers;

        /// <inheritdoc />
        public bool Installed => _installed;

        /// <summary>
        /// Adds a handler, installing the underlying hook if this is the first one. If the install
        /// throws, the handler is rolled back out again and the exception reaches the subscriber.
        /// </summary>
        internal void Add(THandler? value)
        {
            if (value == null) return;
            _handlers = (THandler?)Delegate.Combine(_handlers, value);
            if (_installed) return;

            _installed = true;
            try
            {
                _install();
            }
            catch
            {
                _installed = false;
                _handlers = (THandler?)Delegate.Remove(_handlers, value);
                throw;
            }
        }

        /// <summary>
        /// Removes a handler, uninstalling the underlying hook once the last one is gone.
        /// Removing one that was never added is a no-op. The slot stays usable: a later
        /// <see cref="Add"/> installs the hook again, any number of times.
        /// </summary>
        internal void Remove(THandler? value)
        {
            if (value == null || _handlers == null) return;
            _handlers = (THandler?)Delegate.Remove(_handlers, value);
            if (_handlers == null) Uninstall();
        }

        /// <inheritdoc />
        public void Release()
        {
            _handlers = null;
            Uninstall();
        }

        private void Uninstall()
        {
            if (!_installed) return;

            // Clear the flag first: the uninstall action may consult this slot's state, and a
            // failing uninstall must not leave the slot claiming a hook it no longer owns.
            _installed = false;

            // Uninstalling runs on the last unsubscribe and during teardown, often while AutoCAD
            // is already unwinding a document. A failure here must neither abort the rest of the
            // teardown nor escape a subscriber's `-=`.
            _trace.Guard("failed to uninstall an event hook", _uninstall);
        }
    }
}
