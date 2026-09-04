#nullable enable

using System;
using System.Collections.Generic;

namespace EventManager
{
    /// <summary>
    /// The arm-work-disarm idle debounce: actions queue up, one idle tick runs everything queued
    /// so far, and the underlying idle hook is let go of again as soon as the queue is drained.
    /// </summary>
    /// <remarks>
    /// The idle hook is injected as an arm/disarm pair, the same way <see cref="HookSlot{THandler}"/>
    /// takes its install/uninstall pair, so the mechanics here are plain BCL logic. The behaviour
    /// this type guarantees is documented in full on <c>AcadEventManager.RunOnNextIdle</c>,
    /// which is its only caller; the comments below cover only why the code is shaped as it is.
    /// <para>Not thread safe. Expected to be used on the AutoCAD main thread.</para>
    /// </remarks>
    internal sealed class IdleDrain
    {
        private readonly Action _arm;
        private readonly Action _disarm;
        private readonly EventManagerTrace _trace;

        /// <summary>Actions waiting for the next idle tick, in the order they were queued.</summary>
        private readonly List<Action> _queue = new();

        private bool _armed;
        private bool _draining;

        internal IdleDrain(Action arm, Action disarm, EventManagerTrace trace)
        {
            _arm = arm ?? throw new ArgumentNullException(nameof(arm));
            _disarm = disarm ?? throw new ArgumentNullException(nameof(disarm));
            _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        /// <summary>True while the idle hook is held.</summary>
        internal bool Armed => _armed;

        /// <summary>True only while <see cref="Tick"/> is running the queued actions.</summary>
        internal bool Draining => _draining;

        /// <summary>How many actions are waiting for the next tick.</summary>
        internal int Pending => _queue.Count;

        /// <summary>
        /// Queues one action and arms the idle hook if it is not armed already. A failing arm
        /// reaches the caller, with the action taken back off the queue first.
        /// </summary>
        internal void Enqueue(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            _queue.Add(action);
            if (_armed) return;

            _armed = true;
            try
            {
                _arm();
            }
            catch
            {
                _armed = false;

                // Take back the entry just appended, by position. Removing by value would pick
                // the first delegate that compares equal, which need not be this one.
                _queue.RemoveAt(_queue.Count - 1);
                throw;
            }
        }

        /// <summary>Runs one generation of queued actions. Never throws.</summary>
        internal void Tick()
        {
            // Bail out BEFORE disarming. A queued action may pump messages, and AutoCAD then
            // re-raises Idle with this drain still on the stack; consuming the arming here would
            // throw away the subscription that whatever ran inside the modal just took out.
            if (_draining) return;

            // One shot per arming: disarm first and unconditionally, so a later failure cannot
            // leave the drain running on every tick.
            _armed = false;
            _trace.Guard("failed to release the idle hook", _disarm);

            if (_queue.Count == 0) return;

            var due = _queue.ToArray();
            _queue.Clear();

            _draining = true;
            try
            {
                foreach (var queued in due)
                    _trace.Guard("an action queued for the next idle threw", queued);
            }
            finally
            {
                _draining = false;
            }
        }

        /// <summary>
        /// Drops everything still queued and lets go of the idle hook. Never throws.
        /// </summary>
        /// <remarks>
        /// A generation already in flight cannot be withdrawn -- <see cref="Tick"/> holds it in a
        /// local array and will finish running it -- so this only guarantees that nothing further
        /// is queued and that no new tick is armed.
        /// </remarks>
        internal void Abandon()
        {
            _queue.Clear();
            if (!_armed) return;

            _armed = false;
            _trace.Guard("failed to release the idle hook", _disarm);
        }
    }
}
